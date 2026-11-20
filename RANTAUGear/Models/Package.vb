Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic

Namespace Models
    Public Class Package
        Public Property Id As Integer
        Public Property Name As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property DailyPrice As Decimal
        Public Property Items As New List(Of PackageItem)()
    End Class

    Public Class PackageItem
        Public Property Id As Integer
        Public Property PackageId As Integer
        Public Property UnitId As Integer
        Public Property Quantity As Integer
    End Class
End Namespace
