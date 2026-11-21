Option Strict On
Option Explicit On
Option Infer On
Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports RantauGear.Models
Namespace Services
    Public Module ReportFiles
        Public Function ToTable(snapshot As ReportSnapshot) As DataTable
            Dim table As New DataTable With {.Locale = CultureInfo.GetCultureInfo("id-ID")}
            Dim specs As (Key As String, Caption As String, Kind As Type)()
            If snapshot.Kind = "cash" Then
                specs = {("recorded_at", "Waktu (WIB)", GetType(DateTime)), ("rental_number", "Nomor sewa", GetType(String)), ("kind", "Jenis uang", GetType(String)), ("method", "Metode", GetType(String)), ("cash_in", "Masuk (Rp)", GetType(Decimal)), ("cash_out", "Keluar (Rp)", GetType(Decimal)), ("reference", "Referensi", GetType(String))}
            Else
                specs = {("rental_number", "Nomor sewa", GetType(String)), ("customer_name", "Pelanggan", GetType(String)), ("pickup_at", "Ambil (WIB)", GetType(DateTime)), ("due_at", "Kembali (WIB)", GetType(DateTime)), ("display_state", "Status", GetType(String)), ("rent_amount", "Nilai sewa (Rp)", GetType(Decimal))}
            End If
            For Each spec In specs
                table.Columns.Add(spec.Caption, spec.Kind)
            Next
            For Each row In snapshot.Rows
                Dim values As New List(Of Object)()
                For Each spec In specs
                    Dim field = row.GetProperty(spec.Key)
                    If spec.Kind Is GetType(DateTime) Then
                        values.Add(DateTimeOffset.Parse(field.GetString(), CultureInfo.InvariantCulture).ToOffset(TimeSpan.FromHours(7)).DateTime)
                    ElseIf spec.Kind Is GetType(Decimal) Then
                        values.Add(field.GetDecimal())
                    Else
                        Dim text = If(field.ValueKind = JsonValueKind.Null, String.Empty, field.GetString())
                        values.Add(If({"kind", "method", "display_state"}.Contains(spec.Key), Caption(text), text))
                    End If
                Next
                table.Rows.Add(values.ToArray())
            Next
            Return table
        End Function
        Public Function Caption(value As String) As String
            Select Case value
                Case "rental_receipts" : Return "Penerimaan sewa/DP/denda"
                Case "deposit_refund" : Return "Refund deposit"
                Case "rent_refund" : Return "Refund sewa"
                Case "cash_net" : Return "Arus kas transaksi bersih"
                Case "deposit_applied" : Return "Deposit dipotong (nonkas)"
                Case "opening_deposit" : Return "Saldo deposit awal"
                Case "closing_deposit" : Return "Saldo deposit akhir"
                Case "total" : Return "Jumlah penyewaan"
                Case "down_payment" : Return "DP"
                Case "rent_payment" : Return "Pelunasan/perpanjangan"
                Case "fine_payment" : Return "Pembayaran denda"
                Case "deposit_received" : Return "Deposit diterima"
                Case "cash" : Return "Tunai"
                Case "transfer" : Return "Transfer"
                Case "reserved" : Return "Dipesan"
                Case "active" : Return "Disewa"
                Case "late" : Return "Terlambat"
                Case "returned" : Return "Dikembalikan"
                Case "cancelled" : Return "Dibatalkan"
                Case "expired" : Return "Hangus"
                Case Else : Return value
            End Select
        End Function
        Private Function Cell(value As String) As String
            Dim first = value.TrimStart()
            Dim number As Decimal
            Dim numeric = Decimal.TryParse(value, NumberStyles.AllowLeadingSign Or NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, number)
            If (Not numeric AndAlso first.Length > 0 AndAlso "=+-@".Contains(first(0))) OrElse (value.Length > 0 AndAlso {ChrW(9), ChrW(10), ChrW(13)}.Contains(value(0))) Then value = "'" & value
            Return """" & value.Replace("""", """""") & """"
        End Function
        Public Function ExportCsv(query As ReportQuery, snapshot As ReportSnapshot) As String
            If snapshot.Total <> snapshot.Rows.Count Then Throw New InvalidOperationException("Laporan terlalu besar untuk ekspor. Persempit periode sampai maksimal 10.000 baris.")
            Dim configured = Environment.GetEnvironmentVariable("RANTAUGEAR_EXPORT_DIRECTORY")
            If String.IsNullOrWhiteSpace(configured) Then configured = TryCast(AppContext.GetData("RantauGear.ExportDirectory"), String)
            Dim root = If(String.IsNullOrWhiteSpace(configured), "D:\elaine\SEM 5\Pemrograman Visual-backup\Laporan", If(IO.Path.IsPathRooted(configured), configured, IO.Path.Combine(AppContext.BaseDirectory, configured)))
            Directory.CreateDirectory(root)
            Dim path = IO.Path.Combine(root, "RANTAUGear-" & query.Kind & "-" & DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 6) & ".csv")
            Using table = ToTable(snapshot), stream As New FileStream(path, FileMode.CreateNew, FileAccess.Write), writer As New StreamWriter(stream, New UTF8Encoding(True))
                writer.NewLine = vbCrLf
                writer.WriteLine(Cell("RANTAU Gear | " & query.FromDate.ToString("yyyy-MM-dd") & " sampai " & query.ToDate.ToString("yyyy-MM-dd") & " | WIB | " & If(query.Kind = "cash", "Kas transaksi", "Penyewaan") & " | status " & If(query.Status = "all", "Semua", Caption(query.Status))))
                writer.WriteLine(Cell("Snapshot " & snapshot.SnapshotAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss") & " WIB"))
                For Each entry In snapshot.Summary.OrderBy(Function(x) x.Key)
                    writer.WriteLine(Cell(Caption(entry.Key)) & ";" & Cell(entry.Value.ToString(CultureInfo.InvariantCulture)))
                Next
                writer.WriteLine()
                writer.WriteLine(String.Join(";", table.Columns.Cast(Of DataColumn)().Select(Function(column) Cell(column.ColumnName))))
                For Each row As DataRow In table.Rows
                    Dim cells As New List(Of String)()
                    For Each item In row.ItemArray
                        Dim value = If(TypeOf item Is DateTime, CDate(item).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), If(TypeOf item Is Decimal, CDec(item).ToString(CultureInfo.InvariantCulture), Convert.ToString(item, CultureInfo.InvariantCulture)))
                        cells.Add(Cell(value))
                    Next
                    writer.WriteLine(String.Join(";", cells))
                Next
            End Using
            Return path
        End Function
    End Module
End Namespace
