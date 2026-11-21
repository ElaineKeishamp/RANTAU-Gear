Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text.Json

Namespace Data.Supabase
    Public NotInheritable Class SupabaseOptions
        Public ReadOnly Property ProjectUrl As Uri
        Public ReadOnly Property PublishableKey As String

        Public Sub New(projectUrl As String, publishableKey As String)
            Dim parsed As Uri = Nothing
            If Not Uri.TryCreate(projectUrl, UriKind.Absolute, parsed) OrElse parsed.Scheme <> Uri.UriSchemeHttps OrElse
                parsed.AbsolutePath <> "/" OrElse parsed.Query.Length > 0 OrElse parsed.Fragment.Length > 0 OrElse parsed.UserInfo.Length > 0 Then
                Throw New ArgumentException("Project URL harus alamat HTTPS Supabase tanpa path atau kredensial.", NameOf(projectUrl))
            End If
            If String.IsNullOrWhiteSpace(publishableKey) OrElse Not publishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal) OrElse publishableKey.Any(Function(character) Char.IsWhiteSpace(character)) Then
                Throw New ArgumentException("Gunakan publishable key sb_publishable_; secret/service-role key tidak diizinkan.", NameOf(publishableKey))
            End If
            Me.ProjectUrl = parsed
            Me.PublishableKey = publishableKey
        End Sub

        Public Shared Function Load(filePath As String) As SupabaseOptions
            Using document = JsonDocument.Parse(File.ReadAllText(filePath))
                Return New SupabaseOptions(document.RootElement.GetProperty("projectUrl").GetString(), document.RootElement.GetProperty("publishableKey").GetString())
            End Using
        End Function

        Public Shared Function LoadForApplication() As SupabaseOptions
            Dim configuredPath = Environment.GetEnvironmentVariable("RANTAUGEAR_CONFIG_PATH")
            Dim filePath = If(String.IsNullOrWhiteSpace(configuredPath), Path.Combine(AppContext.BaseDirectory, "supabase.local.json"), configuredPath)
            Try
                Return Load(filePath)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is KeyNotFoundException
                Throw New InvalidOperationException("Koneksi aplikasi belum dikonfigurasi dengan benar. Hubungi administrator.", ex)
            End Try
        End Function
    End Class
End Namespace
