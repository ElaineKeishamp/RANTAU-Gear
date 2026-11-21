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
    Public Class PickupSaveUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil pengambilan belum terkonfirmasi. Klik Periksa simpan untuk memeriksa permintaan yang sama. Jangan menerima pembayaran lagi. Jika menutup form, muat ulang Booking sebelum mencoba pengambilan ulang.", inner)
        End Sub
    End Class
    Partial Public NotInheritable Class SupabaseRentalRepository
        Public Async Function GetPickupDetailsAsync(rentalId As Long, ct As CancellationToken) As Task(Of PickupDetails) Implements IRentalRepository.GetPickupDetailsAsync
            If rentalId <= 0 Then Throw New ArgumentException("Pilih reservasi terlebih dahulu.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Dim result = Await gateway.RpcAsync(Of PickupDetails)("get_pickup_details", New With {.p_rental_id = rentalId}, token, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.Id <> rentalId OrElse result.Lines Is Nothing OrElse result.Lines.Count = 0 OrElse result.Units Is Nothing OrElse result.RentBalance < 0 OrElse result.DepositBalance < 0 Then Throw New InvalidDataException("Rincian pengambilan tidak lengkap.")
            Return result
        End Function
        Public Async Function PickupReservationAsync(requestId As Guid, rentalId As Long, unitIds As List(Of Long), method As String, reference As String, ct As CancellationToken) As Task(Of PickupReceipt) Implements IRentalRepository.PickupReservationAsync
            If requestId = Guid.Empty OrElse rentalId <= 0 OrElse unitIds Is Nothing OrElse unitIds.Count = 0 OrElse unitIds.Any(Function(x) x <= 0) OrElse unitIds.Distinct().Count() <> unitIds.Count Then Throw New ArgumentException("Pilih unit fisik yang berbeda sesuai jumlah reservasi.")
            If Not {"cash", "transfer"}.Contains(method) Then Throw New ArgumentException("Pilih metode pembayaran.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            ct.ThrowIfCancellationRequested()
            Try
                Dim payload = Await gateway.RpcAsync(Of JsonElement)("pickup_reservation", New With {.p_request_id = requestId, .p_rental_id = rentalId, .p_unit_ids = unitIds.ToArray(), .p_method = method, .p_reference = reference}, token, ct).ConfigureAwait(False)
                If payload.ValueKind = JsonValueKind.Array Then
                    If payload.GetArrayLength() <> 1 Then Throw New InvalidDataException("Bukti pengambilan tidak sesuai.")
                    payload = payload(0)
                End If
                If payload.ValueKind <> JsonValueKind.Object Then Throw New InvalidDataException("Bukti pengambilan tidak sesuai.")
                Dim receipt = payload.Deserialize(Of PickupReceipt)()
                If receipt Is Nothing OrElse receipt.Id <> rentalId OrElse String.IsNullOrWhiteSpace(receipt.Number) OrElse Not {"active", "returned"}.Contains(receipt.State) Then Throw New InvalidDataException("Bukti pengambilan tidak lengkap.")
                Return receipt
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New PickupSaveUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New PickupSaveUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
