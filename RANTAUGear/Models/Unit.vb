Option Strict On
Option Explicit On
Option Infer On

Namespace Models
    Public Class Unit
        Public Property Id As Integer
        Public Property Code As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property CategoryId As Integer
        Public Property HourlyRate As Decimal
        Public Property DailyRate As Decimal
        Public Property Status As UnitStatus
        Public Property Description As String = String.Empty
    End Class
End Namespace
