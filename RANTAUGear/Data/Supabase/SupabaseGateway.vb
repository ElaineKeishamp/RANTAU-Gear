Option Strict On
Option Explicit On
Option Infer On

Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Namespace Data.Supabase
    Public NotInheritable Class SupabaseSession
        <JsonPropertyName("access_token")>
        Public Property AccessToken As String = String.Empty
        <JsonPropertyName("refresh_token")>
        Public Property RefreshToken As String = String.Empty
        <JsonPropertyName("expires_in")>
        Public Property ExpiresIn As Integer
    End Class

    Public NotInheritable Class SupabaseRequestException
        Inherits Exception
        Public ReadOnly Property StatusCode As HttpStatusCode

        Public Sub New(statusCode As HttpStatusCode, message As String)
            MyBase.New(message)
            Me.StatusCode = statusCode
        End Sub
    End Class

    ' Transport only: the demo repository is never silently replaced by this client.
    Public NotInheritable Class SupabaseGateway
        Implements IDisposable

        Private ReadOnly options As SupabaseOptions
        Private ReadOnly client As HttpClient
        Private ReadOnly ownsClient As Boolean
        Private Shared ReadOnly JsonSettings As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

        Public Sub New(options As SupabaseOptions, Optional client As HttpClient = Nothing)
            If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
            Me.options = options
            ownsClient = client Is Nothing
            Me.client = If(client, New HttpClient(New HttpClientHandler With {.AllowAutoRedirect = False}) With {.Timeout = TimeSpan.FromSeconds(30)})
        End Sub

        Public Async Function SignInAsync(email As String, password As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of SupabaseSession)
            If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrEmpty(password) Then
                Throw New ArgumentException("Email dan password wajib diisi.")
            End If
            Return Await SendAsync(Of SupabaseSession)("auth/v1/token?grant_type=password",
                New With {.email = email.Trim(), .password = password}, Nothing, cancellationToken).ConfigureAwait(False)
        End Function

        Public Async Function RefreshSessionAsync(refreshToken As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of SupabaseSession)
            If String.IsNullOrWhiteSpace(refreshToken) Then Throw New ArgumentException("Refresh token tidak tersedia.", NameOf(refreshToken))
            Return Await SendAsync(Of SupabaseSession)("auth/v1/token?grant_type=refresh_token",
                New With {.refresh_token = refreshToken}, Nothing, cancellationToken).ConfigureAwait(False)
        End Function

        Public Async Function RpcAsync(Of T)(functionName As String, parameters As Object, accessToken As String,
            Optional cancellationToken As CancellationToken = Nothing) As Task(Of T)
            If String.IsNullOrEmpty(functionName) OrElse Not Regex.IsMatch(functionName, "\A[a-z_][a-z0-9_]*\z", RegexOptions.CultureInvariant) Then
                Throw New ArgumentException("Nama fungsi RPC tidak valid.", NameOf(functionName))
            End If
            If String.IsNullOrWhiteSpace(accessToken) Then Throw New InvalidOperationException("Login diperlukan sebelum mengakses data online.")
            Return Await SendAsync(Of T)("rest/v1/rpc/" & functionName, parameters, accessToken, cancellationToken).ConfigureAwait(False)
        End Function

        Public Async Function SignOutAsync(accessToken As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If String.IsNullOrWhiteSpace(accessToken) Then Throw New ArgumentException("Access token tidak tersedia.", NameOf(accessToken))
            Await SendAsync(Of JsonElement)("auth/v1/logout?scope=local", New With {.unused = True}, accessToken, cancellationToken).ConfigureAwait(False)
        End Function

        Private Async Function SendAsync(Of T)(relativePath As String, body As Object, accessToken As String,
            cancellationToken As CancellationToken) As Task(Of T)
            Using request As New HttpRequestMessage(HttpMethod.Post, New Uri(options.ProjectUrl, relativePath))
                request.Headers.Add("apikey", options.PublishableKey)
                request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                If accessToken IsNot Nothing Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)
                request.Content = New StringContent(JsonSerializer.Serialize(body, JsonSettings), Encoding.UTF8, "application/json")
                Using response = Await client.SendAsync(request, cancellationToken).ConfigureAwait(False)
                    If Not response.IsSuccessStatusCode Then
                        Dim message As String
                        Select Case response.StatusCode
                            Case HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity
                                message = "Permintaan ditolak. Periksa input dan aturan transaksi."
                            Case HttpStatusCode.Unauthorized
                                message = "Login gagal atau sesi telah berakhir."
                            Case HttpStatusCode.Forbidden
                                message = "Akun tidak memiliki izin untuk tindakan ini."
                            Case HttpStatusCode.Conflict
                                message = "Data berubah atau bertabrakan. Muat ulang sebelum mencoba kembali."
                            Case HttpStatusCode.TooManyRequests
                                message = "Terlalu banyak permintaan. Tunggu sebelum mencoba kembali."
                            Case Else
                                message = "Layanan Supabase tidak dapat menyelesaikan permintaan."
                        End Select
                        ' Never surface raw server bodies: they can contain submitted data or tokens.
                        Throw New SupabaseRequestException(response.StatusCode, message)
                    End If
                    If response.StatusCode = HttpStatusCode.NoContent Then Return Nothing
                    Dim content = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                    If String.IsNullOrWhiteSpace(content) Then Return Nothing
                    Return JsonSerializer.Deserialize(Of T)(content, JsonSettings)
                End Using
            End Using
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If ownsClient Then client.Dispose()
        End Sub
    End Class
End Namespace
