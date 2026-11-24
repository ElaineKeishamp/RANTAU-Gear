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
    Public NotInheritable Class ReservationDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly options As ReservationOptions
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .Padding = New Padding(18)}
        Private ReadOnly customerBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly packageBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly pickup As New DateTimePicker With {.Dock = DockStyle.Fill, .Format = DateTimePickerFormat.Custom, .CustomFormat = "dd/MM/yyyy HH:mm", .ShowUpDown = True}
        Private ReadOnly due As New DateTimePicker With {.Dock = DockStyle.Fill, .Format = DateTimePickerFormat.Custom, .CustomFormat = "dd/MM/yyyy HH:mm", .ShowUpDown = True}
        Private ReadOnly methodBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly referenceBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 200}
        Private ReadOnly items As New DataGridView With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .BackgroundColor = Theme.Card}
        Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Text}
        Private ReadOnly confirmed As New CheckBox With {.Dock = DockStyle.Fill, .Text = "DP sesuai ringkasan sudah diterima dari pelanggan", .Enabled = False}
        Private ReadOnly previewButton As New Button With {.Text = "Hitung biaya", .Width = 145, .Height = 38, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text}
        Private ReadOnly saveButton As New RoundedButton With {.Text = "Simpan reservasi + DP", .Width = 210, .Height = 38, .Enabled = False, .CornerRadius = 8}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 90, .Height = 38, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#F3F4F6"), .ForeColor = Theme.Text}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Maintenance, .Padding = New Padding(18, 0, 18, 0)}
        Private quote As ReservationQuote
        Private snapshot As ReservationRequest
        Private requestId As Guid
        Private busy As Boolean
        Private uncertain As Boolean
        Private activeRequest As CancellationTokenSource
        Public ReadOnly Property PendingOperation As Task = Task.CompletedTask
        Public ReadOnly Property Receipt As ReservationReceipt
        Public ReadOnly Property SessionExpired As Boolean
        Private Shared ReadOnly Rupiah As CultureInfo = CultureInfo.GetCultureInfo("id-ID")
        Public Sub New(repository As IRentalRepository, options As ReservationOptions)
            Me.repository = repository : Me.options = options
            Text = "Reservasi dan DP | RANTAU Gear"
            Size = New Size(860, 860) : MinimumSize = New Size(760, 680)
            AutoScaleMode = AutoScaleMode.Dpi : StartPosition = FormStartPosition.CenterParent
            Font = Theme.UiFont : BackColor = Theme.Background
            MinimizeBox = False : MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 5, .ColumnCount = 1}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 125))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 82))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout)
            Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
            scroll.Controls.Add(fields) : layout.Controls.Add(scroll, 0, 0)
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For Each customer In options.Customers
                customerBox.Items.Add(customer)
            Next
            packageBox.Items.Add("Pilih alat satuan")
            For Each package In options.Packages
                packageBox.Items.Add(package)
            Next
            packageBox.SelectedIndex = 0
            methodBox.Items.AddRange({"Tunai", "Transfer"}) : methodBox.SelectedIndex = 0
            pickup.Value = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime.AddHours(1)
            due.Value = pickup.Value.AddDays(1)
            AddField("Pelanggan", customerBox, 44)
            AddField("Data pelanggan", New Label With {.Text = "Belum terdaftar? Tutup form, tambahkan di menu Pelanggan, lalu buka reservasi kembali.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 56)
            AddField("Ambil (WIB)", pickup, 44)
            AddField("Kembali (WIB)", due, 44)
            AddField("Pilihan sewa", packageBox, 44)
            items.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "TypeId", .Visible = False})
            items.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Alat", .HeaderText = "Jenis alat", .ReadOnly = True, .FillWeight = 65})
            items.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Tarif", .HeaderText = "Tarif / hari", .ReadOnly = True, .FillWeight = 22})
            items.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Jumlah", .HeaderText = "Jumlah", .FillWeight = 13})
            For Each equipment In options.Types
                items.Rows.Add(equipment.Id.Value, equipment.Name, equipment.DailyRate.ToString("C0", Rupiah), "0")
            Next
            AddField("Alat dan jumlah", items, 180)
            AddField("Pilihan alat", New Label With {.Text = "Alat satuan: isi jumlah yang diperlukan, 0 berarti tidak dipilih. Paket memakai isi tetap satu bundel. Unit fisik dipilih saat pengambilan.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 65)
            AddField("Metode DP", methodBox, 44)
            AddField("Referensi / catatan", referenceBox, 44)
            summary.Padding = New Padding(18, 8, 18, 0)
            summary.AccessibleName = "Ringkasan biaya"
            confirmed.Padding = New Padding(18, 0, 18, 0)
            layout.Controls.Add(summary, 0, 1)
            layout.Controls.Add(confirmed, 0, 2)
            layout.Controls.Add(message, 0, 3)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
            For Each button As Button In {closeButton, saveButton, previewButton}
                button.Cursor = Cursors.Hand : actions.Controls.Add(button)
            Next
            layout.Controls.Add(actions, 0, 4)
            CancelButton = closeButton
            summary.Text = "Pilih pelanggan, alat dan jadwal, lalu klik Hitung biaya."
            If options.Customers.Count = 0 Then message.Text = "Belum ada pelanggan aktif. Tambahkan pelanggan melalui menu Pelanggan terlebih dahulu."
            AddHandler customerBox.SelectedIndexChanged, Sub() InvalidateQuote()
            AddHandler pickup.ValueChanged, Sub() InvalidateQuote()
            AddHandler due.ValueChanged, Sub() InvalidateQuote()
            AddHandler methodBox.SelectedIndexChanged, Sub() InvalidateQuote()
            AddHandler referenceBox.TextChanged, Sub() InvalidateQuote()
            AddHandler items.CellValueChanged, Sub() InvalidateQuote()
            AddHandler items.DataError, Sub(sender, e) e.ThrowException = False
            AddHandler packageBox.SelectedIndexChanged, Sub() SelectPackage()
            AddHandler confirmed.CheckedChanged, Sub() saveButton.Enabled = Not busy AndAlso quote IsNot Nothing AndAlso confirmed.Checked
            AddHandler previewButton.Click, Sub() StartPreview()
            AddHandler saveButton.Click, Sub() StartSave()
            AddHandler closeButton.Click, Sub() Close()
        End Sub
        Private Sub AddField(caption As String, control As Control, height As Integer)
            Dim row = fields.RowCount : fields.RowCount += 1
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
            fields.Controls.Add(New Label With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Theme.Text}, 0, row)
            control.Margin = New Padding(3, 5, 3, 5) : control.AccessibleName = caption
            fields.Controls.Add(control, 1, row)
        End Sub
        Private Sub SelectPackage()
            Dim package = TryCast(packageBox.SelectedItem, MasterRecord)
            items.ReadOnly = package IsNot Nothing
            For Each row As DataGridViewRow In items.Rows
                Dim component = If(package Is Nothing, Nothing, package.Components.FirstOrDefault(Function(x) x.TypeId = CLng(row.Cells("TypeId").Value)))
                row.Cells("Jumlah").Value = If(component Is Nothing, "0", component.Quantity.ToString(CultureInfo.InvariantCulture))
            Next
            InvalidateQuote()
        End Sub
        Private Sub InvalidateQuote()
            If uncertain Then Return
            quote = Nothing : confirmed.Checked = False : confirmed.Enabled = False : saveButton.Enabled = False
            summary.Text = "Pilihan berubah. Klik Hitung biaya untuk memperbarui biaya."
        End Sub
        Private Function ReadRequest() As ReservationRequest
            items.EndEdit()
            Dim customer = TryCast(customerBox.SelectedItem, MasterRecord)
            Dim package = TryCast(packageBox.SelectedItem, MasterRecord)
            Dim result As New ReservationRequest With {.CustomerId = If(customer Is Nothing, 0L, customer.Id.Value), .PackageId = If(package Is Nothing, CType(Nothing, Long?), package.Id), .PickupAt = New DateTimeOffset(DateTime.SpecifyKind(pickup.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(7)), .DueAt = New DateTimeOffset(DateTime.SpecifyKind(due.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(7)), .Method = If(methodBox.SelectedIndex = 0, "cash", "transfer"), .Reference = referenceBox.Text.Trim()}
            If package IsNot Nothing Then
                For Each component In package.Components
                    result.Lines.Add(New MasterPackageItem With {.TypeId = component.TypeId, .Quantity = component.Quantity})
                Next
            Else
                For Each row As DataGridViewRow In items.Rows
                    Dim quantity As Integer
                    If Not Integer.TryParse(Convert.ToString(row.Cells("Jumlah").Value), quantity) OrElse quantity < 0 Then Throw New ArgumentException("Jumlah alat harus berupa bilangan bulat nol atau lebih.")
                    If quantity > 0 Then result.Lines.Add(New MasterPackageItem With {.TypeId = CLng(row.Cells("TypeId").Value), .Quantity = quantity})
                Next
            End If
            result.Validate() : Return result
        End Function
        Private Async Sub StartPreview()
            If busy Then Return
            _PendingOperation = PreviewAsync() : Await PendingOperation
        End Sub
        Private Async Function PreviewAsync() As Task
            Try
                snapshot = ReadRequest()
            Catch ex As ArgumentException
                message.Text = ex.Message : Return
            End Try
            InvalidateQuote() : SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted
                    message.Text = "Menghitung biaya dan memeriksa stok..."
                    quote = Await repository.PreviewReservationAsync(snapshot, request.Token)
                    summary.Text = quote.RentalDays.ToString() & " hari | Sewa " & quote.RentAmount.ToString("C0", Rupiah) & Environment.NewLine & "DP sekarang: " & quote.DownPayment.ToString("C0", Rupiah) & Environment.NewLine & "Saat ambil: sisa sewa + deposit " & (quote.RentAmount - quote.DownPayment + quote.Deposit).ToString("C0", Rupiah) & Environment.NewLine & "Deposit: " & quote.Deposit.ToString("C0", Rupiah) & " | Batas ambil " & quote.Deadline.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM HH:mm") & " WIB"
                    confirmed.Enabled = True
                    message.Text = "Stok diperiksa ulang saat disimpan. DP tidak dikembalikan jika pelanggan membatalkan atau lewat batas ambil."
                Catch ex As Exception When Expected(ex)
                    quote = Nothing : ShowError(ex)
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Async Sub StartSave()
            If busy OrElse quote Is Nothing OrElse Not confirmed.Checked Then Return
            _PendingOperation = SaveAsync() : Await PendingOperation
        End Sub
        Private Async Function SaveAsync() As Task
            If Not uncertain Then requestId = Guid.NewGuid()
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.Text = "Menyimpan reservasi dan DP..."
                    _Receipt = Await repository.ReserveWithDpAsync(requestId, snapshot, quote, request.Token)
                    busy = False : DialogResult = DialogResult.OK : Close()
                Catch ex As ReservationSaveUncertainException
                    uncertain = True : message.ForeColor = Theme.Maintenance : message.Text = ex.Message
                Catch ex As Exception When Expected(ex)
                    ShowError(ex)
                    If Not SessionExpired Then
                        uncertain = False : InvalidateQuote()
                    End If
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
        End Function
        Private Sub ShowError(ex As Exception)
            message.ForeColor = Theme.Maintenance
            If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then
                _SessionExpired = True : busy = False : Close() : Return
            End If
            If TypeOf ex Is SupabaseRequestException Then
                message.Text = ex.Message
            ElseIf TypeOf ex Is OperationCanceledException Then
                message.Text = "Permintaan dibatalkan atau melewati batas waktu."
            Else
                message.Text = "Layanan belum dapat dihubungi atau respons belum lengkap. Periksa koneksi dan coba kembali."
            End If
        End Sub
        Private Sub SetBusy(value As Boolean)
            busy = value
            If IsDisposed Then Return
            confirmed.Enabled = Not value AndAlso Not uncertain AndAlso quote IsNot Nothing
            fields.Enabled = Not value AndAlso Not uncertain
            previewButton.Enabled = Not value AndAlso Not uncertain
            saveButton.Enabled = Not value AndAlso quote IsNot Nothing AndAlso confirmed.Checked
            saveButton.Text = If(uncertain, "Periksa simpan", "Simpan reservasi + DP")
            closeButton.Text = If(value, "Batalkan", "Tutup")
        End Sub
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If busy Then
                e.Cancel = True : activeRequest?.Cancel()
            End If
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
