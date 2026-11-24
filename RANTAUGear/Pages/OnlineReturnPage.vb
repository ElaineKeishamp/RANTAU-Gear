Option Strict On
Option Explicit On
Option Infer On
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
    Public NotInheritable Class OnlineReturnPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly listing As OnlineDataPage
        Private ReadOnly buttons As New List(Of Button)()
        Private ReadOnly cancelButton As New Button With {.Text = "Batalkan", .Width = 95, .Height = 38, .Visible = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#FEE2E4"), .ForeColor = ColorTranslator.FromHtml("#DC2626"), .Cursor = Cursors.Hand}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted, .Padding = New Padding(18, 0, 18, 0), .AutoSize = False, .TextAlign = ContentAlignment.TopLeft}
        Private busy As Boolean
        Private operation As Task = Task.CompletedTask
        Private activeRequest As CancellationTokenSource
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task Implements IWorkspacePage.PendingLoad
            Get
                Return Task.WhenAll(operation, listing.PendingLoad)
            End Get
        End Property
        Public Sub New(repository As IRentalRepository, user As AppUser)
            Me.repository = repository
            Dock = DockStyle.Fill : BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 3, .ColumnCount = 1}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 8, 18, 0)}
            layout.Controls.Add(toolbar, 0, 0) : layout.Controls.Add(message, 0, 1)
            listing = New OnlineDataPage(repository, "return-qc")
            layout.Controls.Add(listing, 0, 2) : Controls.Add(layout)
            If user.Role = UserRole.SuperAdmin OrElse user.Role = UserRole.GudangQc Then AddAction(toolbar, ReturnAction.Inspect)
            If user.Role = UserRole.SuperAdmin Then AddAction(toolbar, ReturnAction.Approve)
            If user.Role = UserRole.SuperAdmin OrElse user.Role = UserRole.FrontOffice Then AddAction(toolbar, ReturnAction.Settle)
            toolbar.Controls.Add(cancelButton)
            message.Text = "Pilih sewa pada tabel. QC seluruh unit, persetujuan tagihan, lalu penyelesaian deposit dilakukan berurutan."
            AddHandler listing.RecordSelectionChanged, Sub() SetButtons()
            AddHandler listing.SessionExpired, Sub() RaiseEvent SessionExpired(Me, EventArgs.Empty)
            AddHandler cancelButton.Click, Sub() activeRequest?.Cancel()
        End Sub
        Private Sub AddAction(toolbar As FlowLayoutPanel, action As ReturnAction)
            Dim button As New Button With {.Text = ReturnWorkflowDialog.Caption(action), .Tag = action, .Width = 185, .Height = 38, .Enabled = False, .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text}
            buttons.Add(button)
            toolbar.Controls.Add(button)
            AddHandler button.Click, Sub() StartAction(action)
        End Sub
        Private Sub SetButtons()
            For Each button In buttons
                button.Enabled = Not busy AndAlso listing.SelectedRecordId.HasValue
            Next
        End Sub
        Private Async Sub StartAction(action As ReturnAction)
            If busy OrElse Not listing.SelectedRecordId.HasValue Then Return
            operation = RunActionAsync(listing.SelectedRecordId.Value, action) : Await operation
        End Sub
        Private Async Function RunActionAsync(rentalId As Long, action As ReturnAction) As Task
            busy = True : SetButtons() : cancelButton.Visible = True
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Memuat rincian pengembalian..."
                    Dim details = Await repository.GetReturnDetailsAsync(rentalId, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancelButton.Visible = False
                    Using dialog As New ReturnWorkflowDialog(repository, details, action)
                        dialog.ShowDialog(FindForm()) : Await dialog.PendingOperation
                        If IsDisposed Then Return
                        If dialog.SessionExpired Then
                            RaiseEvent SessionExpired(Me, EventArgs.Empty) : Return
                        End If
                        message.Text = If(dialog.Receipt Is Nothing, "Form ditutup. Muat ulang dan periksa tahap terbaru sebelum mengulang tindakan.", details.Number & ": " & ReturnWorkflowDialog.Caption(action) & " tercatat. Riwayat tetap disimpan.")
                    End Using
                    Await listing.LoadPageAsync(0)
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is InvalidOperationException
                    If Not IsDisposed Then
                        message.ForeColor = Theme.Maintenance
                        message.Text = If(TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is InvalidOperationException, ex.Message, "Rincian belum dapat dimuat atau permintaan dibatalkan. Periksa koneksi dan coba kembali.")
                        If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End If
                Finally
                    activeRequest = Nothing : busy = False
                    If Not IsDisposed Then
                        SetButtons() : cancelButton.Visible = False
                    End If
                End Try
            End Using
        End Function
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then activeRequest?.Cancel()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
