Option Strict On
Option Explicit On
Option Infer On
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Models
Namespace Data.Supabase
    Public Class ReturnSaveUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil tindakan belum terkonfirmasi. Klik Periksa simpan untuk memeriksa permintaan yang sama. Jangan mengulang pemberian refund atau penerimaan uang. Jika menutup form, muat ulang daftar sebelum tindakan baru.", inner)
        End Sub
    End Class
    Partial Public NotInheritable Class SupabaseRentalRepository
        Private Async Function RequireReturnAsync(action As ReturnAction?, ct As CancellationToken) As Task
            Dim user = Await GetCurrentUserAsync(ct).ConfigureAwait(False)
            If Not action.HasValue Then Return
            Dim allowed = user.Role = UserRole.SuperAdmin OrElse (action.Value = ReturnAction.Inspect AndAlso user.Role = UserRole.GudangQc) OrElse (action.Value = ReturnAction.Settle AndAlso user.Role = UserRole.FrontOffice)
            If Not allowed Then Throw New UnauthorizedAccessException("Akun tidak dapat melakukan tahap pengembalian ini.")
        End Function
        Public Async Function GetReturnDetailsAsync(rentalId As Long, ct As CancellationToken) As Task(Of ReturnDetails) Implements IRentalRepository.GetReturnDetailsAsync
            If rentalId <= 0 Then Throw New ArgumentException("Pilih sewa terlebih dahulu.")
            Await RequireReturnAsync(Nothing, ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Dim result = Await gateway.RpcAsync(Of ReturnDetails)("get_return_details", New With {.p_rental_id = rentalId}, token, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.Id <> rentalId OrElse result.Units Is Nothing OrElse result.Units.Count = 0 Then Throw New InvalidDataException("Rincian pengembalian tidak lengkap.")
            If result.Financial IsNot Nothing Then
                Dim money = result.Financial
                If {money.Deposit, money.ProposedCharge, money.LateCharge, money.TotalCharges, money.DepositApplied, money.Refund, money.Additional}.Any(Function(value) value < 0 OrElse value <> Decimal.Truncate(value)) OrElse money.DepositApplied <> Math.Min(money.Deposit, money.TotalCharges) OrElse money.Refund <> Math.Max(0, money.Deposit - money.TotalCharges) OrElse money.Additional <> Math.Max(0, money.TotalCharges - money.Deposit) Then Throw New InvalidDataException("Rincian penyelesaian uang tidak seimbang. Muat ulang sebelum melanjutkan.")
            End If
            Return result
        End Function
        Public Async Function ProcessReturnAsync(requestId As Guid, rentalId As Long, action As ReturnAction, checks As List(Of ReturnCheck), method As String, reference As String, ct As CancellationToken) As Task(Of PickupReceipt) Implements IRentalRepository.ProcessReturnAsync
            If requestId = Guid.Empty OrElse rentalId <= 0 Then Throw New ArgumentException("Identitas tindakan tidak valid.")
            If Not [Enum].IsDefined(action) Then Throw New ArgumentException("Tahap pengembalian tidak valid.")
            If action = ReturnAction.Inspect Then
                If checks Is Nothing OrElse checks.Count = 0 OrElse checks.Select(Function(x) x.UnitId).Distinct().Count() <> checks.Count OrElse checks.Any(Function(x) x.UnitId <= 0 OrElse Not {"good", "damaged", "lost"}.Contains(x.Condition) OrElse x.Charge < 0 OrElse x.Charge <> Decimal.Truncate(x.Charge) OrElse (x.Condition = "good" AndAlso x.Charge <> 0) OrElse (x.Condition <> "good" AndAlso (x.Charge <= 0 OrElse String.IsNullOrWhiteSpace(x.Notes)))) Then Throw New ArgumentException("Isi kondisi seluruh unit. Barang rusak/hilang wajib memiliki biaya positif dan catatan.")
            ElseIf action = ReturnAction.Settle AndAlso Not {"cash", "transfer"}.Contains(method) Then
                Throw New ArgumentException("Pilih metode penyelesaian uang.")
            End If
            Await RequireReturnAsync(action, ct).ConfigureAwait(False)
            Dim parameters As Object
            Dim rpc As String
            Select Case action
                Case ReturnAction.Inspect
                    rpc = "inspect_return"
                    parameters = New With {.p_request_id = requestId, .p_rental_id = rentalId, .p_checks = checks.Select(Function(x) New With {.unit_id = x.UnitId, .condition = x.Condition, .charge = x.Charge, .notes = x.Notes}).ToArray()}
                Case ReturnAction.Approve
                    rpc = "approve_return_charges"
                    parameters = New With {.p_request_id = requestId, .p_rental_id = rentalId}
                Case ReturnAction.Settle
                    rpc = "settle_return"
                    parameters = New With {.p_request_id = requestId, .p_rental_id = rentalId, .p_method = method, .p_reference = reference}
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(action))
            End Select
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            ct.ThrowIfCancellationRequested()
            Try
                Dim payload = Await gateway.RpcAsync(Of JsonElement)(rpc, parameters, token, ct).ConfigureAwait(False)
                If payload.ValueKind = JsonValueKind.Array Then
                    If payload.GetArrayLength() <> 1 Then Throw New InvalidDataException("Bukti tindakan tidak sesuai.")
                    payload = payload(0)
                End If
                If payload.ValueKind <> JsonValueKind.Object Then Throw New InvalidDataException("Format bukti tindakan tidak sesuai.")
                Dim receipt = payload.Deserialize(Of PickupReceipt)()
                If receipt Is Nothing OrElse receipt.Id <> rentalId OrElse receipt.State <> "returned" Then Throw New InvalidDataException("Bukti pengembalian tidak lengkap.")
                Return receipt
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New ReturnSaveUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New ReturnSaveUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
