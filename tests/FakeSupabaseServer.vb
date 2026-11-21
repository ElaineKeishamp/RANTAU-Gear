Option Strict On
Option Explicit On
Option Infer On

Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Friend NotInheritable Class FakeSupabaseServer
    Inherits HttpMessageHandler

    Public Shared ReadOnly StaffId As Guid = Guid.Parse("11111111-1111-1111-1111-111111111111")
    Public Property StaffRole As String = "super_admin"
    Public Property StaffName As String = "Administrator uji"
    Public Property Active As Boolean = True
    Public Property HasProfile As Boolean = True
    Public Property Offline As Boolean
    Public Property DelayReads As Boolean
    Public Property InvalidRefresh As Boolean
    Public Property RejectReadOnce As Boolean
    Public Property RejectEveryRead As Boolean
    Public Property EmptyData As Boolean
    Public Property LastReadPath As String = String.Empty
    Public Property RefreshCount As Integer
    Public Property LogoutCount As Integer
    Private access As String = "ACCESS-1"
    Public Property DropSaveReplyOnce As Boolean
    Public Property ConflictNextSave As Boolean
    Public Property SaveCount As Integer
    Public ReadOnly SaveRequestIds As New List(Of Guid)()
    Private ReadOnly masters As New Dictionary(Of String, List(Of Dictionary(Of String, Object)))()
    Private ReadOnly savedRequests As New Dictionary(Of Guid, (Body As String, Id As Long))()
    Private nextId As Long = 100
    Public Sub New()
        masters("categories") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 10L}, {"row_version", 1L}, {"name", "Kategori uji"}, {"is_active", True}}}
        masters("equipment_types") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 20L}, {"row_version", 1L}, {"category_id", 10L}, {"name", "reserved"}, {"daily_rate", 100000D}, {"replacement_value", 1000000D}, {"description", ""}, {"is_active", True}, {"categories", New With {.name = "Kategori uji"}}}}
        masters("equipment_units") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 30L}, {"row_version", 1L}, {"equipment_type_id", 20L}, {"code", "TND-TEST"}, {"state", "ready"}, {"notes", ""}, {"equipment_types", New With {.name = "reserved", .daily_rate = 100000D}}}}
        masters("packages") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 40L}, {"row_version", 1L}, {"name", "Paket uji"}, {"daily_price", 80000D}, {"is_active", True}, {"package_components", New Object() {New With {.equipment_type_id = 20L, .quantity = 1, .is_active = True}}}}}
        masters("customers") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 50L}, {"row_version", 1L}, {"name", "Pelanggan uji"}, {"phone", "080000"}, {"address", "Alamat uji"}, {"is_active", True}}}
    End Sub
    Public Function MasterCount(entity As String) As Integer
        Return masters(entity).Count
    End Function
    Private Async Function SaveMaster(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        SaveCount += 1
        Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
            Dim body = document.RootElement
            Dim entity = body.GetProperty("p_entity").GetString()
            If StaffRole <> "super_admin" AndAlso Not (StaffRole = "front_office" AndAlso entity = "customers") Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
            Dim requestId = body.GetProperty("p_request_id").GetGuid()
            SaveRequestIds.Add(requestId)
            If savedRequests.ContainsKey(requestId) Then
                If savedRequests(requestId).Body <> body.GetRawText() Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_REQUEST"})
                Return Reply(HttpStatusCode.OK, savedRequests(requestId).Id)
            End If
            If ConflictNextSave Then
                ConflictNextSave = False
                Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_VERSION"})
            End If
            Dim row As Dictionary(Of String, Object)
            If body.GetProperty("p_id").ValueKind = JsonValueKind.Null Then
                nextId += 1
                row = New Dictionary(Of String, Object) From {{"id", nextId}, {"row_version", 1L}}
                masters(entity).Add(row)
            Else
                Dim id = body.GetProperty("p_id").GetInt64()
                row = masters(entity).Single(Function(item) CLng(item("id")) = id)
                If CLng(row("row_version")) <> body.GetProperty("p_version").GetInt64() Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_VERSION"})
                row("row_version") = CLng(row("row_version")) + 1
            End If
            For Each field In body.GetProperty("p_values").EnumerateObject()
                row(field.Name) = field.Value.Clone()
            Next
            If entity = "equipment_types" Then row("categories") = New With {.name = "Kategori uji"}
            If entity = "equipment_units" Then row("equipment_types") = New With {.name = "reserved", .daily_rate = 100000D}
            If entity = "packages" Then row("package_components") = body.GetProperty("p_values").GetProperty("components").EnumerateArray().Select(Function(item) New With {.equipment_type_id = item.GetProperty("type_id").GetInt64(), .quantity = item.GetProperty("quantity").GetInt32(), .is_active = True}).ToArray()
            Dim result = CLng(row("id"))
            savedRequests(requestId) = (body.GetRawText(), result)
            If DropSaveReplyOnce Then
                DropSaveReplyOnce = False
                Throw New HttpRequestException("Simulated lost reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, result)
        End Using
    End Function

    Private Shared Function Reply(status As HttpStatusCode, value As Object, Optional total As Long? = Nothing, Optional offset As Integer = 0, Optional count As Integer = 0) As HttpResponseMessage
        Dim response As New HttpResponseMessage(status) With {.Content = New StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")}
        If total.HasValue Then response.Content.Headers.TryAddWithoutValidation("Content-Range", If(count = 0, "*", offset.ToString() & "-" & (offset + count - 1).ToString()) & "/" & total.Value.ToString())
        Return response
    End Function

    Private Function SessionReply() As HttpResponseMessage
        Return Reply(HttpStatusCode.OK, New With {.access_token = access, .refresh_token = "REFRESH-" & (RefreshCount + 1).ToString(), .expires_in = 3600})
    End Function


    Public Property ObjectReservationReply As Boolean
    Public Property ReservationCount As Integer
    Public Property DpCount As Integer
    Public Property DropReservationReplyOnce As Boolean
    Public Property RejectBookingStock As Boolean
    Public Property ChangeBookingPrice As Boolean
    Public ReadOnly ReservationIds As New List(Of String)()
    Private ReadOnly reservationReplies As New Dictionary(Of String, (Body As String, Receipt As Object))()
    Private Async Function BookingRpc(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
        If StaffRole <> "super_admin" AndAlso StaffRole <> "front_office" Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
        Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(ct).ConfigureAwait(False))
            Dim body = document.RootElement
            If request.RequestUri.AbsolutePath.EndsWith("preview_reservation", StringComparison.Ordinal) Then
                If RejectBookingStock Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_STOCK"})
                Dim days = Math.Max(1, CInt(Math.Ceiling((DateTimeOffset.Parse(body.GetProperty("p_due_at").GetString()) - DateTimeOffset.Parse(body.GetProperty("p_pickup_at").GetString())).TotalDays)))
                Dim daily = If(body.GetProperty("p_package_id").ValueKind = JsonValueKind.Null, body.GetProperty("p_lines").EnumerateArray().Sum(Function(x) x.GetProperty("quantity").GetInt32()) * 100000D, 50000D)
                Return Reply(HttpStatusCode.OK, New With {.daily_price = daily, .rental_days = days, .rent_amount = daily * days, .down_payment_due = Math.Ceiling(daily * days / 4D), .deposit_due = daily * days, .pickup_deadline_at = DateTimeOffset.Parse(body.GetProperty("p_pickup_at").GetString()).AddHours(12)})
            End If
            Dim id = body.GetProperty("p_request_id").GetString()
            ReservationIds.Add(id)
            If reservationReplies.ContainsKey(id) Then
                If reservationReplies(id).Body <> body.GetRawText() Then Throw New InvalidOperationException("Retry changed payment payload")
                Return Reply(HttpStatusCode.OK, reservationReplies(id).Receipt)
            End If
            If RejectBookingStock Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_STOCK"})
            If ChangeBookingPrice Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_CHANGED"})
            ReservationCount += 1 : DpCount += 1
            Dim receipt = New With {.id = 900L + ReservationCount, .rental_number = "UJI-RESERVASI-" & ReservationCount.ToString(), .rent_amount = body.GetProperty("p_expected_rent").GetDecimal(), .down_payment_due = body.GetProperty("p_expected_dp").GetDecimal(), .deposit_due = body.GetProperty("p_expected_deposit").GetDecimal()}
            Dim payload As Object = If(ObjectReservationReply, DirectCast(receipt, Object), New Object() {receipt})
            reservationReplies(id) = (body.GetRawText(), payload)
            If DropReservationReplyOnce Then
                DropReservationReplyOnce = False
                Throw New HttpRequestException("Lost reservation reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, payload)
        End Using
    End Function

    Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        If Offline Then Throw New HttpRequestException("Simulated offline fixture")
        Dim path = request.RequestUri.AbsolutePath
        If path = "/auth/v1/token" Then
            If request.RequestUri.Query.Contains("refresh_token") Then
                RefreshCount += 1
                Await Task.Delay(40, cancellationToken).ConfigureAwait(False)
                If InvalidRefresh Then Return Reply(HttpStatusCode.BadRequest, New With {.error = "refresh_expired"})
                access = "ACCESS-" & (RefreshCount + 1).ToString()
                Return SessionReply()
            End If
            Using body = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
                If body.RootElement.GetProperty("email").GetString() <> "staff@example.test" OrElse body.RootElement.GetProperty("password").GetString() <> "TEST_PASSWORD" Then Return Reply(HttpStatusCode.BadRequest, New With {.error = "invalid_credentials"})
            End Using
            Return SessionReply()
        End If
        If request.Headers.Authorization Is Nothing OrElse request.Headers.Authorization.Parameter <> access Then Return Reply(HttpStatusCode.Unauthorized, New With {.error = "expired"})
        If path = "/auth/v1/user" Then Return Reply(HttpStatusCode.OK, New With {.id = StaffId})
        If path = "/auth/v1/logout" Then
            LogoutCount += 1
            access = String.Empty
            Return New HttpResponseMessage(HttpStatusCode.NoContent)
        End If
        If path = "/rest/v1/rpc/preview_reservation" OrElse path = "/rest/v1/rpc/reserve_with_dp" Then Return Await BookingRpc(request, cancellationToken).ConfigureAwait(False)
        If path = "/rest/v1/rpc/save_master_data" Then Return Await SaveMaster(request, cancellationToken).ConfigureAwait(False)
        If path.StartsWith("/rest/v1/", StringComparison.Ordinal) Then
            If DelayReads Then Await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(False)
            LastReadPath = request.RequestUri.PathAndQuery
            If RejectReadOnce OrElse RejectEveryRead Then
                RejectReadOnce = False
                Return Reply(HttpStatusCode.Unauthorized, New With {.error = "expired"})
            End If
            Dim query = request.RequestUri.Query.TrimStart("?"c).Split("&"c).Select(Function(part) part.Split("="c, 2)).ToDictionary(Function(parts) parts(0), Function(parts) Uri.UnescapeDataString(parts(1)))
            Dim offset = Integer.Parse(query("offset"), Globalization.CultureInfo.InvariantCulture)
            Dim limit = Integer.Parse(query("limit"), Globalization.CultureInfo.InvariantCulture)
            Dim rows As New List(Of Object)()
            Select Case path
                Case "/rest/v1/staff_profiles"
                    If HasProfile Then rows.Add(New With {.id = StaffId, .display_name = StaffName, .role = StaffRole, .is_active = Active})
                Case "/rest/v1/categories", "/rest/v1/equipment_types", "/rest/v1/equipment_units", "/rest/v1/packages", "/rest/v1/customers"
                    rows.AddRange(masters(path.Substring("/rest/v1/".Length)).Cast(Of Object)())
                Case "/rest/v1/rental_overview", "/rest/v1/reservation_register"
                    For id = 1 To 61
                        rows.Add(New With {.id = id, .rental_number = "UJI-" & id.ToString("D3"), .pickup_at = "2026-11-21T03:00:00Z", .due_at = "2026-11-22T03:00:00Z", .display_state = "reserved", .rent_amount = 100000D, .deposit_due = 100000D, .customer_name = "Pelanggan uji", .dp_received = 25000D})
                    Next
                Case Else
                    Throw New InvalidOperationException("Unexpected fake resource: " & path)
            End Select
            If EmptyData AndAlso path <> "/rest/v1/staff_profiles" Then rows.Clear()
            Dim page = rows.Skip(offset).Take(limit).ToArray()
            Return Reply(HttpStatusCode.OK, page, rows.Count, offset, page.Length)
        End If
        Throw New InvalidOperationException("Unexpected fake endpoint: " & path)
    End Function
End Class
