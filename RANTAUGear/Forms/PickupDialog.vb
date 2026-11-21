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
    Public NotInheritable Class PickupDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly details As PickupDetails
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(18, 10, 18, 0)}
        Private ReadOnly unitsGrid As New DataGridView With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .BackgroundColor = Color.White}
        Private ReadOnly methodBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 140, .AccessibleName = "Metode pelunasan dan deposit"}
        Private ReadOnly referenceBox As New TextBox With {.Width = 260, .MaxLength = 200, .AccessibleName = "Referensi pembayaran"}
        Private ReadOnly moneyConfirmed As New CheckBox With {.Text = "Pelunasan sisa sewa dan deposit sesuai ringkasan sudah diterima", .Dock = DockStyle.Fill}
        Private ReadOnly unitsConfirmed As New CheckBox With {.Text = "Unit yang dipilih sudah diperiksa dan siap diserahkan", .Dock = DockStyle.Fill}
        Private ReadOnly saveButton As New RoundedButton With {.Text = "Simpan pengambilan", .Width = 205, .Height = 38, .Enabled = False}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 100, .Height = 38}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0), .ForeColor = Theme.Muted}
        Private activeRequest As CancellationTokenSource
        Private busy As Boolean
        Private uncertain As Boolean
        Private stale As Boolean
        Private requestId As Guid
        Private selectedUnits As List(Of Long)
        Private selectedMethod As String
        Private selectedReference As String
        Public ReadOnly Property PendingOperation As Task = Task.CompletedTask
        Public ReadOnly Property Receipt As PickupReceipt
        Public ReadOnly Property SessionExpired As Boolean
        Public Sub New(repository As IRentalRepository, details As PickupDetails)
            Me.repository = repository : Me.details = details
            Text = "Pengambilan alat | RANTAU Gear"
            Size = New Size(900, 850) : MinimumSize = New Size(800, 730)
            Font = Theme.UiFont : BackColor = Theme.Background
            StartPosition = FormStartPosition.CenterParent : AutoScaleMode = AutoScaleMode.Dpi
            MinimizeBox = False : MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 112))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 80))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout) : layout.Controls.Add(fields, 0, 0)
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 76))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 78))
            fields.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            fields.Controls.Add(New Label With {.Text = details.Number & " | " & details.CustomerName & Environment.NewLine & "Kembali: " & Wib(details.DueAt) & " | Batas ambil: " & Wib(details.Deadline), .Dock = DockStyle.Fill, .ForeColor = Theme.Text}, 0, 0)
            Dim needed = String.Join(Environment.NewLine, details.Lines.Select(Function(line) line.Name & ": perlu " & line.Quantity.ToString() & ", unit siap tersedia " & details.Units.Where(Function(unit) unit.TypeId = line.TypeId).Count().ToString()))
            fields.Controls.Add(New TextBox With {.Text = needed, .TabStop = False, .ReadOnly = True, .Multiline = True, .ScrollBars = ScrollBars.Vertical, .Dock = DockStyle.Fill, .BackColor = Theme.Background, .AccessibleName = "Kebutuhan unit sesuai reservasi"}, 0, 1)
            unitsGrid.Columns.Add(New DataGridViewCheckBoxColumn With {.Name = "Pilih", .HeaderText = "Pilih", .FillWeight = 15})
            unitsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Id", .Visible = False})
            unitsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Kode", .HeaderText = "Kode unit", .ReadOnly = True, .FillWeight = 30})
            unitsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Jenis", .HeaderText = "Jenis alat", .ReadOnly = True, .FillWeight = 55})
            For Each unit In details.Units
                Dim line = details.Lines.Single(Function(x) x.TypeId = unit.TypeId)
                unitsGrid.Rows.Add(False, unit.Id, unit.Code, line.Name)
            Next
            fields.Controls.Add(unitsGrid, 0, 2)
            fields.Controls.Add(New Label With {.Text = "Centang unit sesuai reservasi. Server memeriksa ulang saat simpan.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 0, 3)
            Dim method As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
            method.Controls.Add(New Label With {.Text = "Pembayaran", .AutoSize = True})
            methodBox.Items.AddRange({"Tunai", "Transfer"}) : methodBox.SelectedIndex = 0
            method.Controls.Add(methodBox)
            method.Controls.Add(New Label With {.Text = "Referensi", .AutoSize = True}) : method.Controls.Add(referenceBox)
            fields.Controls.Add(method, 0, 4)
            Dim culture = CultureInfo.GetCultureInfo("id-ID")
            Dim summary As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 8, 18, 0), .ForeColor = Theme.Text, .AccessibleName = "Ringkasan penerimaan uang", .Text = "Sewa: " & details.RentAmount.ToString("C0", culture) & " | Sewa sudah dibayar: " & details.RentPaid.ToString("C0", culture) & Environment.NewLine & "Sisa sewa: " & details.RentBalance.ToString("C0", culture) & " | Deposit sekarang: " & details.DepositBalance.ToString("C0", culture) & Environment.NewLine & "Total diterima sekarang: " & (details.RentBalance + details.DepositBalance).ToString("C0", culture) & ". Deposit dicatat terpisah dari pendapatan sewa."}
            layout.Controls.Add(summary, 0, 1)
            moneyConfirmed.Padding = New Padding(18, 0, 18, 0) : unitsConfirmed.Padding = moneyConfirmed.Padding
            layout.Controls.Add(moneyConfirmed, 0, 2) : layout.Controls.Add(unitsConfirmed, 0, 3)
            layout.Controls.Add(message, 0, 4)
            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
            buttons.Controls.Add(closeButton) : buttons.Controls.Add(saveButton)
            closeButton.Cursor = Cursors.Hand : saveButton.Cursor = Cursors.Hand
            layout.Controls.Add(buttons, 0, 5)
            CancelButton = closeButton
            message.Text = If(details.Units.Count = 0, "Belum ada unit siap untuk reservasi ini. Tutup form dan periksa stok/kondisi unit.", "Simpan setelah uang diterima dan alat siap diserahkan. Sewa akan menjadi aktif.")
            AddHandler moneyConfirmed.CheckedChanged, Sub() UpdateSave()
            AddHandler unitsConfirmed.CheckedChanged, Sub() UpdateSave()
            AddHandler unitsGrid.CurrentCellDirtyStateChanged, Sub()
                                                                 If unitsGrid.IsCurrentCellDirty Then unitsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                             End Sub
            AddHandler unitsGrid.CellValueChanged, Sub() InvalidateConfirmation()
            AddHandler methodBox.SelectedIndexChanged, Sub() InvalidateConfirmation()
            AddHandler referenceBox.TextChanged, Sub() InvalidateConfirmation()
            AddHandler saveButton.Click, Sub() StartSave()
            AddHandler closeButton.Click, Sub() Close()
        End Sub
        Private Shared Function Wib(value As DateTimeOffset) As String
            Return value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm") & " WIB"
        End Function
        Private Sub InvalidateConfirmation()
            If uncertain Then Return
            moneyConfirmed.Checked = False : unitsConfirmed.Checked = False
            message.ForeColor = Theme.Muted
            message.Text = "Pilihan berubah. Periksa jumlah unit, lalu konfirmasi kembali pembayaran dan pemeriksaan."
            UpdateSave()
        End Sub
        Private Sub UpdateSave()
            saveButton.Enabled = Not busy AndAlso Not stale AndAlso (uncertain OrElse (moneyConfirmed.Checked AndAlso unitsConfirmed.Checked))
        End Sub
        Private Function ReadUnits() As List(Of Long)
            unitsGrid.EndEdit()
            Dim ids As New List(Of Long)()
            For Each row As DataGridViewRow In unitsGrid.Rows
                If Convert.ToBoolean(row.Cells("Pilih").Value) Then ids.Add(CLng(row.Cells("Id").Value))
            Next
            If ids.Count = 0 OrElse ids.Distinct().Count() <> ids.Count Then Throw New ArgumentException("Centang unit fisik yang akan diserahkan.")
            Dim chosen = details.Units.Where(Function(unit) ids.Contains(unit.Id)).ToList()
            If chosen.Count <> ids.Count OrElse details.Lines.Any(Function(line) chosen.Where(Function(unit) unit.TypeId = line.TypeId).Count() <> line.Quantity) Then Throw New ArgumentException("Jumlah unit yang dicentang harus sesuai setiap jenis alat pada reservasi.")
            Return ids.OrderBy(Function(id) id).ToList()
        End Function
        Private Async Sub StartSave()
            If busy OrElse stale Then Return
            If Not uncertain AndAlso (Not moneyConfirmed.Checked OrElse Not unitsConfirmed.Checked) Then Return
            _PendingOperation = SaveAsync() : Await PendingOperation
        End Sub
        Private Async Function SaveAsync() As Task
            If Not uncertain Then
                Try
                    selectedUnits = ReadUnits()
                Catch ex As ArgumentException
                    message.ForeColor = Theme.Maintenance : message.Text = ex.Message : Return
                End Try
                requestId = Guid.NewGuid()
                selectedMethod = If(methodBox.SelectedIndex = 0, "cash", "transfer") : selectedReference = referenceBox.Text.Trim()
            End If
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Mencatat pengambilan, pelunasan dan deposit..."
                    _Receipt = Await repository.PickupReservationAsync(requestId, details.Id, selectedUnits, selectedMethod, selectedReference, request.Token)
                    busy = False : DialogResult = DialogResult.OK : Close()
                Catch ex As PickupSaveUncertainException
                    uncertain = True : message.ForeColor = Theme.Maintenance : message.Text = ex.Message
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
                    If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then
                        _SessionExpired = True : busy = False : Close()
                    Else
                        uncertain = False
                        message.ForeColor = Theme.Maintenance
                        message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Permintaan belum dapat dikirim. Periksa koneksi lalu coba kembali.")
                        If TypeOf ex Is SupabaseRequestException Then
                            Dim code = DirectCast(ex, SupabaseRequestException).ErrorCode
                            stale = {"RG_PICKUP_MISSING", "RG_PICKUP_STATE", "RG_PICKUP_WINDOW", "RG_PICKUP_UNITS", "RG_PICKUP_BALANCE"}.Contains(code)
                        End If
                        moneyConfirmed.Checked = False : unitsConfirmed.Checked = False
                    End If
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Sub SetBusy(value As Boolean)
            busy = value
            If IsDisposed Then Return
            fields.Enabled = Not value AndAlso Not uncertain AndAlso Not stale
            moneyConfirmed.Enabled = fields.Enabled : unitsConfirmed.Enabled = fields.Enabled
            saveButton.Text = If(uncertain, "Periksa simpan", "Simpan pengambilan")
            closeButton.Text = If(value, "Batalkan", "Tutup")
            UpdateSave()
        End Sub
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If busy Then
                e.Cancel = True : activeRequest?.Cancel()
            End If
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
