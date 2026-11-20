Option Strict On
Option Explicit On
Option Infer On

Imports System

Namespace Models
    Public Class Fine
        Public Property Id As Integer
        Public Property RentalId As Integer
        Public Property FineType As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property Amount As Decimal
        Public Property CreatedAt As DateTime
    End Class
End Namespace
