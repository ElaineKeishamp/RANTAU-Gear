Option Strict On
Option Explicit On
Option Infer On

Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks

Namespace Data.Supabase
    Public NotInheritable Class SupabaseSessionManager
        Implements IDisposable

        Private ReadOnly gateway As SupabaseGateway
        Private ReadOnly time As TimeProvider
        Private ReadOnly gate As New SemaphoreSlim(1, 1)
        Private session As SupabaseSession
        Private expiresAt As DateTimeOffset
        Public ReadOnly Property UserId As Guid

        Public Sub New(gateway As SupabaseGateway, Optional time As TimeProvider = Nothing)
            Me.gateway = gateway
            Me.time = If(time, TimeProvider.System)
        End Sub

        Public Async Function SignInAsync(email As String, password As String, cancellationToken As CancellationToken) As Task
            Await gate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                If session IsNot Nothing Then Throw New InvalidOperationException("Sesi sudah aktif.")
                Dim candidate = Await gateway.SignInAsync(email, password, cancellationToken).ConfigureAwait(False)
                ' Retain the token for cleanup even if server-side user validation fails.
                session = candidate
                expiresAt = time.GetUtcNow().AddSeconds(candidate.ExpiresIn)
                Dim user = Await gateway.GetUserAsync(candidate.AccessToken, cancellationToken).ConfigureAwait(False)
                _UserId = user.Id
            Finally
                gate.Release()
            End Try
        End Function

        Public Async Function GetAccessTokenAsync(cancellationToken As CancellationToken, Optional rejectedToken As String = Nothing) As Task(Of String)
            Await gate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                If session Is Nothing OrElse UserId = Guid.Empty Then Throw New UnauthorizedAccessException("Sesi telah berakhir. Silakan masuk kembali.")
                Dim rejected = rejectedToken IsNot Nothing AndAlso String.Equals(rejectedToken, session.AccessToken, StringComparison.Ordinal)
                If Not rejected AndAlso time.GetUtcNow() < expiresAt.AddSeconds(-60) Then Return session.AccessToken
                Dim renewed As SupabaseSession = Nothing
                Dim failed As Boolean
                Try
                    renewed = Await gateway.RefreshSessionAsync(session.RefreshToken, cancellationToken).ConfigureAwait(False)
                Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.BadRequest OrElse ex.StatusCode = HttpStatusCode.Unauthorized OrElse ex.StatusCode = HttpStatusCode.Forbidden
                    failed = True
                End Try
                If failed Then
                    session = Nothing
                    _UserId = Guid.Empty
                    Throw New UnauthorizedAccessException("Sesi telah berakhir. Silakan masuk kembali.")
                End If
                session = renewed
                expiresAt = time.GetUtcNow().AddSeconds(renewed.ExpiresIn)
                Return renewed.AccessToken
            Finally
                gate.Release()
            End Try
        End Function

        Public Async Function SignOutAsync(cancellationToken As CancellationToken) As Task
            Dim token As String = Nothing
            Await gate.WaitAsync(CancellationToken.None).ConfigureAwait(False)
            Try
                If session IsNot Nothing Then token = session.AccessToken
                session = Nothing
                _UserId = Guid.Empty
                expiresAt = DateTimeOffset.MinValue
            Finally
                gate.Release()
            End Try
            If token Is Nothing Then Return
            Try
                Await gateway.SignOutAsync(token, cancellationToken).ConfigureAwait(False)
            Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.Unauthorized OrElse ex.StatusCode = HttpStatusCode.Forbidden
                ' The server has already invalidated this session; local state is also cleared.
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            session = Nothing
            _UserId = Guid.Empty
            gate.Dispose()
        End Sub
    End Class
End Namespace
