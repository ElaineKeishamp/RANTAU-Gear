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
    Public NotInheritable Class ReservationSaveUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil simpan belum dapat dikonfirmasi. Klik Periksa simpan untuk mengulang permintaan yang sama tanpa menggandakan reservasi atau DP. Jika menutup form, periksa daftar sebelum membuat reservasi ulang.", inner)
        End Sub
    End Class
    Partial Public NotInheritable Class SupabaseRentalRepository
        Private Async Function RequireOfficeAsync(ct As CancellationToken) As Task
            Dim user = Await GetCurrentUserAsync(ct).ConfigureAwait(False)
            If user.Role <> UserRole.SuperAdmin AndAlso user.Role <> UserRole.FrontOffice Then Throw New UnauthorizedAccessException("Hanya admin atau front office yang dapat mencatat reservasi dan DP.")
        End Function
        Public Async Function GetReservationOptionsAsync(ct As CancellationToken) As Task(Of ReservationOptions) Implements IRentalRepository.GetReservationOptionsAsync
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim result As New ReservationOptions()
            For Each kind In {MasterDataKind.Customer, MasterDataKind.EquipmentType, MasterDataKind.Package}
                Dim selection = If(kind = MasterDataKind.Customer, "id,name,phone,is_active", If(kind = MasterDataKind.EquipmentType, "id,name,daily_rate,is_active", "id,name,daily_price,is_active,package_components(equipment_type_id,quantity,is_active)"))
                Dim offset As Integer = 0
                Do
                    Dim page = Await ReadAsync(MasterResource(kind), selection, "id.asc", offset, 100, ct).ConfigureAwait(False)
                    If Not page.Total.HasValue Then Throw New InvalidDataException("Jumlah pilihan reservasi belum dapat dibaca.")
                    For Each row In page.Records
                        If Not row.GetProperty("is_active").GetBoolean() Then Continue For
                        Dim item As New MasterRecord With {.Kind = kind, .Id = row.GetProperty("id").GetInt64(), .Name = row.GetProperty("name").GetString()}
                        Select Case kind
                            Case MasterDataKind.Customer
                                item.Phone = row.GetProperty("phone").GetString()
                                result.Customers.Add(item)
                            Case MasterDataKind.EquipmentType
                                item.DailyRate = row.GetProperty("daily_rate").GetDecimal()
                                result.Types.Add(item)
                            Case MasterDataKind.Package
                                item.DailyRate = row.GetProperty("daily_price").GetDecimal()
                                For Each component In row.GetProperty("package_components").EnumerateArray()
                                    If component.GetProperty("is_active").GetBoolean() Then item.Components.Add(New MasterPackageItem With {.TypeId = component.GetProperty("equipment_type_id").GetInt64(), .Quantity = component.GetProperty("quantity").GetInt32()})
                                Next
                                result.Packages.Add(item)
                        End Select
                    Next
                    offset += page.Records.Count
                    If offset >= page.Total.Value OrElse page.Records.Count = 0 Then Exit Do
                Loop
            Next
            Return result
        End Function
        Private Shared Function LineValues(request As ReservationRequest) As Object
            Return request.Lines.Select(Function(x) New With {.type_id = x.TypeId, .quantity = x.Quantity}).ToArray()
        End Function
        Public Async Function PreviewReservationAsync(request As ReservationRequest, ct As CancellationToken) As Task(Of ReservationQuote) Implements IRentalRepository.PreviewReservationAsync
            request.Validate()
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Dim result = Await gateway.RpcAsync(Of ReservationQuote)("preview_reservation", New With {.p_pickup_at = request.PickupAt, .p_due_at = request.DueAt, .p_lines = LineValues(request), .p_package_id = request.PackageId}, token, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.RentalDays < 1 OrElse result.RentAmount <= 0 OrElse result.DownPayment < 0 OrElse result.Deposit < 0 Then Throw New InvalidDataException("Ringkasan biaya belum dapat dibaca.")
            Return result
        End Function
        Public Async Function ReserveWithDpAsync(requestId As Guid, request As ReservationRequest, quote As ReservationQuote, ct As CancellationToken) As Task(Of ReservationReceipt) Implements IRentalRepository.ReserveWithDpAsync
            request.Validate()
            If requestId = Guid.Empty OrElse quote Is Nothing Then Throw New ArgumentException("Hitung biaya sebelum mencatat DP.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            ct.ThrowIfCancellationRequested()
            Try
                Dim payload = Await gateway.RpcAsync(Of JsonElement)("reserve_with_dp", New With {.p_request_id = requestId, .p_customer_id = request.CustomerId, .p_pickup_at = request.PickupAt, .p_due_at = request.DueAt, .p_lines = LineValues(request), .p_method = request.Method, .p_reference = request.Reference, .p_package_id = request.PackageId, .p_expected_rent = quote.RentAmount, .p_expected_dp = quote.DownPayment, .p_expected_deposit = quote.Deposit}, token, ct).ConfigureAwait(False)
                ' Composite RPC results normally arrive as a one-row array from PostgREST.
                If payload.ValueKind = JsonValueKind.Array Then
                    If payload.GetArrayLength() <> 1 Then Throw New InvalidDataException("Jumlah bukti reservasi tidak sesuai.")
                    payload = payload(0)
                End If
                If payload.ValueKind <> JsonValueKind.Object Then Throw New InvalidDataException("Format bukti reservasi tidak sesuai.")
                Dim receipt = payload.Deserialize(Of ReservationReceipt)()
                If receipt Is Nothing OrElse receipt.Id <= 0 OrElse String.IsNullOrWhiteSpace(receipt.Number) Then Throw New InvalidDataException("Bukti reservasi tidak lengkap.")
                Return receipt
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                Throw New ReservationSaveUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New ReservationSaveUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
