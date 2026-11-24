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
Imports RantauGear.Services
Imports RantauGear.UI
Namespace Pages
    Public NotInheritable Class OnlineBookingPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly listing As OnlineDataPage
        Private ReadOnly addButton As New RoundedButton With {.Text = "Reservasi baru", .Width = 160, .Height = 38, .CornerRadius = 8}
        Private ReadOnly pickupButton As New Button With {.Text = "Proses pengambilan", .Width = 200, .Height = 38, .Enabled = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = ColorTranslator.FromHtml("#4B5563"), .Cursor = Cursors.Hand}
        Private ReadOnly changeButtons As New List(Of Button)()
        Private ReadOnly cancelButton As New Button With {.Text = "Batalkan", .Width = 100, .Height = 38, .Visible = False, .FlatStyle = FlatStyle.Flat, .BackColor = Color.White, .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted, .AutoSize = False, .TextAlign = ContentAlignment.TopLeft}
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
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 104))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            ' Action buttons card
            Dim actionsCard As New RoundedPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(20, 8, 20, 8), .CornerRadius = 12}
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = False, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True}
            actionsCard.Controls.Add(actions)
            layout.Controls.Add(actionsCard, 0, 0)

            ' Configure action buttons
            addButton.Height = 38
            actions.Controls.Add(addButton)

            pickupButton.Height = 38
            actions.Controls.Add(pickupButton)

            For Each action In {BookingChangeAction.Extend, BookingChangeAction.Cancel, BookingChangeAction.Expire}
                Dim button As New Button With {.Text = BookingChangeDialog.Caption(action), .Tag = action, .Width = 140, .Height = 38, .Enabled = False, .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = ColorTranslator.FromHtml("#4B5563"), .Font = Theme.UiFont}
                changeButtons.Add(button)
                actions.Controls.Add(button)
                Dim chosenAction = action
                AddHandler button.Click, Sub() StartChange(chosenAction)
            Next

            actions.Controls.Add(cancelButton)
            ' Message notification card
            Dim messageCard As New RoundedPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 12, 18, 12), .CornerRadius = 12, .BackColor = Theme.SuccessLight}
            message.Padding = New Padding(0)
            message.Text = "Reservasi mencatat alat per jenis dan DP yang sudah diterima. Pilih reservasi pada tabel untuk memproses pengambilan, pelunasan dan deposit."
            message.ForeColor = Theme.ReadyText
            messageCard.Controls.Add(message)
            layout.Controls.Add(messageCard, 0, 1)
            ' Data listing card
            Dim dataCard As New RoundedPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18), .CornerRadius = 12}
            Dim dataContainer As New Panel With {.Dock = DockStyle.Fill}

            listing = New OnlineDataPage(repository, "booking")
            AddHandler listing.SessionExpired, Sub() RaiseEvent SessionExpired(Me, EventArgs.Empty)
            dataContainer.Controls.Add(listing)
            dataCard.Controls.Add(dataContainer)
            layout.Controls.Add(dataCard, 0, 2)

            Controls.Add(layout)
            AddHandler listing.RecordSelectionChanged, Sub() UpdateActions()
            AddHandler pickupButton.Click, Sub() StartPickup()
            AddHandler addButton.Click, Sub() StartNew()
            AddHandler cancelButton.Click, Sub() activeRequest?.Cancel()
        End Sub
        Private Async Sub StartNew()
            If busy Then Return
            operation = NewReservationAsync() : Await operation
        End Sub
        Private Async Function NewReservationAsync() As Task
            busy = True : UpdateActions() : cancelButton.Visible = True
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
                        UpdateActions() : cancelButton.Visible = False
                    End If
                End Try
            End Using
        End Function
        Private Async Sub StartPickup()
            If busy OrElse Not listing.SelectedRecordId.HasValue Then Return
            operation = PickupAsync(listing.SelectedRecordId.Value) : Await operation
        End Sub
        Private Async Function PickupAsync(rentalId As Long) As Task
            busy = True : UpdateActions() : cancelButton.Visible = True
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Memuat rincian pembayaran dan unit siap..."
                    Dim details = Await repository.GetPickupDetailsAsync(rentalId, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancelButton.Visible = False
                    Using dialog As New PickupDialog(repository, details)
                        dialog.ShowDialog(FindForm()) : Await dialog.PendingOperation
                        If IsDisposed Then Return
                        If dialog.SessionExpired Then
                            RaiseEvent SessionExpired(Me, EventArgs.Empty) : Return
                        End If
                        message.Text = If(dialog.Receipt Is Nothing, "Pengambilan ditutup. Periksa status dan pembayaran pada daftar sebelum memproses ulang.", dialog.Receipt.Number & " berhasil diproses. Pelunasan dan deposit tercatat; muat ulang menampilkan status terbaru.")
                        If dialog.Receipt IsNot Nothing Then OfferReceipt(details, dialog.PaymentMethod, dialog.SavedUnitIds)
                    End Using
                    Await listing.LoadPageAsync(0)
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
                    If Not IsDisposed Then
                        message.ForeColor = Theme.Maintenance
                        message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Rincian belum dapat dimuat atau permintaan dibatalkan. Periksa koneksi lalu coba kembali.")
                        If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End If
                Finally
                    activeRequest = Nothing : busy = False
                    If Not IsDisposed Then
                        UpdateActions() : cancelButton.Visible = False
                    End If
                End Try
            End Using
        End Function
        Private Sub OfferReceipt(details As PickupDetails, method As String, unitIds As List(Of Long))
            If MessageBox.Show(FindForm(), "Pengambilan tersimpan. Cetak nota sewa ke PDF sekarang?", "Cetak nota", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Dim receipt = ReceiptPdfService.FromPickup(details, method, unitIds)
            Using dialog As New SaveFileDialog With {.Title = "Simpan nota sewa", .Filter = "Dokumen PDF (*.pdf)|*.pdf", .DefaultExt = "pdf", .AddExtension = True, .OverwritePrompt = True, .FileName = ReceiptPdfService.SuggestedFileName(receipt)}
                If dialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Try
                    ReceiptPdfService.Save(receipt, dialog.FileName)
                Catch ex As Exception When TypeOf ex Is IO.IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    message.ForeColor = Theme.Maintenance
                    message.Text = "Pengambilan tersimpan, tetapi nota belum dapat disimpan. Pilih folder lain atau tutup PDF yang sedang terbuka."
                    Return
                End Try
                message.ForeColor = Theme.Ready
                message.Text = receipt.Number & " berhasil diproses. Nota tersimpan di " & dialog.FileName
                Try
                    Diagnostics.Process.Start(New Diagnostics.ProcessStartInfo(dialog.FileName) With {.UseShellExecute = True})
                Catch ex As System.ComponentModel.Win32Exception
                    ' Tidak ada aplikasi pembuka PDF; file tetap tersimpan.
                End Try
            End Using
        End Sub
        Private Sub UpdateActions()
            addButton.Enabled = Not busy
            pickupButton.Enabled = Not busy AndAlso listing.SelectedRecordId.HasValue
            For Each button In changeButtons
                button.Enabled = pickupButton.Enabled
            Next
        End Sub
        Private Async Sub StartChange(action As BookingChangeAction)
            If busy OrElse Not listing.SelectedRecordId.HasValue Then Return
            operation = ChangeAsync(listing.SelectedRecordId.Value, action) : Await operation
        End Sub
        Private Async Function ChangeAsync(rentalId As Long, action As BookingChangeAction) As Task
            busy = True : UpdateActions() : cancelButton.Visible = True
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Memuat rincian sewa..."
                    Dim details = Await repository.GetBookingChangeDetailsAsync(rentalId, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancelButton.Visible = False
                    Using dialog As New BookingChangeDialog(repository, details, action)
                        dialog.ShowDialog(FindForm()) : Await dialog.PendingOperation
                        If IsDisposed Then Return
                        If dialog.SessionExpired Then
                            RaiseEvent SessionExpired(Me, EventArgs.Empty) : Return
                        End If
                        message.Text = If(dialog.Receipt Is Nothing, "Form ditutup. Muat ulang dan periksa riwayat sebelum mengulang tindakan.", details.Number & ": " & BookingChangeDialog.Caption(action) & " tercatat. Riwayat transaksi tetap disimpan.")
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
                        UpdateActions() : cancelButton.Visible = False
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
