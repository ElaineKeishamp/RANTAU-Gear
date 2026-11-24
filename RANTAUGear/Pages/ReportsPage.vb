Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.UI
Namespace Pages
    Public Class ReportsPage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly fromDate As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Width = 125, .Value = New DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), .AccessibleName = "Dari tanggal"}
        Private ReadOnly toDate As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Width = 125, .Value = DateTime.Today, .AccessibleName = "Sampai tanggal"}
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly totals As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Primary, .Font = New Font("Segoe UI", 11, FontStyle.Bold)}
        Private filtered As New List(Of Rental)
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
            Dim layout = UiHelpers.PageLayout(Me, "Rekap berdasarkan tanggal pengambilan. Nilai sewa adalah subtotal transaksi, di luar deposit.")
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
            toolbar.Controls.Add(UiHelpers.LabelOf("Dari", 10, Theme.Text, False))
            toolbar.Controls.Add(fromDate)
            toolbar.Controls.Add(UiHelpers.LabelOf("Sampai", 10, Theme.Text, False))
            toolbar.Controls.Add(toDate)
            Dim export As New RoundedButton With {.Text = "Ekspor CSV", .Width = 130, .Height = 36, .CornerRadius = 8}
            toolbar.Controls.Add(export)
            layout.Controls.Add(toolbar, 0, 1)
            Dim content As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
            content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 76))
            content.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            content.Controls.Add(totals, 0, 0)
            UiHelpers.StyleGrid(grid)
            UiHelpers.ApplyColumnAlignments(grid)
            content.Controls.Add(grid, 0, 1)
            layout.Controls.Add(content, 0, 2)
            AddHandler fromDate.ValueChanged, Sub() RefreshRows()
            AddHandler toDate.ValueChanged, Sub() RefreshRows()
            AddHandler export.Click, AddressOf ExportCsv
            RefreshRows()
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub RefreshRows()
            filtered = repository.GetRentals().Where(Function(r) r.StartAt.Date >= fromDate.Value.Date AndAlso r.StartAt.Date <= toDate.Value.Date).OrderByDescending(Function(r) r.StartAt).ToList()
            grid.DataSource = filtered.Select(Function(r) New With {.Nomor = r.RentalNumber, .Pelanggan = repository.GetCustomerById(r.CustomerId).Name, .Tanggal = r.StartAt.ToString("dd MMM yyyy"), .Sewa = UiHelpers.Currency(r.Subtotal), .Deposit = UiHelpers.Currency(r.Deposit), .Denda = UiHelpers.Currency(r.LateFine + r.DamageFine), .Status = UiHelpers.StatusText(r.Status, r.DueAt)}).ToList()
            totals.Text = If(fromDate.Value.Date > toDate.Value.Date, "Tanggal awal harus sebelum atau sama dengan tanggal akhir.", filtered.Count.ToString() & " transaksi  |  Nilai sewa: " & UiHelpers.Currency(filtered.Where(Function(r) r.Status <> RentalStatus.Cancelled).Sum(Function(r) r.Subtotal)) & Environment.NewLine & If(filtered.Count = 0, "Belum ada transaksi pada periode ini.", "Denda tercatat: " & UiHelpers.Currency(repository.GetFines().Where(Function(f) filtered.Any(Function(r) r.Id = f.RentalId)).Sum(Function(f) f.Amount)) & "  |  Deposit bukan pendapatan."))
        End Sub
        Private Shared Function Csv(value As String) As String
            If value.Length > 0 AndAlso "=+-@".Contains(value(0)) Then value = "'" & value
            Return """" & value.Replace("""", """""") & """"
        End Function
        Private Sub ExportCsv(sender As Object, e As EventArgs)
            If fromDate.Value.Date > toDate.Value.Date OrElse filtered.Count = 0 Then
                MessageBox.Show("Pilih periode yang berisi transaksi sebelum mengekspor.", "Ekspor laporan", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Using dialog As New SaveFileDialog With {.Filter = "CSV (*.csv)|*.csv", .FileName = "rantau-laporan-" & DateTime.Today.ToString("yyyy-MM-dd") & ".csv"}
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    Dim lines As New List(Of String) From {"Nomor,Pelanggan,Tanggal,Sewa,Deposit,Denda,Status"}
                    For Each rental In filtered
                        lines.Add(String.Join(",", {Csv(rental.RentalNumber), Csv(repository.GetCustomerById(rental.CustomerId).Name), Csv(rental.StartAt.ToString("yyyy-MM-dd HH:mm")), rental.Subtotal.ToString(CultureInfo.InvariantCulture), rental.Deposit.ToString(CultureInfo.InvariantCulture), (rental.LateFine + rental.DamageFine).ToString(CultureInfo.InvariantCulture), Csv(UiHelpers.StatusText(rental.Status, rental.DueAt))}))
                    Next
                    File.WriteAllLines(dialog.FileName, lines, New UTF8Encoding(True))
                    MessageBox.Show("Laporan berhasil disimpan.", "Ekspor CSV", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    MessageBox.Show("File tidak dapat ditulis. Pilih folder lain atau tutup file yang sedang dibuka.", "Ekspor gagal", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Sub
    End Class
End Namespace
