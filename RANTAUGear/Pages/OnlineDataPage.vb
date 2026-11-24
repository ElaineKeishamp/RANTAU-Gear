Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.UI

Namespace Pages
    Public NotInheritable Class OnlineDataPage
        Inherits UserControl
        Implements IWorkspacePage

        Private ReadOnly repository As IRentalRepository
        Private ReadOnly section As String
        Private ReadOnly description As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly empty As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted, .TextAlign = ContentAlignment.MiddleCenter}
        Private ReadOnly reload As New RoundedButton With {.Text = "Muat ulang", .Width = 120, .Height = 36, .CornerRadius = 6}
        Private ReadOnly first As New Button With {.Text = "Awal", .Width = 65, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly previous As New Button With {.Text = "Sebelumnya", .Width = 100, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly following As New Button With {.Text = "Berikutnya", .Width = 100, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly cancel As New Button With {.Text = "Batalkan", .Width = 95, .Height = 36, .Visible = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#FEE2E4"), .ForeColor = ColorTranslator.FromHtml("#DC2626"), .Cursor = Cursors.Hand}
        Private activeRequest As CancellationTokenSource
        Private loaded As RepositoryPage
        Private loading As Boolean
        Private Const PageSize As Integer = 25
        Public Event RecordSelectionChanged As EventHandler
        Public ReadOnly Property SelectedRecordId As Long?
            Get
                If loading OrElse grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("RecordId") Then Return Nothing
                Dim value = grid.CurrentRow.Cells("RecordId").Value
                If value Is Nothing OrElse value Is DBNull.Value Then Return Nothing
                Return CLng(value)
            End Get
        End Property
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task = Task.CompletedTask Implements IWorkspacePage.PendingLoad

        Public Sub New(repository As IRentalRepository, section As String)
            Me.repository = repository
            Me.section = section
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dock = DockStyle.Fill
            Padding = New Padding(28, 18, 28, 24)
            BackColor = Theme.Background
            Controls.Add(layout)
            description.Text = "Data akan dibaca dari Supabase."
            layout.Controls.Add(description, 0, 0)

            ' Search dan filter toolbar
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Padding = New Padding(0, 6, 0, 0)}
            For Each button As Button In New Button() {reload, first, previous, following, cancel}
                button.Cursor = Cursors.Hand
                toolbar.Controls.Add(button)
            Next
            layout.Controls.Add(toolbar, 0, 1)
            layout.Controls.Add(status, 0, 2)
            UiHelpers.StyleGrid(grid)
            UiHelpers.ApplyColumnAlignments(grid)
            layout.Controls.Add(grid, 0, 3)
            layout.Controls.Add(empty, 0, 3)
            grid.Visible = False
            empty.Text = "Memuat data..."
            AddHandler grid.SelectionChanged, Sub() RaiseEvent RecordSelectionChanged(Me, EventArgs.Empty)
            AddHandler reload.Click, Async Sub() Await LoadPageAsync(If(loaded Is Nothing, 0, loaded.Offset))
            AddHandler first.Click, Async Sub() Await LoadPageAsync(0)
            AddHandler previous.Click, Async Sub() Await LoadPageAsync(Math.Max(0, loaded.Offset - PageSize))
            AddHandler following.Click, Async Sub() Await LoadPageAsync(loaded.Offset + PageSize)
            AddHandler cancel.Click, Sub() activeRequest?.Cancel()
            UiHelpers.FinishPage(Me)
            SetBusy(False)
        End Sub

        Protected Overrides Async Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            Await LoadPageAsync(0)
        End Sub

        Public Function LoadPageAsync(offset As Integer) As Task
            If loading OrElse IsDisposed Then Return Task.CompletedTask
            _PendingLoad = LoadCoreAsync(offset)
            Return PendingLoad
        End Function

        Private Async Function LoadCoreAsync(offset As Integer) As Task
            If loading OrElse IsDisposed Then Return
            Using request As New CancellationTokenSource()
                activeRequest = request
                SetBusy(True)
                status.ForeColor = Theme.Muted
                status.Text = "Mengambil data dari Supabase..."
                Try
                    Dim result = Await repository.ReadPageAsync(section, offset, PageSize, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then
                        result.Rows.Dispose()
                        Return
                    End If
                    Dim old = loaded
                    loaded = result
                    grid.DataSource = result.Rows
                    If grid.Columns.Contains("RecordId") Then grid.Columns("RecordId").Visible = False
                    old?.Rows.Dispose()
                    For Each column As DataGridViewColumn In grid.Columns
                        If (section = "booking" OrElse section = "return-qc") AndAlso column.Name <> "RecordId" Then
                            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
                            column.MinimumWidth = If(column.ValueType Is GetType(DateTime), 240, If(column.ValueType Is GetType(Decimal), 170, If(column.Name = "Status", 120, 175)))
                        End If
                        If column.ValueType Is GetType(Decimal) Then
                            column.DefaultCellStyle.Format = "C0"
                            column.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("id-ID")
                        ElseIf column.ValueType Is GetType(DateTime) Then
                            column.DefaultCellStyle.Format = "dd MMM yyyy HH:mm"
                        End If
                    Next
                    description.Text = result.Description
                    grid.Visible = result.Rows.Rows.Count > 0
                    empty.Visible = Not grid.Visible
                    empty.Text = "Belum ada data di Supabase untuk halaman ini."
                    Dim start = If(result.Rows.Rows.Count = 0, 0, result.Offset + 1)
                    status.Text = start.ToString() & " sampai " & If(result.Rows.Rows.Count = 0, 0, result.Offset + result.Rows.Rows.Count).ToString() & " dari " & result.TotalRows.ToString() & " data" & Environment.NewLine &
                        "Diperbarui " & result.LoadedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd MMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("id-ID")) & " WIB"
                Catch ex As OperationCanceledException
                    If Not IsDisposed Then ShowError(If(request.IsCancellationRequested, "Pemuatan dibatalkan. Klik Muat ulang untuk mencoba lagi.", "Koneksi melewati batas waktu. Klik Muat ulang untuk mencoba lagi."))
                Catch ex As UnauthorizedAccessException
                    If Not IsDisposed Then
                        ShowError(ex.Message)
                        RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End If
                Catch ex As RantauGear.Data.Supabase.SupabaseRequestException
                    If Not IsDisposed Then ShowError(ex.Message & " Klik Muat ulang untuk mencoba lagi.")
                Catch ex As HttpRequestException
                    If Not IsDisposed Then ShowError("Tidak dapat menghubungi Supabase. Periksa internet, lalu klik Muat ulang.")
                Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException OrElse TypeOf ex Is FormatException OrElse TypeOf ex Is KeyNotFoundException
                    If Not IsDisposed Then ShowError("Data layanan belum dapat dibaca. Klik Muat ulang atau hubungi administrator.")
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
        End Function

        Private Sub ShowError(message As String)
            status.ForeColor = Theme.Maintenance
            status.Text = message & If(loaded Is Nothing, String.Empty, Environment.NewLine & "Data sebelumnya tetap ditampilkan.")
            If loaded Is Nothing Then empty.Text = "Data belum dapat ditampilkan."
        End Sub

        Private Sub SetBusy(value As Boolean)
            loading = value
            RaiseEvent RecordSelectionChanged(Me, EventArgs.Empty)
            reload.Enabled = Not value
            first.Enabled = Not value AndAlso loaded IsNot Nothing AndAlso loaded.Offset > 0
            previous.Enabled = first.Enabled
            following.Enabled = Not value AndAlso loaded IsNot Nothing AndAlso loaded.Offset <= Integer.MaxValue - PageSize AndAlso loaded.Offset + loaded.Rows.Rows.Count < loaded.TotalRows
            cancel.Visible = value
        End Sub

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = Keys.Escape AndAlso loading Then
                activeRequest?.Cancel()
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                activeRequest?.Cancel()
                loaded?.Rows.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
