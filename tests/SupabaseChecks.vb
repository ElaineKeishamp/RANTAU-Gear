Option Strict On
Option Explicit On
Option Infer On

Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Data.Supabase

Module SupabaseChecks
    Private NotInheritable Class StubHandler
        Inherits HttpMessageHandler
        Public Property Respond As Func(Of HttpRequestMessage, CancellationToken, Task(Of HttpResponseMessage))
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Respond(request, cancellationToken)
        End Function
    End Class

    Public Async Function RunAsync() As Task(Of Integer)
        Dim passed As Integer
        Dim verify As Action(Of Boolean, String) = Sub(ok, label)
                                                      If Not ok Then Throw New Exception("FAIL: " & label)
                                                      passed += 1
                                                      Console.WriteLine("PASS: " & label)
                                                  End Sub
        Try
            Dim invalid = New SupabaseOptions("https://project.supabase.co", "sb_secret_TEST_ONLY")
            Throw New Exception("Secret key was accepted")
        Catch ex As ArgumentException
            verify(True, "Desktop configuration rejects secret keys")
        End Try
        Try
            Dim invalid = New SupabaseOptions("https://user:password@project.supabase.co", "sb_publishable_TEST_ONLY")
            Throw New Exception("Credential-bearing URL was accepted")
        Catch ex As ArgumentException
            verify(True, "Project URL rejects embedded credentials")
        End Try
        Dim options As New SupabaseOptions("https://project.supabase.co", "sb_publishable_TEST_ONLY")
        Using handler As New StubHandler(), http As New HttpClient(handler), gateway As New SupabaseGateway(options, http)
            handler.Respond = Async Function(request, token)
                                  verify(request.RequestUri.AbsolutePath = "/auth/v1/token" AndAlso request.RequestUri.Query = "?grant_type=password", "Sign-in targets Supabase Auth token endpoint")
                                  verify(request.Headers.GetValues("apikey").Single() = options.PublishableKey AndAlso request.Headers.Authorization Is Nothing, "Auth sends publishable key without reusing a bearer token")
                                  Dim body = Await request.Content.ReadAsStringAsync(token)
                                  Using document = JsonDocument.Parse(body)
                                      verify(document.RootElement.GetProperty("email").GetString() = "staff@example.test", "Login trims email before sending")
                                  End Using
                                  Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("{""access_token"":""ACCESS_TEST_ONLY"",""refresh_token"":""REFRESH_TEST_ONLY"",""expires_in"":3600}", Encoding.UTF8, "application/json")}
                              End Function
            Dim session = Await gateway.SignInAsync(" staff@example.test ", "PASSWORD_TEST_ONLY")
            verify(session.AccessToken = "ACCESS_TEST_ONLY" AndAlso session.RefreshToken = "REFRESH_TEST_ONLY", "Auth snake-case fields deserialize into session")
            handler.Respond = Function(request, token)
                                  verify(request.RequestUri.AbsolutePath = "/rest/v1/rpc/check_availability", "RPC targets function endpoint")
                                  verify(request.Headers.Authorization.Scheme = "Bearer" AndAlso request.Headers.Authorization.Parameter = session.AccessToken, "RPC authenticates with user access token")
                                  Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("2")})
                              End Function
            verify(Await gateway.RpcAsync(Of Integer)("check_availability", New With {.p_type_id = 1}, session.AccessToken) = 2, "RPC result deserializes to requested type")
            Try
                Await gateway.RpcAsync(Of Integer)("check_availability", New With {.p_type_id = 1}, "")
                Throw New Exception("Anonymous RPC was accepted")
            Catch ex As InvalidOperationException
                verify(True, "RPC requires user session")
            End Try
            Try
                Await gateway.RpcAsync(Of Integer)("../auth/v1/token", New With {.unused = True}, session.AccessToken)
                Throw New Exception("Invalid RPC path was accepted")
            Catch ex As ArgumentException
                verify(True, "RPC rejects path traversal")
            End Try
            handler.Respond = Function(request, token) Task.FromResult(New HttpResponseMessage(HttpStatusCode.Unauthorized) With {.Content = New StringContent("PASSWORD_TEST_ONLY REFRESH_TEST_ONLY")})
            Try
                Await gateway.RpcAsync(Of Integer)("check_availability", New With {.p_type_id = 1}, session.AccessToken)
                Throw New Exception("Unauthorized response was accepted")
            Catch ex As SupabaseRequestException
                verify(ex.StatusCode = HttpStatusCode.Unauthorized AndAlso Not ex.Message.Contains("TEST_ONLY"), "HTTP errors do not expose raw server credentials or response bodies")
            End Try
            handler.Respond = Async Function(request, token)
                                  verify(request.RequestUri.Query = "?grant_type=refresh_token" AndAlso request.Headers.Authorization Is Nothing, "Refresh does not send stale bearer token")
                                  Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(token))
                                      verify(document.RootElement.GetProperty("refresh_token").GetString() = session.RefreshToken, "Refresh sends refresh_token field")
                                  End Using
                                  Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("{""access_token"":""NEW_ACCESS_TEST_ONLY"",""refresh_token"":""NEW_REFRESH_TEST_ONLY"",""expires_in"":3600}")}
                              End Function
            Dim refreshed = Await gateway.RefreshSessionAsync(session.RefreshToken)
            verify(refreshed.RefreshToken = "NEW_REFRESH_TEST_ONLY", "Refresh returns rotated refresh token")
            handler.Respond = Function(request, token)
                                  token.ThrowIfCancellationRequested()
                                  Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK))
                              End Function
            Using cancelled As New CancellationTokenSource()
                cancelled.Cancel()
                Try
                    Await gateway.RpcAsync(Of Integer)("check_availability", New With {.p_type_id = 1}, session.AccessToken, cancelled.Token)
                    Throw New Exception("Cancellation was ignored")
                Catch ex As OperationCanceledException
                    verify(True, "Cancellation propagates without retrying a mutation")
                End Try
            End Using
        End Using
        Return passed
    End Function
End Module
