Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Collections.Generic

Namespace Models
    Public Class Rental
        Public Property Id As Integer
        Public Property RentalNumber As String = String.Empty
        Public Property CustomerId As Integer
        Public Property StartAt As DateTime
        Public Property DueAt As DateTime
        Public Property ReturnedAt As Nullable(Of DateTime)
        Public Property Status As RentalStatus
        Public Property Subtotal As Decimal
        Public Property Deposit As Decimal
        Public Property LateFine As Decimal
        Public Property DamageFine As Decimal
        Public Property Total As Decimal
        Public Property CreatedBy As String = String.Empty
        Public Property Items As New List(Of RentalItem)()
    End Class

    Public Class RentalItem
        Public Property Id As Integer
        Public Property RentalId As Integer
        Public Property UnitId As Integer
        Public Property PackageId As Integer
        Public Property ItemName As String = String.Empty
        Public Property Quantity As Integer
        Public Property DailyRate As Decimal
        Public Property ConditionOnCheckout As String = "Baik"
        Public Property ConditionOnReturn As String = String.Empty
        Public Property ReturnNotes As String = String.Empty
        Public Property DamageFine As Decimal
    End Class
End Namespace
