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
Imports RantauGear.UI
Namespace Pages
    Public NotInheritable Class OnlineBookingPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly listing As OnlineDataPage
        Private ReadOnly addButton As New RoundedButton With {.Text = "Reservasi baru", .Width = 160, .Height = 38}
        Private ReadOnly cancelButton As New Button With {.Text = "Batalkan", .Width = 100, .Height = 38, .Visible = False}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private operation As Task = Task.CompletedTask
        Private activeRequest As CancellationTokenSource
        Private busy As Boolean
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task Implements IWorkspacePage.PendingLoad
            Get
                Return Task.WhenAll(operation, listing.PendingLoad)
            End Get
        End Property
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
            Dock = DockStyle.Fill : BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 3, .ColumnCount = 1}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(20, 8, 20, 0)}
            actions.Controls.Add(addButton) : actions.Controls.Add(cancelButton)
            addButton.Cursor = Cursors.Hand : cancelButton.Cursor = Cursors.Hand
            layout.Controls.Add(actions, 0, 0)
            message.Padding = New Padding(20, 0, 20, 0)
            message.Text = "Reservasi mencatat alat per jenis dan DP yang sudah diterima. Pengambilan serta deposit belum diaktifkan."
            layout.Controls.Add(message, 0, 1)
            listing = New OnlineDataPage(repository, "booking")
            AddHandler listing.SessionExpired, Sub() RaiseEvent SessionExpired(Me, EventArgs.Empty)
            layout.Controls.Add(listing, 0, 2) : Controls.Add(layout)
            AddHandler addButton.Click, Sub() StartNew()
            AddHandler cancelButton.Click, Sub() activeRequest?.Cancel()
        End Sub
        Private Async Sub StartNew()
            If busy Then Return
            operation = NewReservationAsync() : Await operation
        End Sub
        Private Async Function NewReservationAsync() As Task
            busy = True : addButton.Enabled = False : cancelButton.Visible = True
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Memuat pelanggan, alat dan paket..."
                    Dim options = Await repository.GetReservationOptionsAsync(request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancelButton.Visible = False
                    Using dialog As New ReservationDialog(repository, options)
                        dialog.ShowDialog(FindForm())
                        Await dialog.PendingOperation
                        If IsDisposed Then Return
                        If dialog.SessionExpired Then
                            RaiseEvent SessionExpired(Me, EventArgs.Empty)
                            Return
                        End If
                        If dialog.Receipt IsNot Nothing Then
                            message.Text = dialog.Receipt.Number & " tersimpan. DP " & dialog.Receipt.DownPayment.ToString("C0", Globalization.CultureInfo.GetCultureInfo("id-ID")) & " tercatat."
                        Else
                            message.Text = "Periksa daftar untuk memastikan reservasi yang sudah tersimpan sebelum membuat ulang."
                        End If
                    End Using
                    Await listing.LoadPageAsync(0)
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
                    If Not IsDisposed Then
                        message.ForeColor = Theme.Maintenance
                        message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Pilihan belum dapat dimuat atau permintaan dibatalkan. Periksa koneksi lalu coba kembali.")
                        If TypeOf ex Is UnauthorizedAccessException Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End If
                Finally
                    activeRequest = Nothing : busy = False
                    If Not IsDisposed Then
                        addButton.Enabled = True : cancelButton.Visible = False
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
