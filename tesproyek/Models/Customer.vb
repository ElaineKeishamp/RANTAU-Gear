Option Strict On
Option Explicit On
Option Infer On

Namespace Models
    Public Class Customer
        Public Property Id As Integer
        Public Property Name As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property GuaranteeType As String = String.Empty
        Public Property Deposit As Decimal
        Public Property IdentityVerified As Boolean
    End Class
End Namespace
