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
    Public Class BookingChangeUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil tindakan belum terkonfirmasi. Klik Periksa simpan dengan permintaan yang sama. Jangan menerima pembayaran lagi. Jika menutup form, muat ulang Booking sebelum tindakan baru.", inner)
        End Sub
    End Class
    Partial Public NotInheritable Class SupabaseRentalRepository
        Public Async Function GetBookingChangeDetailsAsync(rentalId As Long, ct As CancellationToken) As Task(Of BookingChangeDetails) Implements IRentalRepository.GetBookingChangeDetailsAsync
            If rentalId <= 0 Then Throw New ArgumentException("Pilih sewa terlebih dahulu.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Dim result = Await gateway.RpcAsync(Of BookingChangeDetails)("get_booking_change_details", New With {.p_rental_id = rentalId}, token, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.Id <> rentalId OrElse String.IsNullOrWhiteSpace(result.Number) OrElse {result.RentAmount, result.DailyPrice, result.DpReceived, result.DepositReceived}.Any(Function(x) x < 0) Then Throw New InvalidDataException("Rincian sewa tidak lengkap.")
            Return result
        End Function
        Public Async Function PreviewExtensionAsync(rentalId As Long, dueAt As DateTimeOffset, ct As CancellationToken) As Task(Of ExtensionQuote) Implements IRentalRepository.PreviewExtensionAsync
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Dim result = Await gateway.RpcAsync(Of ExtensionQuote)("preview_extension", New With {.p_rental_id = rentalId, .p_due_at = dueAt}, token, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.NewDue <= result.OldDue OrElse result.Days < 1 OrElse {result.RentAmount, result.Additional, result.Deposit}.Any(Function(x) x < 0 OrElse x <> Decimal.Truncate(x)) Then Throw New InvalidDataException("Perhitungan perpanjangan tidak lengkap.")
            Return result
        End Function
        Public Async Function ChangeBookingAsync(requestId As Guid, rentalId As Long, action As BookingChangeAction, quote As ExtensionQuote, reason As String, method As String, reference As String, ct As CancellationToken) As Task(Of PickupReceipt) Implements IRentalRepository.ChangeBookingAsync
            If requestId = Guid.Empty OrElse rentalId <= 0 OrElse Not [Enum].IsDefined(action) Then Throw New ArgumentException("Identitas tindakan tidak valid.")
            If action = BookingChangeAction.Extend AndAlso (quote Is Nothing OrElse Not {"cash", "transfer"}.Contains(method)) Then Throw New ArgumentException("Hitung biaya perpanjangan dan pilih metode pembayaran.")
            If action = BookingChangeAction.Cancel AndAlso String.IsNullOrWhiteSpace(reason) Then Throw New ArgumentException("Alasan pembatalan wajib diisi.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            ct.ThrowIfCancellationRequested()
            Try
                Dim payload = Await gateway.RpcAsync(Of JsonElement)("change_booking", New With {.p_request_id = requestId, .p_rental_id = rentalId, .p_action = action.ToString().ToLowerInvariant(), .p_due_at = If(quote Is Nothing, CType(Nothing, DateTimeOffset?), quote.NewDue), .p_reason = If(reason, ""), .p_expected_due = If(quote Is Nothing, CType(Nothing, DateTimeOffset?), quote.OldDue), .p_expected_additional = If(quote Is Nothing, CType(Nothing, Decimal?), quote.Additional), .p_method = If(action = BookingChangeAction.Extend, method, "cash"), .p_reference = If(reference, "")}, token, ct).ConfigureAwait(False)
                If payload.ValueKind = JsonValueKind.Array Then
                    If payload.GetArrayLength() <> 1 Then Throw New InvalidDataException("Bukti tindakan tidak sesuai.")
                    payload = payload(0)
                End If
                If payload.ValueKind <> JsonValueKind.Object Then Throw New InvalidDataException("Bukti tindakan tidak sesuai.")
                Dim receipt = payload.Deserialize(Of PickupReceipt)()
                Dim expected = If(action = BookingChangeAction.Extend, "active", If(action = BookingChangeAction.Cancel, "cancelled", "expired"))
                If receipt Is Nothing OrElse receipt.Id <> rentalId OrElse String.IsNullOrWhiteSpace(receipt.Number) OrElse (receipt.State <> expected AndAlso receipt.State <> "returned") Then Throw New InvalidDataException("Bukti perubahan sewa tidak lengkap.")
                Return receipt
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New BookingChangeUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New BookingChangeUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
