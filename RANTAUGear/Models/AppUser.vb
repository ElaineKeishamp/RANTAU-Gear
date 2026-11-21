Option Strict On
Option Explicit On
Option Infer On

Namespace Models
    Public Class AppUser
        Public Property Id As Integer
        Public Property AuthUserId As Guid
        Public Property Username As String = String.Empty
        Public Property DisplayName As String = String.Empty
        Public Property Password As String = String.Empty
        Public Property Role As UserRole
        Public Property IsActive As Boolean = True
    End Class
End Namespace
