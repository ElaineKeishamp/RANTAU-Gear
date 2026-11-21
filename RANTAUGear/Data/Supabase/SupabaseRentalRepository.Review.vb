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
    Public Class StaffWriteUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil perubahan akun belum terkonfirmasi. Klik Periksa simpan dengan data yang sama. Setelah menutup form, muat ulang daftar akun sebelum mengulangi tindakan.", inner)
        End Sub
    End Class
    Partial Public NotInheritable Class SupabaseRentalRepository
        Private Async Function RequireAdminAsync(ct As CancellationToken) As Task
            Dim user = Await GetCurrentUserAsync(ct).ConfigureAwait(False)
            If user.Role <> UserRole.SuperAdmin Then Throw New UnauthorizedAccessException("Pengelolaan akun hanya untuk Super Admin.")
        End Function
        Private Async Function ReadRpcAsync(Of T)(name As String, parameters As Object, ct As CancellationToken) As Task(Of T)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            Try
                Return Await gateway.RpcAsync(Of T)(name, parameters, token, ct).ConfigureAwait(False)
            Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.Unauthorized
            End Try
            token = Await sessions.GetAccessTokenAsync(ct, token).ConfigureAwait(False)
            Try
                Return Await gateway.RpcAsync(Of T)(name, parameters, token, ct).ConfigureAwait(False)
            Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.Unauthorized
                Throw New UnauthorizedAccessException("Sesi berakhir. Silakan masuk kembali.")
            End Try
        End Function
        Public Async Function ReadReportAsync(query As ReportQuery, offset As Integer, pageSize As Integer, ct As CancellationToken) As Task(Of ReportSnapshot) Implements IRentalRepository.ReadReportAsync
            query.Validate()
            If offset < 0 OrElse pageSize < 1 OrElse pageSize > 10000 Then Throw New ArgumentException("Halaman laporan tidak valid.")
            Await RequireOfficeAsync(ct).ConfigureAwait(False)
            Dim result = Await ReadRpcAsync(Of ReportSnapshot)("read_report", New With {.p_kind = query.Kind, .p_from = query.FromDate.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture), .p_to = query.ToDate.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture), .p_status = query.Status, .p_offset = offset, .p_limit = pageSize}, ct).ConfigureAwait(False)
            Dim keys = If(query.Kind = "cash", New String() {"rental_receipts", "deposit_received", "deposit_refund", "rent_refund", "cash_net", "deposit_applied", "opening_deposit", "closing_deposit"}, New String() {"total", "reserved", "active", "late", "returned", "cancelled", "expired"})
            If result Is Nothing OrElse result.Kind <> query.Kind OrElse result.Rows Is Nothing OrElse result.Summary Is Nothing OrElse result.Total < result.Rows.Count OrElse result.Rows.Count > pageSize OrElse keys.Any(Function(key) Not result.Summary.ContainsKey(key)) Then Throw New InvalidDataException("Laporan belum dapat dibaca lengkap.")
            Return result
        End Function
        Public Async Function ReadStaffAsync(offset As Integer, pageSize As Integer, ct As CancellationToken) As Task(Of StaffPageResult) Implements IRentalRepository.ReadStaffAsync
            Await RequireAdminAsync(ct).ConfigureAwait(False)
            Dim result = Await ReadRpcAsync(Of StaffPageResult)("list_staff", New With {.p_offset = offset, .p_limit = pageSize}, ct).ConfigureAwait(False)
            If result Is Nothing OrElse result.Rows Is Nothing OrElse result.Total < result.Rows.Count OrElse result.Rows.Any(Function(row) row.Id = Guid.Empty OrElse row.RowVersion < 1) Then Throw New InvalidDataException("Daftar akun belum lengkap.")
            Return result
        End Function
        Public Async Function SaveStaffAsync(requestId As Guid, operation As String, record As StaffRecord, password As String, ct As CancellationToken) As Task(Of Guid) Implements IRentalRepository.SaveStaffAsync
            record.Validate(operation)
            If requestId = Guid.Empty Then Throw New ArgumentException("ID tindakan tidak valid.")
            If operation <> "update" AndAlso (password Is Nothing OrElse password.Length < 8 OrElse password.Length > 128) Then Throw New ArgumentException("Password harus berisi 8 sampai 128 karakter.")
            Await RequireAdminAsync(ct).ConfigureAwait(False)
            Dim token = Await sessions.GetAccessTokenAsync(ct).ConfigureAwait(False)
            ct.ThrowIfCancellationRequested()
            Try
                Dim id As Guid
                If operation = "update" Then
                    id = Await gateway.RpcAsync(Of Guid)("update_staff", New With {.p_request_id = requestId, .p_target_id = record.Id, .p_version = record.RowVersion, .p_name = record.DisplayName.Trim(), .p_role = record.Role, .p_active = record.IsActive}, token, ct).ConfigureAwait(False)
                Else
                    id = Await gateway.StaffAdminAsync(New With {.request_id = requestId, .action = operation, .target_id = If(operation = "password", CType(record.Id, Guid?), Nothing), .email = record.Email.Trim().ToLowerInvariant(), .display_name = record.DisplayName.Trim(), .role = record.Role, .password = password}, token, ct).ConfigureAwait(False)
                End If
                If id = Guid.Empty Then Throw New InvalidDataException("Identitas akun belum terkonfirmasi.")
                Return id
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException OrElse TypeOf ex Is FormatException
                Throw New StaffWriteUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New StaffWriteUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
