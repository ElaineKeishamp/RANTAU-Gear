Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Models
Imports RantauGear.UI
Namespace Forms
    Public NotInheritable Class BookingChangeDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly details As BookingChangeDetails
        Private ReadOnly action As BookingChangeAction
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(18, 10, 18, 0)}
        Private ReadOnly due As New DateTimePicker With {.Dock = DockStyle.Fill, .Format = DateTimePickerFormat.Custom, .CustomFormat = "dd/MM/yyyy HH:mm", .ShowUpDown = True, .AccessibleName = "Jadwal kembali baru WIB"}
        Private ReadOnly reasonBox As New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .MaxLength = 500, .AccessibleName = "Alasan pembatalan pelanggan"}
        Private ReadOnly methodBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 140, .AccessibleName = "Metode tambahan sewa"}
        Private ReadOnly referenceBox As New TextBox With {.Width = 220, .MaxLength = 200, .AccessibleName = "Referensi pembayaran"}
        Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 8, 18, 0), .ForeColor = Theme.Text}
        Private ReadOnly confirmed As New CheckBox With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0)}
        Private ReadOnly previewButton As New Button With {.Text = "Hitung biaya", .Width = 145, .Height = 38}
        Private ReadOnly saveButton As New RoundedButton With {.Width = 195, .Height = 38, .Enabled = False}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 95, .Height = 38}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0), .ForeColor = Theme.Muted}
        Private quote As ExtensionQuote
        Private frozenQuote As ExtensionQuote
        Private frozenReason As String
        Private frozenMethod As String
        Private frozenReference As String
        Private requestId As Guid
        Private busy As Boolean
        Private stale As Boolean
        Private uncertain As Boolean
        Private activeRequest As CancellationTokenSource
        Public ReadOnly Property PendingOperation As Task = Task.CompletedTask
        Public ReadOnly Property Receipt As PickupReceipt
        Public ReadOnly Property SessionExpired As Boolean
        Private Shared ReadOnly Culture As CultureInfo = CultureInfo.GetCultureInfo("id-ID")
        Public Shared Function Caption(action As BookingChangeAction) As String
            Select Case action
                Case BookingChangeAction.Extend : Return "Perpanjangan"
                Case BookingChangeAction.Cancel : Return "Pembatalan"
                Case BookingChangeAction.Expire : Return "Catat hangus"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(action))
            End Select
        End Function
        Public Sub New(repository As IRentalRepository, details As BookingChangeDetails, action As BookingChangeAction)
            Me.repository = repository : Me.details = details : Me.action = action
            If action = BookingChangeAction.Extend AndAlso (details.State <> "active" OrElse details.ServerNow > details.DueAt) Then Throw New InvalidOperationException("Perpanjangan hanya untuk sewa aktif sebelum jatuh tempo. Sewa terlambat diselesaikan lewat pengembalian.")
            If action <> BookingChangeAction.Extend AndAlso details.State <> "reserved" Then Throw New InvalidOperationException("Tindakan ini hanya untuk reservasi yang belum diambil. Muat ulang Booking.")
            If action = BookingChangeAction.Cancel AndAlso details.ServerNow > details.Deadline Then Throw New InvalidOperationException("Batas ambil sudah lewat. Gunakan Catat hangus; DP tetap tercatat.")
            If action = BookingChangeAction.Expire AndAlso details.ServerNow <= details.Deadline Then Throw New InvalidOperationException("Batas 12 jam sejak jadwal ambil belum lewat. Reservasi belum boleh dihanguskan.")
            Text = Caption(action) & " | RANTAU Gear"
            Size = New Size(820, 730) : MinimumSize = New Size(760, 660)
            StartPosition = FormStartPosition.CenterParent : AutoScaleMode = AutoScaleMode.Dpi
            Font = Theme.UiFont : BackColor = Theme.Background : MinimizeBox = False : MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 135))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 96))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout) : layout.Controls.Add(fields, 0, 0)
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 90))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, If(action = BookingChangeAction.Extend, 64, 0)))
            fields.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, If(action = BookingChangeAction.Extend, 54, 0)))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
            fields.Controls.Add(New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Text, .Text = details.Number & " | " & details.CustomerName & Environment.NewLine & "Ambil: " & Wib(details.PickupAt) & Environment.NewLine & "Kembali: " & Wib(details.DueAt)}, 0, 0)
            due.Value = details.DueAt.ToOffset(TimeSpan.FromHours(7)).DateTime.AddDays(1)
            Dim datePanel As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .Visible = action = BookingChangeAction.Extend}
            datePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190)) : datePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            datePanel.Controls.Add(New Label With {.Text = "Kembali baru (WIB)", .Dock = DockStyle.Fill, .ForeColor = Theme.Text}, 0, 0) : datePanel.Controls.Add(due, 1, 0)
            fields.Controls.Add(datePanel, 0, 1)
            reasonBox.Visible = action = BookingChangeAction.Cancel
            fields.Controls.Add(reasonBox, 0, 2)
            Dim payment As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True, .Visible = action = BookingChangeAction.Extend}
            methodBox.Items.AddRange({"Tunai", "Transfer"}) : methodBox.SelectedIndex = 0
            payment.Controls.Add(New Label With {.Text = "Metode", .AutoSize = True}) : payment.Controls.Add(methodBox)
            payment.Controls.Add(New Label With {.Text = "Referensi", .AutoSize = True}) : payment.Controls.Add(referenceBox)
            fields.Controls.Add(payment, 0, 3)
            fields.Controls.Add(New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted, .Text = If(action = BookingChangeAction.Cancel, "Isi alasan pembatalan dari pelanggan pada kotak di atas.", If(action = BookingChangeAction.Expire, "Status hangus tampil otomatis; tombol ini mencatat riwayat hangus.", "Tarif mengikuti snapshot sewa; stok diperiksa kembali saat simpan."))}, 0, 4)
            Select Case action
                Case BookingChangeAction.Extend
                    summary.Text = "Pilih jadwal kembali baru, lalu klik Hitung biaya. Deposit yang telah diterima tidak ditambah."
                    confirmed.Text = "Hitung biaya sebelum mengonfirmasi pembayaran" : confirmed.Enabled = False
                    saveButton.Text = "Simpan perpanjangan"
                Case BookingChangeAction.Cancel
                    summary.Text = "DP diterima: " & Money(details.DpReceived) & Environment.NewLine & "DP tidak dikembalikan. Alat dilepas dari reservasi setelah pembatalan tercatat." & Environment.NewLine & "Riwayat pemesanan, pembayaran dan alasan pembatalan tetap disimpan."
                    confirmed.Text = "Pelanggan meminta pembatalan dan DP tidak dikembalikan"
                    saveButton.Text = "Catat pembatalan"
                Case BookingChangeAction.Expire
                    summary.Text = "Batas ambil: " & Wib(details.Deadline) & Environment.NewLine & "DP " & Money(details.DpReceived) & " hangus dan tetap tercatat. Alat tersedia untuk reservasi lain." & Environment.NewLine & "Kabar terlambat dari pelanggan tidak menggeser batas 12 jam."
                    confirmed.Text = "Batas 12 jam sejak jadwal ambil sudah lewat dan alat belum diambil"
                    saveButton.Text = "Catat hangus"
            End Select
            layout.Controls.Add(summary, 0, 1) : layout.Controls.Add(confirmed, 0, 2) : layout.Controls.Add(message, 0, 3)
            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
            For Each button As Button In {closeButton, saveButton, previewButton}
                button.Cursor = Cursors.Hand : buttons.Controls.Add(button)
            Next
            previewButton.Visible = action = BookingChangeAction.Extend
            layout.Controls.Add(buttons, 0, 4) : CancelButton = closeButton
            message.Text = "Periksa rincian sebelum menyimpan. Tindakan yang sudah terjadi tidak dihapus dari riwayat."
            AddHandler due.ValueChanged, Sub() InvalidateQuote()
            AddHandler methodBox.SelectedIndexChanged, Sub() InvalidateQuote()
            AddHandler referenceBox.TextChanged, Sub() InvalidateQuote()
            AddHandler reasonBox.TextChanged, Sub() InvalidateQuote()
            AddHandler confirmed.CheckedChanged, Sub() UpdateSave()
            AddHandler previewButton.Click, Sub() StartPreview()
            AddHandler saveButton.Click, Sub() StartSave()
            AddHandler closeButton.Click, Sub() Close()
        End Sub
        Private Shared Function Money(value As Decimal) As String
            Return value.ToString("C0", Culture)
        End Function
        Private Shared Function Wib(value As DateTimeOffset) As String
            Return value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm") & " WIB"
        End Function
        Private Sub InvalidateQuote()
            If uncertain Then Return
            quote = Nothing : confirmed.Checked = False
            If action = BookingChangeAction.Extend Then
                confirmed.Enabled = False : summary.Text = "Pilihan berubah. Klik Hitung biaya sebelum menerima tambahan pembayaran."
            End If
            message.ForeColor = Theme.Muted : message.Text = "Rincian berubah. Periksa kembali sebelum mengonfirmasi."
            UpdateSave()
        End Sub
        Private Sub UpdateSave()
            saveButton.Enabled = Not busy AndAlso Not stale AndAlso (uncertain OrElse (confirmed.Checked AndAlso (action <> BookingChangeAction.Extend OrElse quote IsNot Nothing)))
        End Sub
        Private Async Sub StartPreview()
            If busy OrElse stale Then Return
            _PendingOperation = PreviewAsync() : Await PendingOperation
        End Sub
        Private Async Function PreviewAsync() As Task
            InvalidateQuote() : SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    Dim newDue As New DateTimeOffset(DateTime.SpecifyKind(due.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(7))
                    quote = Await repository.PreviewExtensionAsync(details.Id, newDue, request.Token)
                    summary.Text = "Kembali baru: " & Wib(quote.NewDue) & Environment.NewLine & "Total durasi: " & quote.Days.ToString() & " hari | Total sewa: " & Money(quote.RentAmount) & Environment.NewLine & "Tambahan diterima sekarang: " & Money(quote.Additional) & Environment.NewLine & "Deposit tetap: " & Money(quote.Deposit)
                    confirmed.Text = If(quote.Additional > 0, "Tambahan sewa " & Money(quote.Additional) & " sudah diterima", "Perpanjangan tanpa tambahan biaya sudah disetujui pelanggan")
                    message.Text = "Hasil cek stok belum menahan stok. Server memeriksa ulang saat simpan."
                Catch ex As Exception When Expected(ex)
                    quote = Nothing : ShowError(ex)
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Async Sub StartSave()
            If busy OrElse stale OrElse (Not uncertain AndAlso Not confirmed.Checked) Then Return
            _PendingOperation = SaveAsync() : Await PendingOperation
        End Sub
        Private Async Function SaveAsync() As Task
            If Not uncertain Then
                If action = BookingChangeAction.Extend AndAlso quote Is Nothing Then Return
                If action = BookingChangeAction.Cancel AndAlso String.IsNullOrWhiteSpace(reasonBox.Text) Then
                    message.ForeColor = Theme.Maintenance : message.Text = "Alasan pembatalan wajib diisi." : Return
                End If
                requestId = Guid.NewGuid() : frozenQuote = quote : frozenReason = reasonBox.Text.Trim()
                frozenMethod = If(methodBox.SelectedIndex = 0, "cash", "transfer") : frozenReference = referenceBox.Text.Trim()
            End If
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    _Receipt = Await repository.ChangeBookingAsync(requestId, details.Id, action, frozenQuote, frozenReason, frozenMethod, frozenReference, request.Token)
                    busy = False : DialogResult = DialogResult.OK : Close()
                Catch ex As BookingChangeUncertainException
                    uncertain = True : message.ForeColor = Theme.Maintenance : message.Text = ex.Message
                Catch ex As Exception When Expected(ex)
                    uncertain = False : confirmed.Checked = False : ShowError(ex)
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
        End Function
        Private Sub ShowError(ex As Exception)
            If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then
                _SessionExpired = True : busy = False : Close() : Return
            End If
            message.ForeColor = Theme.Maintenance
            message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Permintaan dibatalkan atau layanan belum dapat dihubungi. Periksa koneksi dan coba kembali.")
            If TypeOf ex Is SupabaseRequestException Then
                Dim code = DirectCast(ex, SupabaseRequestException).ErrorCode
                stale = {"RG_CHANGE_STATE", "RG_CHANGE_LATE", "RG_CHANGE_QUOTE", "RG_CHANGE_EXPIRED", "RG_CHANGE_REQUEST"}.Contains(code)
            End If
        End Sub
        Private Sub SetBusy(value As Boolean)
            busy = value
            If IsDisposed Then Return
            fields.Enabled = Not value AndAlso Not uncertain AndAlso Not stale
            confirmed.Enabled = fields.Enabled AndAlso (action <> BookingChangeAction.Extend OrElse quote IsNot Nothing)
            previewButton.Enabled = fields.Enabled
            If uncertain Then saveButton.Text = "Periksa simpan"
            closeButton.Text = If(value, "Batalkan", "Tutup") : UpdateSave()
        End Sub
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If busy Then
                e.Cancel = True : activeRequest?.Cancel()
            End If
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
