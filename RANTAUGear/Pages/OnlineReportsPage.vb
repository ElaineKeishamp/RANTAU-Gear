Option Strict On
Option Explicit On
Option Infer On
Imports System.Data
Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Models
Imports RantauGear.Services
Imports RantauGear.UI
Namespace Pages
    Public NotInheritable Class OnlineReportsPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly filters As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
        Private ReadOnly kindBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 155, .AccessibleName = "Jenis laporan"}
        Private ReadOnly fromDate As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Width = 145, .AccessibleName = "Tanggal mulai WIB"}
        Private ReadOnly toDate As New DateTimePicker With {.Format = DateTimePickerFormat.Short, .Width = 145, .AccessibleName = "Tanggal akhir WIB"}
        Private ReadOnly statusBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 145, .AccessibleName = "Status penyewaan"}
        Private ReadOnly loadButton As New RoundedButton With {.Text = "Tampilkan", .Width = 120, .Height = 36, .CornerRadius = 6}
        Private ReadOnly exportButton As New Button With {.Text = "Ekspor CSV", .Width = 120, .Height = 36, .Enabled = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text}
        Private ReadOnly previous As New Button With {.Text = "Sebelumnya", .Width = 110, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text}
        Private ReadOnly following As New Button With {.Text = "Berikutnya", .Width = 110, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text}
        Private ReadOnly cancel As New Button With {.Text = "Batalkan", .Width = 100, .Height = 36, .Visible = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#FEE2E4"), .ForeColor = ColorTranslator.FromHtml("#DC2626")}
        Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Text}
        Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly empty As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Theme.Muted}
        Private loaded As ReportSnapshot
        Private lastQuery As ReportQuery
        Private offset As Integer
        Private busy As Boolean
        Private dirty As Boolean
        Private activeRequest As CancellationTokenSource
        Public ReadOnly Property LastExportPath As String
        Public ReadOnly Property PendingLoad As Task = Task.CompletedTask Implements IWorkspacePage.PendingLoad
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
            Dock = DockStyle.Fill : Padding = New Padding(18) : BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 5, .ColumnCount = 1}
            For Each rowHeight As Integer In {104, 150, 54, 65}
                layout.RowStyles.Add(New RowStyle(SizeType.Absolute, rowHeight))
            Next
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100)) : Controls.Add(layout)
            kindBox.Items.AddRange({"Kas transaksi", "Penyewaan"}) : kindBox.SelectedIndex = 0
            statusBox.Items.AddRange({"Semua", "Dipesan", "Disewa", "Terlambat", "Dikembalikan", "Dibatalkan", "Hangus"}) : statusBox.SelectedIndex = 0 : statusBox.Enabled = False
            Dim today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date
            fromDate.Value = New DateTime(today.Year, today.Month, 1) : toDate.Value = today
            For Each control As Control In New Control() {kindBox, New Label With {.Text = "Dari", .AutoSize = True}, fromDate, New Label With {.Text = "Sampai", .AutoSize = True}, toDate, statusBox, loadButton}
                filters.Controls.Add(control)
            Next
            layout.Controls.Add(filters, 0, 0) : layout.Controls.Add(summary, 0, 1)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
            For Each button As Button In {exportButton, previous, following, cancel}
                button.Cursor = Cursors.Hand : actions.Controls.Add(button)
            Next
            layout.Controls.Add(actions, 0, 2) : layout.Controls.Add(status, 0, 3)
            UiHelpers.StyleGrid(grid)
            UiHelpers.ApplyColumnAlignments(grid)
            layout.Controls.Add(grid, 0, 4) : layout.Controls.Add(empty, 0, 4)
            grid.Visible = False : empty.Text = "Memuat laporan..."
            AddHandler kindBox.SelectedIndexChanged, Sub() FilterChanged()
            AddHandler statusBox.SelectedIndexChanged, Sub() FilterChanged()
            AddHandler fromDate.ValueChanged, Sub() FilterChanged()
            AddHandler toDate.ValueChanged, Sub() FilterChanged()
            AddHandler loadButton.Click, Sub() StartLoad(0)
            AddHandler previous.Click, Sub() StartLoad(Math.Max(0, offset - 25))
            AddHandler following.Click, Sub() StartLoad(offset + 25)
            AddHandler exportButton.Click, Sub() StartExport()
            AddHandler cancel.Click, Sub() activeRequest?.Cancel()
        End Sub
        Protected Overrides Async Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e) : _PendingLoad = LoadAsync(0) : Await PendingLoad
        End Sub
        Private Sub FilterChanged()
            dirty = True : statusBox.Enabled = kindBox.SelectedIndex = 1
            If kindBox.SelectedIndex = 0 AndAlso statusBox.SelectedIndex <> 0 Then statusBox.SelectedIndex = 0
            status.Text = "Filter berubah. Klik Tampilkan; tabel sebelumnya tetap ditampilkan."
            SetBusy(False)
        End Sub
        Private Function Query() As ReportQuery
            Dim states = {"all", "reserved", "active", "late", "returned", "cancelled", "expired"}
            Return New ReportQuery With {.Kind = If(kindBox.SelectedIndex = 0, "cash", "rentals"), .FromDate = fromDate.Value.Date, .ToDate = toDate.Value.Date, .Status = If(kindBox.SelectedIndex = 0, "all", states(statusBox.SelectedIndex))}
        End Function
        Private Async Sub StartLoad(newOffset As Integer)
            If busy Then Return
            _PendingLoad = LoadAsync(newOffset) : Await PendingLoad
        End Sub
        Private Async Function LoadAsync(newOffset As Integer) As Task
            Dim query = If(dirty OrElse lastQuery Is Nothing OrElse newOffset = 0, Me.Query(), lastQuery.Copy())
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    query.Validate()
                    status.ForeColor = Theme.Muted : status.Text = "Mengambil laporan dari server..."
                    Dim result = Await repository.ReadReportAsync(query, newOffset, 25, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    Dim table = ReportFiles.ToTable(result)
                    Dim old = TryCast(grid.DataSource, DataTable)
                    grid.DataSource = table : old?.Dispose()
                    For Each column As DataGridViewColumn In grid.Columns
                        column.MinimumWidth = If(column.ValueType Is GetType(DateTime), 240, 170)
                        If column.ValueType Is GetType(DateTime) Then column.DefaultCellStyle.Format = "dd MMM yyyy HH:mm"
                        If column.ValueType Is GetType(Decimal) Then
                            column.DefaultCellStyle.Format = "N0"
                            column.DefaultCellStyle.FormatProvider = Globalization.CultureInfo.GetCultureInfo("id-ID")
                        End If
                    Next
                    loaded = result : lastQuery = query.Copy() : offset = newOffset : dirty = False
                    summary.Text = SummaryText(result, query)
                    status.Text = If(result.Total = 0, "Tidak ada transaksi dalam periode ini.", (offset + 1).ToString() & " sampai " & (offset + result.Rows.Count).ToString() & " dari " & result.Total.ToString()) & Environment.NewLine & "Snapshot " & result.SnapshotAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm:ss") & " WIB"
                    grid.Visible = result.Rows.Count > 0 : empty.Visible = Not grid.Visible : empty.Text = "Tidak ada data sesuai filter."
                Catch ex As Exception When Expected(ex)
                    ShowError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
        End Function
        Private Shared Function SummaryText(result As ReportSnapshot, query As ReportQuery) As String
            Dim s = result.Summary
            Dim period = query.FromDate.ToString("dd/MM/yyyy") & " sampai " & query.ToDate.ToString("dd/MM/yyyy") & " WIB"
            If result.Kind = "cash" Then
                Dim money As Func(Of String, String) = Function(key) s(key).ToString("C0", Globalization.CultureInfo.GetCultureInfo("id-ID"))
                Return period & " | Berdasarkan waktu pembayaran" & Environment.NewLine & "Sewa/DP/denda masuk " & money("rental_receipts") & " | Deposit masuk " & money("deposit_received") & Environment.NewLine & "Refund deposit " & money("deposit_refund") & " | Refund sewa " & money("rent_refund") & " | Arus kas bersih " & money("cash_net") & Environment.NewLine & "Deposit dipotong (nonkas) " & money("deposit_applied") & Environment.NewLine & "Saldo deposit: awal " & money("opening_deposit") & " | akhir " & money("closing_deposit") & Environment.NewLine & "Kas transaksi rental, belum termasuk modal atau biaya operasional."
            End If
            Return period & " | Berdasarkan jadwal ambil" & Environment.NewLine & "Total " & s("total").ToString() & " | Dipesan " & s("reserved").ToString() & " | Disewa " & s("active").ToString() & " | Terlambat " & s("late").ToString() & Environment.NewLine & "Dikembalikan " & s("returned").ToString() & " | Dibatalkan " & s("cancelled").ToString() & " | Hangus " & s("expired").ToString() & Environment.NewLine & "Nilai sewa pada tabel adalah tagihan, bukan penerimaan kas."
        End Function
        Private Async Sub StartExport()
            If busy OrElse loaded Is Nothing OrElse dirty Then Return
            _PendingLoad = ExportAsync() : Await PendingLoad
        End Sub
        Private Async Function ExportAsync() As Task
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    Dim result = Await repository.ReadReportAsync(lastQuery, 0, 10000, request.Token)
                    request.Token.ThrowIfCancellationRequested()
                    If IsDisposed Then Return
                    Try
                        _LastExportPath = ReportFiles.ExportCsv(lastQuery, result)
                    Catch ex As UnauthorizedAccessException
                        status.ForeColor = Theme.Maintenance : status.Text = "File laporan belum dapat disimpan. Periksa akses folder Laporan." : Return
                    End Try
                    status.ForeColor = Theme.Muted : status.Text = "Seluruh hasil filter diekspor: " & LastExportPath
                Catch ex As Exception When Expected(ex) OrElse TypeOf ex Is IO.IOException OrElse TypeOf ex Is InvalidOperationException
                    ShowError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
        End Function
        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is KeyNotFoundException
        End Function
        Private Sub ShowError(ex As Exception)
            If IsDisposed Then Return
            status.ForeColor = Theme.Maintenance
            status.Text = If(TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException, ex.Message, "Laporan belum dapat dimuat atau permintaan dibatalkan. Data sebelumnya tetap ditampilkan bila ada.")
            If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
        End Sub
        Private Sub SetBusy(value As Boolean)
            busy = value : filters.Enabled = Not value : cancel.Visible = value
            exportButton.Enabled = Not value AndAlso loaded IsNot Nothing AndAlso Not dirty
            previous.Enabled = exportButton.Enabled AndAlso offset > 0
            following.Enabled = exportButton.Enabled AndAlso offset + If(loaded Is Nothing, 0, loaded.Rows.Count) < If(loaded Is Nothing, 0L, loaded.Total)
            statusBox.Enabled = kindBox.SelectedIndex = 1
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                activeRequest?.Cancel() : TryCast(grid.DataSource, DataTable)?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
