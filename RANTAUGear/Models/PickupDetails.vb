Option Strict On
Option Explicit On
Option Infer On
Imports System.Text.Json.Serialization
Namespace Models
    Public Class PickupLine
        <JsonPropertyName("type_id")> Public Property TypeId As Long
        <JsonPropertyName("name")> Public Property Name As String
        <JsonPropertyName("quantity")> Public Property Quantity As Integer
    End Class
    Public Class PickupUnit
        <JsonPropertyName("id")> Public Property Id As Long
        <JsonPropertyName("type_id")> Public Property TypeId As Long
        <JsonPropertyName("code")> Public Property Code As String
    End Class
    Public Class PickupDetails
        <JsonPropertyName("id")> Public Property Id As Long
        <JsonPropertyName("rental_number")> Public Property Number As String
        <JsonPropertyName("customer_name")> Public Property CustomerName As String
        <JsonPropertyName("pickup_at")> Public Property PickupAt As DateTimeOffset
        <JsonPropertyName("due_at")> Public Property DueAt As DateTimeOffset
        <JsonPropertyName("deadline")> Public Property Deadline As DateTimeOffset
        <JsonPropertyName("rent_amount")> Public Property RentAmount As Decimal
        <JsonPropertyName("rent_paid")> Public Property RentPaid As Decimal
        <JsonPropertyName("deposit_paid")> Public Property DepositPaid As Decimal
        <JsonPropertyName("rent_balance")> Public Property RentBalance As Decimal
        <JsonPropertyName("deposit_balance")> Public Property DepositBalance As Decimal
        <JsonPropertyName("lines")> Public Property Lines As New List(Of PickupLine)()
        <JsonPropertyName("units")> Public Property Units As New List(Of PickupUnit)()
    End Class
    Public Class PickupReceipt
        Inherits ReservationReceipt
        <JsonPropertyName("state")> Public Property State As String
    End Class
End Namespace
