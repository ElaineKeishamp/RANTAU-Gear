Option Strict On
Option Explicit On
Option Infer On
Imports System.Text.Json.Serialization
Namespace Models
    Public Enum BookingChangeAction
        Extend
        Cancel
        Expire
    End Enum
    Public Class BookingChangeDetails
        <JsonRequired, JsonPropertyName("id")> Public Property Id As Long
        <JsonRequired, JsonPropertyName("rental_number")> Public Property Number As String
        <JsonRequired, JsonPropertyName("customer_name")> Public Property CustomerName As String
        <JsonRequired, JsonPropertyName("state")> Public Property State As String
        <JsonRequired, JsonPropertyName("pickup_at")> Public Property PickupAt As DateTimeOffset
        <JsonRequired, JsonPropertyName("due_at")> Public Property DueAt As DateTimeOffset
        <JsonRequired, JsonPropertyName("deadline")> Public Property Deadline As DateTimeOffset
        <JsonRequired, JsonPropertyName("server_now")> Public Property ServerNow As DateTimeOffset
        <JsonRequired, JsonPropertyName("rent_amount")> Public Property RentAmount As Decimal
        <JsonRequired, JsonPropertyName("daily_price")> Public Property DailyPrice As Decimal
        <JsonRequired, JsonPropertyName("dp_received")> Public Property DpReceived As Decimal
        <JsonRequired, JsonPropertyName("deposit_received")> Public Property DepositReceived As Decimal
    End Class
    Public Class ExtensionQuote
        <JsonRequired, JsonPropertyName("old_due")> Public Property OldDue As DateTimeOffset
        <JsonRequired, JsonPropertyName("new_due")> Public Property NewDue As DateTimeOffset
        <JsonRequired, JsonPropertyName("days")> Public Property Days As Integer
        <JsonRequired, JsonPropertyName("rent_amount")> Public Property RentAmount As Decimal
        <JsonRequired, JsonPropertyName("additional")> Public Property Additional As Decimal
        <JsonRequired, JsonPropertyName("deposit")> Public Property Deposit As Decimal
    End Class
End Namespace
