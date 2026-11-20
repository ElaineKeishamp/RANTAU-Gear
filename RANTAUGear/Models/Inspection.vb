Option Strict On
Option Explicit On
Option Infer On

Imports System

Namespace Models
    Public Class Inspection
        Public Property Id As Integer
        Public Property RentalId As Integer
        Public Property UnitId As Integer
        Public Property InspectionType As String = String.Empty
        Public Property Condition As String = "Baik"
        Public Property Notes As String = String.Empty
        Public Property FineAmount As Decimal
        Public Property InspectionDate As DateTime
        Public Property InspectorName As String = String.Empty
    End Class
End Namespace
