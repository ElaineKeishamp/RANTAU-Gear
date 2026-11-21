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
    Public NotInheritable Class StaffManagementPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly selfId As Guid
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly addButton As New RoundedButton With {.Text = "Tambah akun", .Width = 140, .Height = 36}
        Private ReadOnly editButton As New Button With {.Text = "Edit akun", .Width = 125, .Height = 36}
        Private ReadOnly passwordButton As New Button With {.Text = "Ubah password", .Width = 160, .Height = 36}
        Private ReadOnly reload As New Button With {.Text = "Muat ulang", .Width = 120, .Height = 36}
        Private ReadOnly previous As New Button With {.Text = "Sebelumnya", .Width = 105, .Height = 36}
        Private ReadOnly following As New Button With {.Text = "Berikutnya", .Width = 105, .Height = 36}
        Private ReadOnly cancel As New Button With {.Text = "Batalkan", .Width = 95, .Height = 36, .Visible = False}
        Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly empty As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Theme.Muted}
        Private rows As New List(Of StaffRecord)()
        Private offset As Integer
        Private total As Long
        Private busy As Boolean
        Private activeRequest As CancellationTokenSource
        Public ReadOnly Property PendingLoad As Task = Task.CompletedTask Implements IWorkspacePage.PendingLoad
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public Sub New(repository As IRentalRepository, selfId As Guid)
            Me.repository = repository : Me.selfId = selfId
            Dock = DockStyle.Fill : Padding = New Padding(18) : BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60)) : layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 104)) : layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70)) : layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(layout)
            layout.Controls.Add(New Label With {.Text = "Kelola akun petugas. Nonaktifkan akses tanpa menghapus riwayat. Akun sendiri tetap Super Admin aktif.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 0, 0)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
            For Each button As Button In {addButton, editButton, passwordButton, reload, previous, following, cancel}
                button.Cursor = Cursors.Hand : actions.Controls.Add(button)
            Next
            layout.Controls.Add(actions, 0, 1) : layout.Controls.Add(status, 0, 2)
            UiHelpers.StyleGrid(grid) : grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            layout.Controls.Add(grid, 0, 3) : layout.Controls.Add(empty, 0, 3)
            grid.Visible = False : empty.Text = "Memuat akun staf..."
            AddHandler grid.SelectionChanged, Sub() UpdateButtons()
            AddHandler reload.Click, Sub() StartLoad(offset)
            AddHandler previous.Click, Sub() StartLoad(Math.Max(0, offset - 25))
            AddHandler following.Click, Sub() StartLoad(offset + 25)
            AddHandler addButton.Click, Sub() StartEdit("create")
            AddHandler editButton.Click, Sub() StartEdit("update")
            AddHandler passwordButton.Click, Sub() StartEdit("password")
            AddHandler cancel.Click, Sub() activeRequest?.Cancel()
        End Sub
        Protected Overrides Async Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e) : _PendingLoad = LoadAsync(0) : Await PendingLoad
        End Sub
        Private Function Selected() As StaffRecord
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim id = DirectCast(grid.CurrentRow.Cells("Id").Value, Guid)
            Return rows.FirstOrDefault(Function(record) record.Id = id)
        End Function
        Private Async Sub StartLoad(newOffset As Integer)
            If busy Then Return
            _PendingLoad = LoadAsync(newOffset) : Await PendingLoad
        End Sub
        Private Async Function LoadAsync(newOffset As Integer) As Task
            busy = True : UpdateButtons()
            Using request As New CancellationTokenSource()
                activeRequest = request : UpdateButtons()
                Try
                    status.ForeColor = Theme.Muted : status.Text = "Memuat akun staf..."
                    Dim result = Await repository.ReadStaffAsync(newOffset, 25, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    rows = result.Rows : total = result.Total : offset = newOffset
                    Dim table As New DataTable()
                    table.Columns.Add("Id", GetType(Guid)) : table.Columns.Add("Nama") : table.Columns.Add("Email") : table.Columns.Add("Peran") : table.Columns.Add("Status")
                    For Each record In rows
                        table.Rows.Add(record.Id, record.DisplayName, record.Email, RoleCaption(record.Role), If(record.IsActive, "Aktif", "Nonaktif"))
                    Next
                    Dim old = TryCast(grid.DataSource, DataTable)
                    grid.DataSource = table : old?.Dispose() : grid.Columns("Id").Visible = False
                    For Each column As DataGridViewColumn In grid.Columns
                        column.MinimumWidth = If(column.Name = "Email", 260, 170)
                    Next
                    grid.Visible = rows.Count > 0 : empty.Visible = Not grid.Visible : empty.Text = "Belum ada akun staf."
                    status.Text = If(rows.Count = 0, "Tidak ada akun.", (offset + 1).ToString() & " sampai " & (offset + rows.Count).ToString() & " dari " & total.ToString() & " akun")
                Catch ex As Exception When Expected(ex)
                    ShowError(ex)
                Finally
                    activeRequest = Nothing : busy = False
                    If Not IsDisposed Then UpdateButtons()
                End Try
            End Using
        End Function
        Private Async Sub StartEdit(operation As String)
            If busy Then Return
            Dim record = If(operation = "create", New StaffRecord(), Selected())
            If record Is Nothing Then Return
            _PendingLoad = EditAsync(record, operation) : Await PendingLoad
        End Sub
        Private Async Function EditAsync(record As StaffRecord, operation As String) As Task
            busy = True : UpdateButtons()
            Try
                Using dialog As New StaffEditorDialog(repository, record, operation, selfId)
                    dialog.ShowDialog(FindForm()) : Await dialog.PendingSave
                    If dialog.SessionExpired Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                End Using
            Finally
                busy = False
                If Not IsDisposed Then UpdateButtons()
            End Try
            If Not IsDisposed Then Await LoadAsync(offset)
        End Function
        Public Shared Function RoleCaption(role As String) As String
            Select Case role
                Case "super_admin" : Return "Super Admin"
                Case "front_office" : Return "Front office"
                Case "warehouse_qc" : Return "Gudang / QC"
                Case Else : Return role
            End Select
        End Function
        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
        End Function
        Private Sub ShowError(ex As Exception)
            If IsDisposed Then Return
            status.ForeColor = Theme.Maintenance : status.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Daftar belum dapat dimuat atau permintaan dibatalkan. Data sebelumnya tetap ditampilkan bila ada.")
            If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
        End Sub
        Private Sub UpdateButtons()
            addButton.Enabled = Not busy : reload.Enabled = Not busy
            Dim selectedRecord = If(grid.DataSource Is Nothing, Nothing, Selected())
            editButton.Enabled = Not busy AndAlso selectedRecord IsNot Nothing : passwordButton.Enabled = editButton.Enabled
            previous.Enabled = Not busy AndAlso offset > 0 : following.Enabled = Not busy AndAlso offset + rows.Count < total
            cancel.Visible = busy AndAlso activeRequest IsNot Nothing
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                activeRequest?.Cancel() : TryCast(grid.DataSource, DataTable)?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
