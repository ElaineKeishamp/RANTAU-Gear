Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Models
Imports RantauGear.UI
Namespace Forms
    Public NotInheritable Class StaffEditorDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly original As StaffRecord
        Private ReadOnly operation As String
        Private ReadOnly selfId As Guid
        Private ReadOnly fields As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .Padding = New Padding(18)}
        Private ReadOnly nameBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 150}
        Private ReadOnly emailBox As New TextBox With {.Dock = DockStyle.Fill, .MaxLength = 254}
        Private ReadOnly roleBox As New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList}
        Private ReadOnly activeBox As New CheckBox With {.Text = "Akun staf aktif", .AutoSize = True}
        Private ReadOnly passwordBox As New TextBox With {.Dock = DockStyle.Fill, .UseSystemPasswordChar = True, .MaxLength = 128}
        Private ReadOnly confirmBox As New TextBox With {.Dock = DockStyle.Fill, .UseSystemPasswordChar = True, .MaxLength = 128}
        Private ReadOnly showButton As New Button With {.Text = "Tampilkan password", .Dock = DockStyle.Fill}
        Private ReadOnly saveButton As New RoundedButton With {.Text = "Simpan akun", .Width = 165, .Height = 38}
        Private ReadOnly closeButton As New Button With {.Text = "Batal", .Width = 100, .Height = 38}
        Private ReadOnly message As New Label With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 0, 18, 0), .ForeColor = Theme.Muted}
        Private busy As Boolean
        Private uncertain As Boolean
        Private stale As Boolean
        Private requestId As Guid
        Private frozen As StaffRecord
        Private frozenPassword As String
        Private request As CancellationTokenSource
        Public ReadOnly Property PendingSave As Task = Task.CompletedTask
        Public ReadOnly Property SavedId As Guid?
        Public ReadOnly Property SessionExpired As Boolean
        Public Sub New(repository As IRentalRepository, record As StaffRecord, operation As String, selfId As Guid)
            Me.repository = repository : original = record.Copy() : Me.operation = operation : Me.selfId = selfId
            Text = If(operation = "create", "Tambah akun staf", If(operation = "password", "Ubah password staf", "Edit akun staf")) & " | RANTAU Gear"
            Size = New Size(780, 720) : MinimumSize = New Size(720, 650)
            Font = Theme.UiFont : BackColor = Theme.Background : AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent : MinimizeBox = False : MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100)) : layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 110)) : layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            Controls.Add(layout)
            Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
            scroll.Controls.Add(fields) : layout.Controls.Add(scroll, 0, 0)
            fields.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170)) : fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            nameBox.Text = record.DisplayName : emailBox.Text = record.Email : activeBox.Checked = record.IsActive
            roleBox.Items.AddRange({"Super Admin", "Front office", "Gudang / QC"})
            Dim keys = {"super_admin", "front_office", "warehouse_qc"} : roleBox.SelectedIndex = Array.IndexOf(keys, record.Role)
            AddField("Nama tampilan", nameBox, 45) : AddField("Email login", emailBox, 45)
            emailBox.ReadOnly = operation <> "create" : nameBox.ReadOnly = operation = "password"
            If operation <> "password" Then
                AddField("Peran", roleBox, 45)
                If operation = "update" Then AddField("Akses akun", activeBox, 45)
                If operation = "update" AndAlso record.Id = selfId Then
                    roleBox.Enabled = False : activeBox.Enabled = False
                End If
            End If
            If operation <> "update" Then
                AddField("Password baru", passwordBox, 45) : AddField("Ulangi password", confirmBox, 45)
                AddField("Tampilan", showButton, 45)
            End If
            AddField("Petunjuk", New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted, .Text = If(operation = "create", "Password 8 sampai 128 karakter. Akun dibuat aktif tanpa email undangan; sampaikan akses kepada petugas secara pribadi.", If(operation = "password", "Password tidak disimpan pada file aplikasi. Bila mengubah password sendiri, aplikasi meminta masuk kembali setelah berhasil.", "Email login tidak diubah di sini. Akun sendiri tidak dapat dinonaktifkan atau diturunkan perannya. Nonaktifkan akun untuk menutup akses tanpa menghapus riwayat."))}, 115)
            layout.Controls.Add(message, 0, 1)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
            actions.Controls.Add(closeButton) : actions.Controls.Add(saveButton) : layout.Controls.Add(actions, 0, 2)
            saveButton.Cursor = Cursors.Hand : closeButton.Cursor = Cursors.Hand : showButton.Cursor = Cursors.Hand
            AcceptButton = saveButton : CancelButton = closeButton
            AddHandler showButton.Click, Sub()
                                             passwordBox.UseSystemPasswordChar = Not passwordBox.UseSystemPasswordChar
                                             confirmBox.UseSystemPasswordChar = passwordBox.UseSystemPasswordChar
                                             showButton.Text = If(passwordBox.UseSystemPasswordChar, "Tampilkan password", "Sembunyikan password")
                                         End Sub
            AddHandler saveButton.Click, Sub() StartSave()
            AddHandler nameBox.TextChanged, Sub() InputChanged()
            AddHandler emailBox.TextChanged, Sub() InputChanged()
            AddHandler passwordBox.TextChanged, Sub() InputChanged()
            AddHandler confirmBox.TextChanged, Sub() InputChanged()
            AddHandler roleBox.SelectedIndexChanged, Sub() InputChanged()
            AddHandler activeBox.CheckedChanged, Sub() InputChanged()
            AddHandler closeButton.Click, Sub() Close()
        End Sub
        Private Sub InputChanged()
            If busy OrElse uncertain OrElse stale Then Return
            message.ForeColor = Theme.Muted : message.Text = "Periksa rincian lalu simpan perubahan akun."
        End Sub
        Private Sub AddField(caption As String, control As Control, height As Integer)
            Dim row = fields.RowCount : fields.RowCount += 1
            fields.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
            fields.Controls.Add(New Label With {.Dock = DockStyle.Fill, .Text = caption, .ForeColor = Theme.Text, .TextAlign = ContentAlignment.MiddleLeft}, 0, row)
            control.Margin = New Padding(4, 6, 4, 6) : control.AccessibleName = caption
            fields.Controls.Add(control, 1, row)
        End Sub
        Private Async Sub StartSave()
            If busy OrElse stale Then Return
            _PendingSave = SaveAsync() : Await PendingSave
        End Sub
        Private Async Function SaveAsync() As Task
            If Not uncertain Then
                Try
                    frozen = original.Copy() : frozen.DisplayName = nameBox.Text.Trim() : frozen.Email = emailBox.Text.Trim()
                    If operation <> "password" Then
                        If roleBox.SelectedIndex < 0 Then Throw New ArgumentException("Pilih peran staf.")
                        frozen.Role = {"super_admin", "front_office", "warehouse_qc"}(roleBox.SelectedIndex)
                        frozen.IsActive = If(operation = "create", True, activeBox.Checked)
                    End If
                    frozen.Validate(operation)
                    frozenPassword = passwordBox.Text
                    If operation <> "update" AndAlso (frozenPassword.Length < 8 OrElse frozenPassword <> confirmBox.Text) Then Throw New ArgumentException("Password minimal 8 karakter dan kedua isian harus sama.")
                Catch ex As ArgumentException
                    message.ForeColor = Theme.Maintenance : message.Text = ex.Message : Return
                End Try
                requestId = Guid.NewGuid()
            End If
            busy = True : fields.Enabled = False : saveButton.Enabled = False
            Using attempt As New CancellationTokenSource()
                request = attempt
                Try
                    message.ForeColor = Theme.Muted : message.Text = "Menyimpan perubahan akun..."
                    _SavedId = Await repository.SaveStaffAsync(requestId, operation, frozen, frozenPassword, attempt.Token)
                    If operation = "password" AndAlso SavedId.GetValueOrDefault() = selfId Then _SessionExpired = True
                    frozenPassword = String.Empty : passwordBox.Clear() : confirmBox.Clear()
                    busy = False : DialogResult = DialogResult.OK : Close()
                Catch ex As StaffWriteUncertainException
                    uncertain = True : message.ForeColor = Theme.Maintenance : message.Text = ex.Message
                Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException
                    message.ForeColor = Theme.Maintenance : message.Text = If(TypeOf ex Is SupabaseRequestException, ex.Message, "Permintaan belum dapat dikirim. Periksa koneksi lalu coba kembali.")
                    If TypeOf ex Is UnauthorizedAccessException OrElse (TypeOf ex Is SupabaseRequestException AndAlso {Net.HttpStatusCode.Unauthorized, Net.HttpStatusCode.Forbidden}.Contains(DirectCast(ex, SupabaseRequestException).StatusCode)) Then
                        _SessionExpired = True : busy = False : Close()
                    ElseIf TypeOf ex Is SupabaseRequestException Then
                        stale = {"RG_STAFF_VERSION", "RG_STAFF_REQUEST"}.Contains(DirectCast(ex, SupabaseRequestException).ErrorCode)
                    End If
                Finally
                    request = Nothing : busy = False
                    If Not IsDisposed Then
                        fields.Enabled = Not uncertain AndAlso Not stale : saveButton.Enabled = Not stale
                        saveButton.Text = If(uncertain, "Periksa simpan", "Simpan akun") : closeButton.Text = "Tutup"
                    End If
                End Try
            End Using
        End Function
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If busy Then
                e.Cancel = True : request?.Cancel()
            Else
                passwordBox.Clear() : confirmBox.Clear() : frozenPassword = String.Empty
            End If
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
