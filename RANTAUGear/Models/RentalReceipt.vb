Option Strict On
Option Explicit On
Option Infer On
Namespace Models
    Public Class ReceiptLine
        Public Property Name As String = String.Empty
        Public Property Quantity As Integer
    End Class
    ''' <summary>Data nota sewa. Dibangun dari PickupDetails; tidak membaca atau mengubah database.</summary>
    Public Class RentalReceipt
        Public Property Number As String = String.Empty
        Public Property CustomerName As String = String.Empty
        Public Property PickupAt As DateTimeOffset
        Public Property DueAt As DateTimeOffset
        Public Property RentAmount As Decimal
        Public Property RentPaidBefore As Decimal
        Public Property RentPaidNow As Decimal
        Public Property DepositTotal As Decimal
        Public Property Method As String = String.Empty
        Public Property Lines As New List(Of ReceiptLine)()
        Public Property UnitCodes As New List(Of String)()
    End Class
End Namespace
