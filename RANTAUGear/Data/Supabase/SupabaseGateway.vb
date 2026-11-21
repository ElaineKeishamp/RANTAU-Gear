Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
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

    Public NotInheritable Class SupabaseAuthUser
        <JsonPropertyName("id")>
        Public Property Id As Guid
    End Class

    Public NotInheritable Class SupabaseRows
        Public ReadOnly Property Records As IReadOnlyList(Of JsonElement)
        Public ReadOnly Property Total As Long?
        Public Sub New(records As IReadOnlyList(Of JsonElement), total As Long?)
            Me.Records = records
            Me.Total = total
        End Sub
    End Class

    Public NotInheritable Class SupabaseRequestException
        Inherits Exception
        Public ReadOnly Property StatusCode As HttpStatusCode
        Public ReadOnly Property ErrorCode As String
        Public Sub New(statusCode As HttpStatusCode, message As String, Optional errorCode As String = "")
            MyBase.New(message)
            Me.StatusCode = statusCode
            Me.ErrorCode = errorCode
        End Sub
    End Class

    Public NotInheritable Class SupabaseGateway
        Implements IDisposable

        Private ReadOnly options As SupabaseOptions
        Private ReadOnly client As HttpClient
        Private ReadOnly ownsClient As Boolean
        Private Shared ReadOnly JsonSettings As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

        Private NotInheritable Class Reply
            Public Property Content As String = String.Empty
            Public Property Total As Long?
        End Class

        Public Sub New(options As SupabaseOptions, Optional client As HttpClient = Nothing)
            If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
            Me.options = options
            ownsClient = client Is Nothing
            Me.client = If(client, New HttpClient(New HttpClientHandler With {.AllowAutoRedirect = False}) With {.Timeout = TimeSpan.FromSeconds(30)})
        End Sub

        Public Async Function SignInAsync(email As String, password As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of SupabaseSession)
            If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrEmpty(password) Then Throw New ArgumentException("Email dan password wajib diisi.")
            Dim result = Await RequestAsync(HttpMethod.Post, "auth/v1/token?grant_type=password", New With {.email = email.Trim(), .password = password}, Nothing, False, cancellationToken).ConfigureAwait(False)
            Return ParseSession(result.Content)
        End Function

        Public Async Function RefreshSessionAsync(refreshToken As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of SupabaseSession)
            If String.IsNullOrWhiteSpace(refreshToken) Then Throw New ArgumentException("Refresh token tidak tersedia.", NameOf(refreshToken))
            Dim result = Await RequestAsync(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token", New With {.refresh_token = refreshToken}, Nothing, False, cancellationToken).ConfigureAwait(False)
            Return ParseSession(result.Content)
        End Function

        Private Shared Function ParseSession(content As String) As SupabaseSession
            Dim result = JsonSerializer.Deserialize(Of SupabaseSession)(content, JsonSettings)
            If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.AccessToken) OrElse String.IsNullOrWhiteSpace(result.RefreshToken) OrElse result.ExpiresIn <= 0 Then
                Throw New InvalidDataException("Layanan mengembalikan sesi login yang tidak lengkap.")
            End If
            Return result
        End Function

        Public Async Function GetUserAsync(accessToken As String, cancellationToken As CancellationToken) As Task(Of SupabaseAuthUser)
            RequireToken(accessToken)
            Dim response = Await RequestAsync(HttpMethod.Get, "auth/v1/user", Nothing, accessToken, False, cancellationToken).ConfigureAwait(False)
            Dim user = JsonSerializer.Deserialize(Of SupabaseAuthUser)(response.Content, JsonSettings)
            If user Is Nothing OrElse user.Id = Guid.Empty Then Throw New InvalidDataException("Identitas akun dari layanan tidak valid.")
            Return user
        End Function

        Public Async Function ReadRowsAsync(resource As String, columns As String, order As String, offset As Integer, pageSize As Integer,
            accessToken As String, cancellationToken As CancellationToken, Optional identityFilter As Guid? = Nothing) As Task(Of SupabaseRows)
            ValidateName(resource)
            RequireToken(accessToken)
            If offset < 0 OrElse pageSize < 1 OrElse pageSize > 100 Then Throw New ArgumentOutOfRangeException(NameOf(pageSize))
            Dim path = "rest/v1/" & resource & "?select=" & Uri.EscapeDataString(columns) & "&order=" & Uri.EscapeDataString(order) &
                "&offset=" & offset.ToString(Globalization.CultureInfo.InvariantCulture) & "&limit=" & pageSize.ToString(Globalization.CultureInfo.InvariantCulture)
            If identityFilter.HasValue Then path &= "&id=eq." & identityFilter.Value.ToString("D")
            Dim response = Await RequestAsync(HttpMethod.Get, path, Nothing, accessToken, True, cancellationToken).ConfigureAwait(False)
            Using document = JsonDocument.Parse(response.Content)
                If document.RootElement.ValueKind <> JsonValueKind.Array Then Throw New InvalidDataException("Layanan tidak mengembalikan daftar data.")
                Dim rows = document.RootElement.EnumerateArray().Select(Function(item) item.Clone()).ToList()
                Return New SupabaseRows(rows, response.Total)
            End Using
        End Function

        Public Async Function RpcAsync(Of T)(functionName As String, parameters As Object, accessToken As String,
            Optional cancellationToken As CancellationToken = Nothing) As Task(Of T)
            ValidateName(functionName)
            RequireToken(accessToken)
            Dim response = Await RequestAsync(HttpMethod.Post, "rest/v1/rpc/" & functionName, parameters, accessToken, False, cancellationToken).ConfigureAwait(False)
            If String.IsNullOrWhiteSpace(response.Content) Then Return Nothing
            Return JsonSerializer.Deserialize(Of T)(response.Content, JsonSettings)
        End Function

        Public Async Function SignOutAsync(accessToken As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            RequireToken(accessToken)
            Await RequestAsync(HttpMethod.Post, "auth/v1/logout?scope=local", Nothing, accessToken, False, cancellationToken).ConfigureAwait(False)
        End Function

        Private Shared Sub ValidateName(name As String)
            If String.IsNullOrEmpty(name) OrElse Not Regex.IsMatch(name, "\A[a-z_][a-z0-9_]*\z", RegexOptions.CultureInvariant) Then
                Throw New ArgumentException("Nama resource Supabase tidak valid.", NameOf(name))
            End If
        End Sub

        Private Shared Sub RequireToken(token As String)
            If String.IsNullOrWhiteSpace(token) Then Throw New InvalidOperationException("Login diperlukan sebelum mengakses data online.")
        End Sub

        Private Async Function RequestAsync(method As HttpMethod, relativePath As String, body As Object, accessToken As String,
            countRows As Boolean, cancellationToken As CancellationToken) As Task(Of Reply)
            Using request As New HttpRequestMessage(method, New Uri(options.ProjectUrl, relativePath))
                request.Headers.Add("apikey", options.PublishableKey)
                request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                If countRows Then request.Headers.Add("Prefer", "count=exact")
                If accessToken IsNot Nothing Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", accessToken)
                If method <> HttpMethod.Get Then request.Content = New StringContent(JsonSerializer.Serialize(body, JsonSettings), Encoding.UTF8, "application/json")
                Using response = Await client.SendAsync(request, cancellationToken).ConfigureAwait(False)
                    If Not response.IsSuccessStatusCode Then
                        Dim message As String
                        Select Case response.StatusCode
                            Case HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity
                                message = If(relativePath.StartsWith("auth/v1/token?grant_type=password", StringComparison.Ordinal), "Email atau password tidak sesuai, atau akun belum dapat masuk.", "Permintaan ditolak. Periksa input dan aturan transaksi.")
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
                        ' Server bodies can contain submitted data; only controlled messages reach the UI.
                        Dim code As String = String.Empty
                        Try
                            Using errorData = JsonDocument.Parse(Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
                                Dim field As JsonElement
                                If errorData.RootElement.TryGetProperty("code", field) Then code = field.GetString()
                                If errorData.RootElement.TryGetProperty("message", field) Then
                                    Dim identifier = field.GetString()
                                    If identifier IsNot Nothing AndAlso (identifier.StartsWith("RG_MASTER_", StringComparison.Ordinal) OrElse identifier.StartsWith("RG_BOOKING_", StringComparison.Ordinal) OrElse identifier.StartsWith("RG_PICKUP_", StringComparison.Ordinal)) Then code = identifier
                                    Select Case identifier
                                        Case "Reservasi tidak ditemukan." : code = "RG_PICKUP_MISSING"
                                        Case "Reservasi ini tidak dapat diambil." : code = "RG_PICKUP_STATE"
                                        Case "Pengambilan harus dalam jadwal ambil, sebelum batas 12 jam, dan sebelum jadwal kembali." : code = "RG_PICKUP_WINDOW"
                                        Case "Semua unit harus siap, lolos QC, dan belum dipinjam.", "Unit masih dalam jeda pemeriksaan dua jam." : code = "RG_PICKUP_UNITS"
                                        Case "Jenis dan jumlah unit harus sama dengan reservasi.", "Pilih unit tanpa duplikasi." : code = "RG_PICKUP_QUANTITY"
                                        Case "Saldo pembayaran perlu diperiksa." : code = "RG_PICKUP_BALANCE"
                                    End Select
                                End If
                            End Using
                        Catch ex As JsonException
                            code = String.Empty
                        End Try
                        Select Case code
                            Case "RG_PICKUP_MISSING", "RG_PICKUP_STATE" : message = "Reservasi tidak ditemukan atau sudah diproses. Tutup form dan muat ulang Booking."
                            Case "RG_PICKUP_WINDOW" : message = "Pengambilan hanya dari jadwal ambil sampai batas 12 jam, serta sebelum jadwal kembali. Periksa jadwal reservasi."
                            Case "RG_PICKUP_UNITS" : message = "Unit sudah dipakai atau masih dalam pemeriksaan. Tutup form dan buka pengambilan kembali untuk memperbarui pilihan."
                            Case "RG_PICKUP_QUANTITY" : message = "Jenis dan jumlah unit yang dipilih harus sama dengan reservasi."
                            Case "RG_PICKUP_BALANCE" : message = "Saldo pembayaran perlu diperiksa admin. Pengambilan tidak disimpan."
                            Case "RG_BOOKING_INPUT" : message = "Periksa pelanggan, jadwal dan jumlah alat yang dipilih."
                            Case "RG_BOOKING_STOCK" : message = "Stok tidak cukup pada jadwal tersebut. Kurangi jumlah, ganti alat, atau ubah jadwal lalu hitung kembali."
                            Case "RG_BOOKING_CHANGED" : message = "Tarif, aturan atau isi paket berubah. Muat ulang pilihan dan hitung ulang sebelum menerima DP."
                            Case "RG_MASTER_INPUT", "23502", "23514", "22P02", "22003" : message = "Periksa kolom wajib, jumlah, tarif, dan nilai yang diisi."
                            Case "23505" : message = "Nama atau kode tersebut sudah digunakan. Pilih nama atau kode lain."
                            Case "RG_MASTER_PARENT" : message = "Pilih kategori atau jenis alat yang aktif."
                            Case "RG_MASTER_CHILDREN" : message = "Nonaktifkan jenis alat pada kategori ini terlebih dahulu."
                            Case "RG_MASTER_PACKAGE" : message = "Jenis alat masih dipakai paket aktif. Perbarui paket terlebih dahulu."
                            Case "RG_MASTER_CAPACITY" : message = "Perubahan mengurangi stok yang dibutuhkan sewa atau reservasi. Selesaikan konflik jadwal terlebih dahulu."
                            Case "RG_MASTER_HISTORY" : message = "Kode dan jenis unit yang sudah memiliki riwayat transaksi tidak dapat diganti."
                            Case "RG_MASTER_STATE" : message = "Status disewa atau hilang mengikuti proses rental dan tidak dapat diubah lewat form ini."
                            Case "RG_MASTER_QC" : message = "Konfirmasi pemeriksaan sebelum menyatakan unit siap."
                            Case "RG_MASTER_COMPONENTS" : message = "Periksa isi paket: jenis alat harus unik dan jumlahnya positif."
                            Case "RG_MASTER_VERSION", "RG_MASTER_MISSING" : message = "Data sudah berubah atau tidak ditemukan. Tutup form, muat ulang daftar, lalu buka kembali."
                            Case "RG_MASTER_REQUEST" : message = "Permintaan sudah digunakan untuk data lain. Tutup form dan periksa daftar sebelum mencoba lagi."
                        End Select
                        Throw New SupabaseRequestException(response.StatusCode, message, code)
                    End If
                    Dim result As New Reply()
                    If response.Content IsNot Nothing Then
                        result.Content = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                        Dim ranges As IEnumerable(Of String) = Nothing
                        If response.Content.Headers.TryGetValues("Content-Range", ranges) OrElse response.Headers.TryGetValues("Content-Range", ranges) Then
                            Dim total As Long
                            Dim suffix = ranges.First().Split("/"c).Last()
                            If Long.TryParse(suffix, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, total) AndAlso total >= 0 Then result.Total = total
                        End If
                    End If
                    Return result
                End Using
            End Using
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If ownsClient Then client.Dispose()
        End Sub
    End Class
End Namespace
