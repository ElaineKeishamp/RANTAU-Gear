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
Imports RantauGear.Forms
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Pages
    Public NotInheritable Class MasterDataPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly lists As New List(Of MasterListPage)()
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task Implements IWorkspacePage.PendingLoad
            Get
                Return Task.WhenAll(lists.Select(Function(list) list.PendingLoad))
            End Get
        End Property
        Public Sub New(repository As IRentalRepository, Optional kind As MasterDataKind? = Nothing)
            Dock = DockStyle.Fill
            BackColor = Theme.Background
            If kind.HasValue Then
                Dim list = CreateList(repository, kind.Value)
                Controls.Add(list)
            Else
                Dim tabs As New TabControl With {.Dock = DockStyle.Fill}
                For Each item In New MasterDataKind() {MasterDataKind.Category, MasterDataKind.EquipmentType, MasterDataKind.EquipmentUnit}
                    Dim tab As New TabPage(Globalization.CultureInfo.GetCultureInfo("id-ID").TextInfo.ToTitleCase(MasterEditDialog.KindName(item)))
                    tab.Controls.Add(CreateList(repository, item))
                    tabs.TabPages.Add(tab)
                Next
                Controls.Add(tabs)
            End If
            UiHelpers.FinishPage(Me)
        End Sub
        Private Function CreateList(repository As IRentalRepository, kind As MasterDataKind) As MasterListPage
            Dim list As New MasterListPage(repository, kind)
            lists.Add(list)
            AddHandler list.SessionExpired, Sub() RaiseEvent SessionExpired(Me, EventArgs.Empty)
            Return list
        End Function
    End Class

    Public NotInheritable Class MasterListPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly kind As MasterDataKind
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly empty As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Theme.Muted}
        Private ReadOnly addButton As New RoundedButton With {.Text = "Tambah", .Width = 110, .Height = 36}
        Private ReadOnly editButton As New Button With {.Text = "Edit pilihan", .Width = 110, .Height = 36}
        Private ReadOnly reload As New Button With {.Text = "Muat ulang", .Width = 110, .Height = 36}
        Private ReadOnly previous As New Button With {.Text = "Sebelumnya", .Width = 100, .Height = 36}
        Private ReadOnly following As New Button With {.Text = "Berikutnya", .Width = 100, .Height = 36}
        Private ReadOnly cancel As New Button With {.Text = "Batalkan", .Width = 95, .Height = 36, .Visible = False}
        Private records As New List(Of MasterRecord)()
        Private offset As Integer
        Private total As Long
        Private busy As Boolean
        Private activeRequest As CancellationTokenSource
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task = Task.CompletedTask Implements IWorkspacePage.PendingLoad

        Public Sub New(repository As IRentalRepository, kind As MasterDataKind)
            Me.repository = repository
            Me.kind = kind
            Dock = DockStyle.Fill
            Padding = New Padding(20, 16, 20, 20)
            BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 90))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(layout)
            layout.Controls.Add(New Label With {.Text = "Kelola " & MasterEditDialog.KindName(kind) & " di Supabase. Data yang sudah dipakai tetap disimpan; gunakan status nonaktif jika tidak digunakan lagi.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 0, 0)
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
            For Each button As Button In New Button() {addButton, editButton, reload, previous, following, cancel}
                button.Cursor = Cursors.Hand
                toolbar.Controls.Add(button)
            Next
            layout.Controls.Add(toolbar, 0, 1)
            layout.Controls.Add(status, 0, 2)
            UiHelpers.StyleGrid(grid)
            layout.Controls.Add(grid, 0, 3)
            layout.Controls.Add(empty, 0, 3)
            grid.Visible = False
            empty.Text = "Memuat data..."
            AddHandler reload.Click, Sub() StartLoad(offset)
            AddHandler previous.Click, Sub() StartLoad(Math.Max(0, offset - 25))
            AddHandler following.Click, Sub() StartLoad(offset + 25)
            AddHandler addButton.Click, Sub() StartEdit(New MasterRecord With {.Kind = kind})
            AddHandler editButton.Click, Sub()
                                            If grid.CurrentRow Is Nothing Then Return
                                            Dim id = CLng(grid.CurrentRow.Cells("Id").Value)
                                            StartEdit(records.Single(Function(record) record.Id.HasValue AndAlso record.Id.Value = id).Copy())
                                        End Sub
            AddHandler cancel.Click, Sub() activeRequest?.Cancel()
            AddHandler grid.SelectionChanged, Sub()
                                                  If Not busy Then editButton.Enabled = grid.CurrentRow IsNot Nothing
                                              End Sub
            SetBusy(False)
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            StartLoad(0)
        End Sub

        Private Async Sub StartLoad(newOffset As Integer)
            If busy OrElse IsDisposed Then Return
            _PendingLoad = LoadAsync(newOffset)
            Await PendingLoad
        End Sub

        Public Async Function LoadAsync(newOffset As Integer) As Task
            If busy OrElse IsDisposed Then Return
            Using request As New CancellationTokenSource()
                activeRequest = request
                SetBusy(True)
                status.ForeColor = Theme.Muted
                status.Text = "Mengambil data dari Supabase..."
                Try
                    Dim result = Await repository.ReadMasterAsync(kind, newOffset, 25, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    records = result.Records
                    offset = result.Offset
                    total = result.Total
                    BindRows()
                    status.Text = If(records.Count = 0, "Belum ada data.", (offset + 1).ToString() & " sampai " & (offset + records.Count).ToString() & " dari " & total.ToString() & " data")
                Catch ex As Exception When Expected(ex)
                    HandleError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
        End Function

        Private Sub BindRows()
            Dim table As New DataTable With {.Locale = Globalization.CultureInfo.GetCultureInfo("id-ID")}
            table.Columns.Add("Id", GetType(Long))
            Select Case kind
                Case MasterDataKind.Category
                    table.Columns.Add("Nama") : table.Columns.Add("Status")
                    For Each item In records
                        table.Rows.Add(item.Id.Value, item.Name, If(item.IsActive, "Aktif", "Nonaktif"))
                    Next
                Case MasterDataKind.EquipmentType
                    table.Columns.Add("Nama") : table.Columns.Add("Kategori") : table.Columns.Add("Tarif / hari", GetType(Decimal)) : table.Columns.Add("Nilai penggantian", GetType(Decimal)) : table.Columns.Add("Status")
                    For Each item In records
                        table.Rows.Add(item.Id.Value, item.Name, item.ParentName, item.DailyRate, item.ReplacementValue, If(item.IsActive, "Aktif", "Nonaktif"))
                    Next
                Case MasterDataKind.EquipmentUnit
                    table.Columns.Add("Kode") : table.Columns.Add("Jenis alat") : table.Columns.Add("Kondisi") : table.Columns.Add("Catatan")
                    For Each item In records
                        table.Rows.Add(item.Id.Value, item.Code, item.ParentName, MasterEditDialog.StateName(item.State), item.Notes)
                    Next
                Case MasterDataKind.Package
                    table.Columns.Add("Nama") : table.Columns.Add("Tarif / hari", GetType(Decimal)) : table.Columns.Add("Isi") : table.Columns.Add("Status")
                    For Each item In records
                        table.Rows.Add(item.Id.Value, item.Name, item.DailyRate, item.Components.Count.ToString() & " jenis alat", If(item.IsActive, "Aktif", "Nonaktif"))
                    Next
                Case MasterDataKind.Customer
                    table.Columns.Add("Nama") : table.Columns.Add("Telepon") : table.Columns.Add("Alamat") : table.Columns.Add("Status")
                    For Each item In records
                        table.Rows.Add(item.Id.Value, item.Name, item.Phone, item.Address, If(item.IsActive, "Aktif", "Nonaktif"))
                    Next
            End Select
            Dim old = TryCast(grid.DataSource, DataTable)
            grid.DataSource = table
            old?.Dispose()
            grid.Columns("Id").Visible = False
            For Each column As DataGridViewColumn In grid.Columns
                If column.ValueType Is GetType(Decimal) Then
                    column.DefaultCellStyle.Format = "C0"
                    column.DefaultCellStyle.FormatProvider = table.Locale
                End If
            Next
            grid.Visible = records.Count > 0
            empty.Visible = Not grid.Visible
            empty.Text = "Belum ada " & MasterEditDialog.KindName(kind) & ". Klik Tambah untuk mencatat data pertama."
        End Sub

        Private Async Sub StartEdit(record As MasterRecord)
            If busy OrElse IsDisposed Then Return
            _PendingLoad = EditAsync(record)
            Await PendingLoad
        End Sub

        Public Async Function EditAsync(record As MasterRecord) As Task
            Dim choices As New List(Of MasterRecord)()
            Using request As New CancellationTokenSource()
                activeRequest = request
                SetBusy(True)
                status.ForeColor = Theme.Muted
                status.Text = "Menyiapkan form..."
                Try
                    Dim lookup As MasterDataKind? = If(kind = MasterDataKind.EquipmentType, CType(MasterDataKind.Category, MasterDataKind?), If(kind = MasterDataKind.EquipmentUnit OrElse kind = MasterDataKind.Package, CType(MasterDataKind.EquipmentType, MasterDataKind?), Nothing))
                    If lookup.HasValue Then
                        Dim nextOffset As Integer
                        Do
                            Dim page = Await repository.ReadMasterAsync(lookup.Value, nextOffset, 100, request.Token)
                            choices.AddRange(page.Records)
                            nextOffset += page.Records.Count
                            If nextOffset >= page.Total OrElse page.Records.Count = 0 Then Exit Do
                        Loop
                    End If
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancel.Visible = False
                    Using dialog As New MasterEditDialog(repository, record, choices)
                        dialog.ShowDialog(FindForm())
                        Await dialog.PendingSave
                        If dialog.SessionExpired Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End Using
                Catch ex As Exception When Expected(ex)
                    HandleError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
            If Not IsDisposed Then Await LoadAsync(offset)
        End Function

        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.IO.InvalidDataException OrElse TypeOf ex Is System.Text.Json.JsonException
        End Function
        Private Sub HandleError(ex As Exception)
            If IsDisposed Then Return
            status.ForeColor = Theme.Maintenance
            If TypeOf ex Is UnauthorizedAccessException Then
                status.Text = ex.Message
                RaiseEvent SessionExpired(Me, EventArgs.Empty)
            ElseIf TypeOf ex Is SupabaseRequestException Then
                status.Text = ex.Message
            ElseIf TypeOf ex Is OperationCanceledException Then
                status.Text = "Pemuatan dibatalkan atau melewati batas waktu. Klik Muat ulang untuk mencoba lagi."
            Else
                status.Text = "Data belum dapat dimuat. Periksa koneksi lalu klik Muat ulang."
            End If
            If records.Count > 0 Then status.Text &= Environment.NewLine & "Data sebelumnya tetap ditampilkan."
        End Sub
        Private Sub SetBusy(value As Boolean)
            busy = value
            addButton.Enabled = Not value
            editButton.Enabled = Not value AndAlso grid.CurrentRow IsNot Nothing
            reload.Enabled = Not value
            previous.Enabled = Not value AndAlso offset > 0
            following.Enabled = Not value AndAlso offset <= Integer.MaxValue - 25 AndAlso offset + records.Count < total
            cancel.Visible = value
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                activeRequest?.Cancel()
                TryCast(grid.DataSource, DataTable)?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
