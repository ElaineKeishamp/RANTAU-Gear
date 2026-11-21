Option Strict On
Option Explicit On
Option Infer On
Imports System.Text.Json.Serialization
Namespace Models
    Public Class ReservationRequest
        Public Property CustomerId As Long
        Public Property PickupAt As DateTimeOffset
        Public Property DueAt As DateTimeOffset
        Public Property PackageId As Long?
        Public Property Lines As New List(Of MasterPackageItem)()
        Public Property Method As String = "cash"
        Public Property Reference As String = String.Empty
        Public Sub Validate()
            If CustomerId <= 0 Then Throw New ArgumentException("Pilih pelanggan aktif. Tambahkan melalui menu Pelanggan bila belum terdaftar.")
            If DueAt <= PickupAt Then Throw New ArgumentException("Jadwal kembali harus setelah jadwal ambil.")
            If Not {"cash", "transfer"}.Contains(Method) Then Throw New ArgumentException("Pilih metode pembayaran DP.")
            If Lines.Count = 0 OrElse Lines.Any(Function(x) x.TypeId <= 0 OrElse x.Quantity <= 0) OrElse Lines.Select(Function(x) x.TypeId).Distinct().Count() <> Lines.Count Then Throw New ArgumentException("Pilih minimal satu alat dengan jumlah bulat positif.")
        End Sub
    End Class
    Public Class ReservationOptions
        Public Property Customers As New List(Of MasterRecord)()
        Public Property Types As New List(Of MasterRecord)()
        Public Property Packages As New List(Of MasterRecord)()
    End Class
    Public Class ReservationQuote
        <JsonPropertyName("daily_price")> Public Property DailyPrice As Decimal
        <JsonPropertyName("rental_days")> Public Property RentalDays As Integer
        <JsonPropertyName("rent_amount")> Public Property RentAmount As Decimal
        <JsonPropertyName("down_payment_due")> Public Property DownPayment As Decimal
        <JsonPropertyName("deposit_due")> Public Property Deposit As Decimal
        <JsonPropertyName("pickup_deadline_at")> Public Property Deadline As DateTimeOffset
    End Class
    Public Class ReservationReceipt
        <JsonPropertyName("id")> Public Property Id As Long
        <JsonPropertyName("rental_number")> Public Property Number As String
        <JsonPropertyName("rent_amount")> Public Property RentAmount As Decimal
        <JsonPropertyName("down_payment_due")> Public Property DownPayment As Decimal
        <JsonPropertyName("deposit_due")> Public Property Deposit As Decimal
    End Class
End Namespace
