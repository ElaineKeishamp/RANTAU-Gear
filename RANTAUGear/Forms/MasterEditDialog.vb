Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Forms
    Public NotInheritable Class MasterEditDialog
        Inherits Form

        Private ReadOnly repository As IRentalRepository
        Private ReadOnly original As MasterRecord
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .Padding = New Padding(20)}
        Private ReadOnly nameBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 150}
        Private ReadOnly codeBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 50}
        Private ReadOnly phoneBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 50}
        Private ReadOnly notesBox As New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .Height = 76, .MaxLength = 2000, .ScrollBars = ScrollBars.Vertical}
        Private ReadOnly parentBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly stateBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly priceBox As New NumericUpDown With {.Dock = DockStyle.Fill, .Maximum = 99999999999999D, .ThousandsSeparator = True}
        Private ReadOnly replacementBox As New NumericUpDown With {.Dock = DockStyle.Fill, .Maximum = 99999999999999D, .ThousandsSeparator = True}
        Private ReadOnly activeBox As New CheckBox With {.Text = "Aktif untuk transaksi baru", .AutoSize = True}
        Private ReadOnly qcBox As New CheckBox With {.Text = "Unit sudah diperiksa dan siap disewakan", .AutoSize = True}
        Private ReadOnly componentsGrid As New DataGridView With {.Dock = DockStyle.Fill, .Height = 190, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False, .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, .BackgroundColor = Color.White}
        Private ReadOnly saveButton As New RoundedButton With {.Text = "Simpan", .Width = 145, .Height = 38, .CornerRadius = 8}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 100, .Height = 38, .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#F3F4F6"), .ForeColor = Theme.Text}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Maintenance, .Padding = New Padding(20, 0, 20, 0)}
        Private ReadOnly photoBox As New PictureBox With {.SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.FromArgb(245, 247, 246), .BorderStyle = BorderStyle.FixedSingle, .Width = 140, .Height = 140}
        Private ReadOnly choosePhotoButton As New Button With {.Text = "Pilih Foto", .Width = 100, .Height = 34, .Cursor = Cursors.Hand, .FlatStyle = FlatStyle.Flat, .BackColor = Theme.Primary, .ForeColor = Color.White}
        Private ReadOnly photoOpenDialog As New OpenFileDialog With {.Filter = "Gambar (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", .Title = "Pilih foto alat"}
        Private request As CancellationTokenSource
        Private busy As Boolean
        Private uncertain As Boolean
        Private stale As Boolean
        Private pendingRecord As MasterRecord
        Private requestId As Guid
        Private selectedPhotoPath As String = String.Empty
        Public ReadOnly Property SavedId As Long?
        Public ReadOnly Property SessionExpired As Boolean
        Public ReadOnly Property PendingSave As Task = Task.CompletedTask

        Private NotInheritable Class StateChoice
            Public Property Key As String
            Public Property Caption As String
            Public Overrides Function ToString() As String
                Return Caption
            End Function
        End Class

        Public Sub New(repository As IRentalRepository, record As MasterRecord, choices As List(Of MasterRecord))
            Me.repository = repository
            original = record.Copy()
            Text = If(record.Id.HasValue, "Edit ", "Tambah ") & KindName(record.Kind) & " | RANTAU Gear"
            Size = New Size(720, 740)
            MinimumSize = New Size(650, 600)
            StartPosition = FormStartPosition.CenterParent
            BackColor = Theme.Background
            Font = Theme.UiFont
            AutoScaleMode = AutoScaleMode.Dpi
            MinimizeBox = False
            MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 86))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout)
            Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
            scroll.Controls.Add(fields)
            layout.Controls.Add(scroll, 0, 0)
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160))
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            If record.Kind <> MasterDataKind.EquipmentUnit Then AddField("Nama", nameBox)
            Select Case record.Kind
                Case MasterDataKind.EquipmentType, MasterDataKind.EquipmentUnit
                    For Each choice In choices
                        parentBox.Items.Add(choice)
                    Next
                    If record.ParentId.HasValue Then
                        For i = 0 To parentBox.Items.Count - 1
                            If DirectCast(parentBox.Items(i), MasterRecord).Id = record.ParentId Then parentBox.SelectedIndex = i
                        Next
                    End If
                    AddField(If(record.Kind = MasterDataKind.EquipmentType, "Kategori", "Jenis alat"), parentBox)
                    If choices.Count = 0 Then message.Text = "Tambahkan " & If(record.Kind = MasterDataKind.EquipmentType, "kategori", "jenis alat") & " terlebih dahulu di Katalog."
            End Select
            Select Case record.Kind
                Case MasterDataKind.EquipmentType
                    AddField("Tarif per hari (Rp)", priceBox)
                    AddField("Nilai penggantian (Rp)", replacementBox)
                    AddField("Deskripsi", notesBox, 90)
                Case MasterDataKind.EquipmentUnit
                    AddField("Kode unit", codeBox)
                    Dim permitted = If(record.State = "rented", New String() {"rented"}, If(record.State = "lost", New String() {"lost", "retired"}, New String() {"ready", "maintenance", "retired"}))
                    For Each state In permitted
                        stateBox.Items.Add(New StateChoice With {.Key = state, .Caption = StateName(state)})
                        If state = record.State Then stateBox.SelectedIndex = stateBox.Items.Count - 1
                    Next
                    AddField("Kondisi", stateBox)
                    AddField("Pemeriksaan", qcBox, 55)
                    AddField("Catatan", notesBox, 90)
                    AddField("Riwayat", New Label With {.Text = "Kode dan jenis unit yang pernah dipakai transaksi tidak dapat diganti. Status disewa/hilang mengikuti proses rental.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 70)
                    qcBox.Checked = record.State = "ready" AndAlso record.Id.HasValue
                    AddHandler stateBox.SelectedIndexChanged, Sub() UpdateQc()
                    UpdateQc()
                Case MasterDataKind.Package
                    AddField("Tarif paket per hari (Rp)", priceBox)
                    componentsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "TypeId", .Visible = False})
                    componentsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "TypeName", .HeaderText = "Jenis alat", .ReadOnly = True, .FillWeight = 75})
                    componentsGrid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Quantity", .HeaderText = "Jumlah", .ValueType = GetType(String), .FillWeight = 25})
                    For Each choice In choices
                        Dim item = record.Components.FirstOrDefault(Function(component) component.TypeId = choice.Id.Value)
                        componentsGrid.Rows.Add(choice.Id.Value, choice.ToString(), If(item Is Nothing, 0, item.Quantity))
                    Next
                    AddField("Isi paket", componentsGrid, 220)
                    AddField("Jumlah", New Label With {.Text = "Isi 0 untuk alat yang tidak termasuk paket. Paket aktif harus memiliki minimal satu jenis alat.", .ForeColor = Theme.Muted, .Dock = DockStyle.Fill}, 60)
                Case MasterDataKind.Customer
                    AddField("Nomor telepon", phoneBox)
                    AddField("Alamat", notesBox, 90)
            End Select
            ' Foto preview (hanya untuk jenis alat dan unit)
            If record.Kind = MasterDataKind.EquipmentType OrElse record.Kind = MasterDataKind.EquipmentUnit Then
                Dim photoLabel As New Label With {.Text = "Foto", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Theme.Text}
                Dim photoRow As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(4, 8, 4, 8)}
                photoRow.Controls.Add(photoBox)
                Dim photoBtnRow As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 34, .FlowDirection = FlowDirection.LeftToRight}
                photoBtnRow.Controls.Add(choosePhotoButton)
                photoRow.Controls.Add(photoBtnRow)
                AddHandler choosePhotoButton.Click, Sub()
                                                        If photoOpenDialog.ShowDialog(FindForm()) = DialogResult.OK Then
                                                            selectedPhotoPath = photoOpenDialog.FileName
                                                            Try
                                                                Using img As Image = ImageHelper.LoadImageFromFile(selectedPhotoPath)
                                                                    photoBox.Image = New Bitmap(img)
                                                                End Using
                                                            Catch
                                                                photoBox.Image = Nothing
                                                            End Try
                                                        End If
                                                    End Sub
                ' Tampilkan foto yang sudah ada jika ada di Assets
                Dim existingRel As String = ImageHelper.FindImageForName(record.Name)
                If existingRel IsNot Nothing Then
                    Dim existingImg As Image = ImageHelper.LoadFromAssets(existingRel)
                    If existingImg IsNot Nothing Then
                        photoBox.Image = New Bitmap(existingImg)
                    End If
                End If
                fields.Controls.Add(photoLabel, 0, fields.RowCount)
                fields.Controls.Add(photoRow, 1, fields.RowCount)
                fields.RowCount += 1
                fields.RowStyles.Add(New RowStyle(SizeType.Absolute, 170))
            End If
            If record.Kind <> MasterDataKind.EquipmentUnit Then AddField("Status", activeBox, 52)
            nameBox.Text = record.Name
            codeBox.Text = record.Code
            phoneBox.Text = record.Phone
            activeBox.Checked = record.IsActive
            priceBox.Value = Math.Clamp(record.DailyRate, priceBox.Minimum, priceBox.Maximum)
            replacementBox.Value = Math.Clamp(record.ReplacementValue, replacementBox.Minimum, replacementBox.Maximum)
            notesBox.Text = If(record.Kind = MasterDataKind.Customer, record.Address, If(record.Kind = MasterDataKind.EquipmentType, record.Description, record.Notes))
            layout.Controls.Add(message, 0, 1)
            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(12, 0, 12, 0)}
            buttons.Controls.Add(closeButton)
            buttons.Controls.Add(saveButton)
            layout.Controls.Add(buttons, 0, 2)
            AcceptButton = saveButton
            CancelButton = closeButton
            closeButton.DialogResult = DialogResult.None
            AddHandler closeButton.Click, Sub()
                                               If busy Then
                                                   request?.Cancel()
                                               Else
                                                   DialogResult = DialogResult.Cancel
                                                   Close()
                                               End If
                                           End Sub
            AddHandler saveButton.Click, Async Sub()
                                             If busy Then Return
                                             _PendingSave = SaveAsync()
                                             Await PendingSave
                                         End Sub
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub

        Public Shared Function KindName(kind As MasterDataKind) As String
            Select Case kind
                Case MasterDataKind.Category : Return "kategori"
                Case MasterDataKind.EquipmentType : Return "jenis alat"
                Case MasterDataKind.EquipmentUnit : Return "unit fisik"
                Case MasterDataKind.Package : Return "paket"
                Case MasterDataKind.Customer : Return "pelanggan"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(kind))
            End Select
        End Function

        Public Shared Function StateName(state As String) As String
            Select Case state
                Case "ready" : Return "Siap"
                Case "maintenance" : Return "Perawatan"
                Case "retired" : Return "Dinonaktifkan"
                Case "rented" : Return "Disewa"
                Case "lost" : Return "Hilang"
                Case Else : Return state
            End Select
        End Function

        Private Sub AddField(caption As String, control As Control, Optional height As Integer = 52)
            Dim row = fields.RowCount
            fields.RowCount += 1
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
            fields.Controls.Add(New Label With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .ForeColor = Theme.Text}, 0, row)
            control.AccessibleName = caption
            control.Margin = New Padding(4, 8, 4, 8)
            fields.Controls.Add(control, 1, row)
        End Sub

        Private Sub UpdateQc()
            If stateBox.SelectedItem Is Nothing Then Return
            Dim needed = DirectCast(stateBox.SelectedItem, StateChoice).Key = "ready" AndAlso (Not original.Id.HasValue OrElse original.State <> "ready")
            qcBox.Enabled = needed
            If needed Then qcBox.Checked = False
        End Sub

        Private Function ReadRecord() As MasterRecord
            Dim record = original.Copy()
            record.Name = nameBox.Text.Trim()
            record.Code = codeBox.Text.Trim()
            record.Phone = phoneBox.Text.Trim()
            record.DailyRate = priceBox.Value
            record.ReplacementValue = replacementBox.Value
            record.IsActive = activeBox.Checked
            record.Notes = notesBox.Text.Trim()
            record.Address = notesBox.Text.Trim()
            record.Description = notesBox.Text.Trim()
            If record.Kind = MasterDataKind.EquipmentType OrElse record.Kind = MasterDataKind.EquipmentUnit Then
                Dim parent = TryCast(parentBox.SelectedItem, MasterRecord)
                record.ParentId = If(parent Is Nothing, CType(Nothing, Long?), parent.Id)
                If parent IsNot Nothing AndAlso Not parent.IsActive AndAlso (record.IsActive OrElse record.Kind = MasterDataKind.EquipmentUnit) Then Throw New ArgumentException("Pilih kategori atau jenis alat yang aktif.")
            End If
            If record.Kind = MasterDataKind.EquipmentUnit Then
                If stateBox.SelectedItem Is Nothing Then Throw New ArgumentException("Pilih kondisi unit.")
                record.State = DirectCast(stateBox.SelectedItem, StateChoice).Key
                record.QcConfirmed = qcBox.Checked
                If record.State = "ready" AndAlso (Not original.Id.HasValue OrElse original.State <> "ready") AndAlso Not record.QcConfirmed Then Throw New ArgumentException("Konfirmasi pemeriksaan sebelum menyatakan unit siap.")
            ElseIf record.Kind = MasterDataKind.Package Then
                componentsGrid.EndEdit()
                record.Components.Clear()
                For Each row As DataGridViewRow In componentsGrid.Rows
                    Dim quantity As Integer
                    If Not Integer.TryParse(Convert.ToString(row.Cells("Quantity").Value), quantity) OrElse quantity < 0 Then Throw New ArgumentException("Jumlah isi paket harus berupa bilangan bulat nol atau lebih.")
                    If quantity > 0 Then record.Components.Add(New MasterPackageItem With {.TypeId = CLng(row.Cells("TypeId").Value), .Quantity = quantity})
                Next
            End If
            record.Validate()
            Return record
        End Function

        Private Async Function SaveAsync() As Task
            message.ForeColor = Theme.Maintenance
            If Not uncertain Then
                Try
                    pendingRecord = ReadRecord()
                Catch ex As ArgumentException
                    message.Text = ex.Message
                    Return
                End Try
                requestId = Guid.NewGuid()
            End If
            busy = True
            fields.Enabled = False
            saveButton.Enabled = False
            closeButton.Text = "Batalkan"
            message.ForeColor = Theme.Muted
            message.Text = "Menyimpan data ke Supabase..."
            Using attempt As New CancellationTokenSource()
                request = attempt
                Try
                    Dim id = Await repository.SaveMasterAsync(requestId, pendingRecord, attempt.Token)
                    _SavedId = id
                    ' Simpan foto yang dipilih ke folder Assets dengan nama berdasarkan alat
                    If Not String.IsNullOrWhiteSpace(selectedPhotoPath) AndAlso File.Exists(selectedPhotoPath) Then
                        Try
                            Dim safeName As String = String.Concat(pendingRecord.Name.Split(Path.GetInvalidFileNameChars())).Trim()
                            If safeName.Length = 0 Then safeName = "alat"
                            Dim ext As String = Path.GetExtension(selectedPhotoPath)
                            If String.IsNullOrEmpty(ext) Then ext = ".png"
                            Dim destPath As String = Path.Combine(Path.Combine(AppContext.BaseDirectory, "Assets"), safeName & ext)
                            File.Copy(selectedPhotoPath, destPath, overwrite:=True)
                        Catch
                            ' Gagal menyalin foto tidak mencegah penyimpanan data
                        End Try
                    End If
                    busy = False
                    DialogResult = DialogResult.OK
                    Close()
                Catch ex As MasterSaveUncertainException
                    uncertain = True
                    message.Text = ex.Message
                Catch ex As UnauthorizedAccessException
                    _SessionExpired = True
                    busy = False
                    DialogResult = DialogResult.Abort
                    Close()
                Catch ex As SupabaseRequestException When ex.StatusCode = Net.HttpStatusCode.Unauthorized
                    _SessionExpired = True
                    busy = False
                    DialogResult = DialogResult.Abort
                    Close()
                Catch ex As SupabaseRequestException
                    message.Text = ex.Message
                    stale = ex.ErrorCode = "RG_MASTER_VERSION" OrElse ex.ErrorCode = "RG_MASTER_MISSING"
                Catch ex As OperationCanceledException
                    message.Text = "Penyimpanan dibatalkan sebelum dikirim."
                Catch ex As System.Net.Http.HttpRequestException
                    message.Text = "Tidak dapat menghubungi Supabase. Periksa internet lalu coba lagi."
                Catch ex As Exception When TypeOf ex Is System.Text.Json.JsonException OrElse TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is FormatException OrElse TypeOf ex Is KeyNotFoundException
                    message.Text = "Respons layanan belum dapat dibaca. Tutup form lalu muat ulang daftar."
                Finally
                    request = Nothing
                    busy = False
                    If Not IsDisposed Then
                        message.ForeColor = Theme.Maintenance
                        fields.Enabled = Not uncertain AndAlso Not stale
                        saveButton.Enabled = Not stale
                        saveButton.Text = If(uncertain, "Simpan ulang", "Simpan")
                        closeButton.Text = If(uncertain, "Tutup", "Batal")
                    End If
                End Try
            End Using
        End Function

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If busy Then
                e.Cancel = True
                request?.Cancel()
            End If
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
