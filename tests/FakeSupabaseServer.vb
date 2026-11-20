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

    Private Shared Function Reply(status As HttpStatusCode, value As Object, Optional total As Long? = Nothing, Optional offset As Integer = 0, Optional count As Integer = 0) As HttpResponseMessage
        Dim response As New HttpResponseMessage(status) With {.Content = New StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")}
        If total.HasValue Then response.Content.Headers.TryAddWithoutValidation("Content-Range", If(count = 0, "*", offset.ToString() & "-" & (offset + count - 1).ToString()) & "/" & total.Value.ToString())
        Return response
    End Function

    Private Function SessionReply() As HttpResponseMessage
        Return Reply(HttpStatusCode.OK, New With {.access_token = access, .refresh_token = "REFRESH-" & (RefreshCount + 1).ToString(), .expires_in = 3600})
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
                Case "/rest/v1/equipment_units"
                    rows.Add(New With {.code = "TND-TEST", .state = "ready", .equipment_types = New With {.name = "reserved", .daily_rate = 100000D}})
                Case "/rest/v1/packages"
                    rows.Add(New With {.name = "Paket uji", .daily_price = 80000D, .is_active = True})
                Case "/rest/v1/rental_overview"
                    For id = 1 To 61
                        rows.Add(New With {.id = id, .rental_number = "UJI-" & id.ToString("D3"), .pickup_at = "2026-11-21T03:00:00Z", .due_at = "2026-11-22T03:00:00Z", .display_state = "reserved", .rent_amount = 100000D, .deposit_due = 100000D})
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
