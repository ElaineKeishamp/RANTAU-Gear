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
    Public NotInheritable Class ReturnWorkflowDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly details As ReturnDetails
        Private ReadOnly action As ReturnAction
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(18, 10, 18, 0)}
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .BackgroundColor = Color.White, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill}
        Private ReadOnly methodBox As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 130, .AccessibleName = "Metode penyelesaian uang"}
        Private ReadOnly referenceBox As New TextBox With {.Width = 250, .MaxLength = 200, .AccessibleName = "Referensi penyelesaian uang"}
        Private ReadOnly confirmed As New CheckBox With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0)}
        Private ReadOnly saveButton As New RoundedButton With {.Width = 210, .Height = 38, .Enabled = False}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 100, .Height = 38}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0), .ForeColor = Theme.Muted}
        Private busy As Boolean
        Private uncertain As Boolean
        Private stale As Boolean
        Private activeRequest As CancellationTokenSource
        Private requestId As Guid
        Private checks As List(Of ReturnCheck)
        Private selectedMethod As String
        Private selectedReference As String
        Public ReadOnly Property PendingOperation As Task = Task.CompletedTask
        Public ReadOnly Property Receipt As PickupReceipt
        Public ReadOnly Property SessionExpired As Boolean
        Private Shared ReadOnly Culture As CultureInfo = CultureInfo.GetCultureInfo("id-ID")
        Public Shared Function Caption(action As ReturnAction) As String
            Select Case action
                Case ReturnAction.Inspect : Return "Pemeriksaan QC"
                Case ReturnAction.Approve : Return "Persetujuan denda"
                Case ReturnAction.Settle : Return "Penyelesaian deposit"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(action))
            End Select
        End Function
        Public Sub New(repository As IRentalRepository, details As ReturnDetails, action As ReturnAction)
            Me.repository = repository : Me.details = details : Me.action = action
            If details.Settled Then Throw New InvalidOperationException("Transaksi sudah selesai. Muat ulang daftar untuk melihat tahap terbaru.")
            If action = ReturnAction.Inspect AndAlso details.State <> "active" Then Throw New InvalidOperationException("Sewa sudah diperiksa. Lanjutkan ke persetujuan admin atau penyelesaian uang.")
            If action = ReturnAction.Approve AndAlso (details.State <> "returned" OrElse details.Approved) Then Throw New InvalidOperationException("Persetujuan membutuhkan hasil QC yang belum disetujui.")
            If action = ReturnAction.Settle AndAlso (details.State <> "returned" OrElse Not details.Approved) Then Throw New InvalidOperationException("Selesaikan QC dan persetujuan admin sebelum menyelesaikan deposit.")
            If action <> ReturnAction.Inspect AndAlso details.Financial Is Nothing Then Throw New InvalidOperationException("Rincian tagihan belum tersedia untuk akun ini.")
            Text = Caption(action) & " | RANTAU Gear"
            Size = New Size(1000, 850) : MinimumSize = New Size(900, 760)
            Font = Theme.UiFont : BackColor = Theme.Background : AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent : MinimizeBox = False : MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 132))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 68))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 88))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout) : layout.Controls.Add(fields, 0, 0)
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 105))
            fields.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            Dim returnedText = If(details.ReturnedAt.HasValue, " | Kembali " & details.ReturnedAt.GetValueOrDefault().ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm") & " WIB", String.Empty)
            fields.Controls.Add(New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Text, .Text = details.Number & " | Jatuh tempo " & details.DueAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm") & " WIB" & returnedText & Environment.NewLine & If(action = ReturnAction.Inspect, "Pilih kondisi setiap unit. Rusak/hilang wajib biaya dan catatan; biaya ini masih usulan untuk admin.", "Periksa hasil QC. Denda terlambat tetap ditambahkan pada biaya rusak/hilang.")}, 0, 0)
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "UnitId", .Visible = False})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Kode", .HeaderText = "Kode unit", .ReadOnly = True, .MinimumWidth = 100})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Alat", .HeaderText = "Alat", .ReadOnly = True, .MinimumWidth = 150})
            grid.Columns.Add(New DataGridViewComboBoxColumn With {.Name = "Kondisi", .HeaderText = "Kondisi", .DataSource = New String() {"Baik", "Rusak", "Hilang"}, .MinimumWidth = 100})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Acuan", .HeaderText = "Acuan ganti", .ReadOnly = True, .MinimumWidth = 135})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Biaya", .HeaderText = "Biaya (Rp)", .MinimumWidth = 115})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Catatan", .HeaderText = "Catatan QC", .MinimumWidth = 200})
            For Each unit In details.Units
                Dim condition As String = Nothing
                Select Case unit.Condition
                    Case "good" : condition = "Baik"
                    Case "damaged" : condition = "Rusak"
                    Case "lost" : condition = "Hilang"
                End Select
                grid.Rows.Add(unit.UnitId, unit.Code, unit.Name, condition, If(unit.ReplacementValue > 0, unit.ReplacementValue.ToString("C0", Culture), "Belum diisi"), unit.Charge.ToString(CultureInfo.InvariantCulture), unit.Notes)
            Next
            grid.ReadOnly = action <> ReturnAction.Inspect
            fields.Controls.Add(grid, 0, 1)
            Dim payment As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True, .Visible = action = ReturnAction.Settle}
            payment.Controls.Add(New Label With {.Text = "Metode uang", .AutoSize = True})
            methodBox.Items.AddRange({"Tunai", "Transfer"}) : methodBox.SelectedIndex = 0
            payment.Controls.Add(methodBox) : payment.Controls.Add(New Label With {.Text = "Referensi", .AutoSize = True}) : payment.Controls.Add(referenceBox)
            fields.Controls.Add(payment, 0, 2)
            Dim summary As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 8, 18, 0), .ForeColor = Theme.Text, .AccessibleName = "Ringkasan denda dan penyelesaian uang"}
            If action = ReturnAction.Inspect Then
                summary.Text = "Seluruh unit wajib diperiksa sekaligus. Barang yang tidak kembali dicatat Hilang." & Environment.NewLine & "Alat baik kembali siap setelah jeda pemeriksaan; rusak masuk perawatan, hilang keluar dari stok siap." & Environment.NewLine & "Simpan QC belum menyetujui denda atau mengembalikan deposit."
                confirmed.Text = "Seluruh unit sudah diperiksa dan barang yang tidak kembali ditandai Hilang"
                saveButton.Text = "Simpan hasil QC"
            Else
                Dim money = details.Financial
                summary.Text = "Rusak/hilang: " & FormatRupiah(money.ProposedCharge) & " | Terlambat: " & FormatRupiah(money.LateCharge) & Environment.NewLine & "Total denda: " & FormatRupiah(money.TotalCharges) & " | Deposit diterima: " & FormatRupiah(money.Deposit) & Environment.NewLine & "Potong deposit: " & FormatRupiah(money.DepositApplied) & " | Refund: " & FormatRupiah(money.Refund) & Environment.NewLine & "Tambahan dibayar: " & FormatRupiah(money.Additional)
                If action = ReturnAction.Approve Then
                    confirmed.Text = "Saya menyetujui denda rusak/hilang dan keterlambatan sesuai rincian"
                    saveButton.Text = "Setujui tagihan"
                Else
                    confirmed.Text = If(money.Refund > 0, "Refund " & FormatRupiah(money.Refund) & " sudah diserahkan kepada pelanggan", If(money.Additional > 0, "Pembayaran tambahan " & FormatRupiah(money.Additional) & " sudah diterima", "Pemotongan deposit tanpa perpindahan uang tambahan sudah dikonfirmasi"))
                    saveButton.Text = "Selesaikan deposit"
                End If
            End If
            layout.Controls.Add(summary, 0, 1) : layout.Controls.Add(confirmed, 0, 2) : layout.Controls.Add(message, 0, 3)
            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
            buttons.Controls.Add(closeButton) : buttons.Controls.Add(saveButton)
            saveButton.Cursor = Cursors.Hand : closeButton.Cursor = Cursors.Hand
            layout.Controls.Add(buttons, 0, 4) : CancelButton = closeButton
            message.Text = "Konfirmasi hanya setelah tindakan benar-benar dilakukan. Riwayat yang sudah dicatat tetap disimpan."
            AddHandler confirmed.CheckedChanged, Sub() UpdateSave()
            AddHandler grid.CurrentCellDirtyStateChanged, Sub()
                                                             If grid.IsCurrentCellDirty Then grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                         End Sub
            AddHandler grid.DataError, Sub(sender, e) e.ThrowException = False
            AddHandler grid.CellValueChanged, Sub() InvalidateConfirmation()
            AddHandler methodBox.SelectedIndexChanged, Sub() InvalidateConfirmation()
            AddHandler referenceBox.TextChanged, Sub() InvalidateConfirmation()
            AddHandler saveButton.Click, Sub() StartSave()
            AddHandler closeButton.Click, Sub() Close()
        End Sub
        Private Shared Function FormatRupiah(value As Decimal) As String
            Return value.ToString("C0", Culture)
        End Function
        Private Sub InvalidateConfirmation()
            If uncertain Then Return
            confirmed.Checked = False
            message.ForeColor = Theme.Muted : message.Text = "Rincian berubah. Periksa kembali sebelum mengonfirmasi."
        End Sub
        Private Sub UpdateSave()
            saveButton.Enabled = Not busy AndAlso Not stale AndAlso (uncertain OrElse confirmed.Checked)
        End Sub
        Private Function ReadChecks() As List(Of ReturnCheck)
            grid.EndEdit()
            Dim result As New List(Of ReturnCheck)()
            For Each row As DataGridViewRow In grid.Rows
                Dim condition As String
                Select Case Convert.ToString(row.Cells("Kondisi").Value)
                    Case "Baik" : condition = "good"
                    Case "Rusak" : condition = "damaged"
                    Case "Hilang" : condition = "lost"
                    Case Else : Throw New ArgumentException("Pilih kondisi setiap unit. Pengembalian sebagian tidak diizinkan.")
                End Select
                Dim charge As Decimal
                If Not Decimal.TryParse(Convert.ToString(row.Cells("Biaya").Value), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, charge) OrElse charge < 0 OrElse charge > 99999999999999D Then Throw New ArgumentException("Biaya wajib rupiah bulat nol atau lebih, tanpa pemisah ribuan.")
                Dim notes = Convert.ToString(row.Cells("Catatan").Value).Trim()
                If (condition = "good" AndAlso charge <> 0) OrElse (condition <> "good" AndAlso (charge <= 0 OrElse notes.Length = 0)) Then Throw New ArgumentException("Barang baik tidak dikenai biaya kondisi. Rusak/hilang wajib biaya positif dan catatan QC.")
                result.Add(New ReturnCheck With {.UnitId = CLng(row.Cells("UnitId").Value), .Condition = condition, .Charge = charge, .Notes = notes})
            Next
            If result.Count <> details.Units.Count OrElse result.Select(Function(x) x.UnitId).Distinct().Count() <> result.Count Then Throw New ArgumentException("Seluruh unit harus diperiksa sekaligus.")
            Return result
        End Function
        Private Async Sub StartSave()
            If busy OrElse stale OrElse (Not uncertain AndAlso Not confirmed.Checked) Then Return
            _PendingOperation = SaveAsync() : Await PendingOperation
        End Sub
        Private Async Function SaveAsync() As Task
            If Not uncertain Then
                Try
                    checks = If(action = ReturnAction.Inspect, ReadChecks(), New List(Of ReturnCheck)())
                Catch ex As ArgumentException
                    message.ForeColor = Theme.Maintenance : message.Text = ex.Message : Return
                End Try
                requestId = Guid.NewGuid() : selectedMethod = If(methodBox.SelectedIndex = 0, "cash", "transfer") : selectedReference = referenceBox.Text.Trim()
            End If
            SetBusy(True)
            Using request As New CancellationTokenSource()
                activeRequest = request
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Mencatat " & Caption(action).ToLowerInvariant() & "..."
                    _Receipt = Await repository.ProcessReturnAsync(requestId, details.Id, action, checks, selectedMethod, selectedReference, request.Token)
                    busy = False : DialogResult = DialogResult.OK : Close()
                Catch ex As ReturnSaveUncertainException
                    uncertain = True : message.ForeColor = Theme.Maintenance : message.Text = ex.Message
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException
                    If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso DirectCast(ex, SupabaseRequestException).StatusCode = Net.HttpStatusCode.Unauthorized) Then
                        _SessionExpired = True : busy = False : Close()
                    Else
                        uncertain = False : confirmed.Checked = False
                        message.ForeColor = Theme.Maintenance
                        message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Permintaan belum dapat dikirim. Periksa koneksi lalu coba kembali.")
                        stale = TypeOf ex Is SupabaseRequestException
                    End If
                Finally
                    activeRequest = Nothing : SetBusy(False)
                End Try
            End Using
        End Function
        Private Sub SetBusy(value As Boolean)
            busy = value
            If IsDisposed Then Return
            fields.Enabled = Not value AndAlso Not uncertain AndAlso Not stale : confirmed.Enabled = fields.Enabled
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
