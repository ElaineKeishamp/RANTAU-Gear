Option Strict On
Option Explicit On
Option Infer On
Imports System.Text.Json.Serialization
Namespace Models
    Public Enum ReturnAction
        Inspect
        Approve
        Settle
    End Enum
    Public Class ReturnUnit
        <JsonPropertyName("unit_id")> Public Property UnitId As Long
        <JsonPropertyName("code")> Public Property Code As String
        <JsonPropertyName("name")> Public Property Name As String
        <JsonPropertyName("replacement_value")> Public Property ReplacementValue As Decimal
        <JsonPropertyName("condition")> Public Property Condition As String
        <JsonPropertyName("charge")> Public Property Charge As Decimal
        <JsonPropertyName("notes")> Public Property Notes As String
    End Class
    Public Class ReturnCheck
        Public Property UnitId As Long
        Public Property Condition As String
        Public Property Charge As Decimal
        Public Property Notes As String
    End Class
    Public Class ReturnFinancial
        <JsonRequired, JsonPropertyName("deposit")> Public Property Deposit As Decimal
        <JsonRequired, JsonPropertyName("proposed_charge")> Public Property ProposedCharge As Decimal
        <JsonRequired, JsonPropertyName("late_charge")> Public Property LateCharge As Decimal
        <JsonRequired, JsonPropertyName("total_charges")> Public Property TotalCharges As Decimal
        <JsonRequired, JsonPropertyName("deposit_applied")> Public Property DepositApplied As Decimal
        <JsonRequired, JsonPropertyName("refund")> Public Property Refund As Decimal
        <JsonRequired, JsonPropertyName("additional")> Public Property Additional As Decimal
    End Class
    Public Class ReturnDetails
        <JsonPropertyName("id")> Public Property Id As Long
        <JsonPropertyName("rental_number")> Public Property Number As String
        <JsonPropertyName("state")> Public Property State As String
        <JsonPropertyName("due_at")> Public Property DueAt As DateTimeOffset
        <JsonPropertyName("returned_at")> Public Property ReturnedAt As DateTimeOffset?
        <JsonPropertyName("approved")> Public Property Approved As Boolean
        <JsonPropertyName("settled")> Public Property Settled As Boolean
        <JsonPropertyName("units")> Public Property Units As New List(Of ReturnUnit)()
        <JsonPropertyName("financial")> Public Property Financial As ReturnFinancial
    End Class
End Namespace
