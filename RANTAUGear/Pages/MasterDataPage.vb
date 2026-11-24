Option Strict On
Option Explicit On
Option Infer On
Imports System.Data
Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Forms
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Pages
    Public NotInheritable Class MasterDataPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly lists As New List(Of MasterListPage)()
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task Implements IWorkspacePage.PendingLoad
            Get
                Return Task.WhenAll(lists.Select(Function(list) list.PendingLoad))
            End Get
        End Property
        Public Sub New(repository As IRentalRepository, Optional kind As MasterDataKind? = Nothing)
            Dock = DockStyle.Fill
            BackColor = Theme.Background
            If kind.HasValue Then
                Dim list = CreateList(repository, kind.Value)
                Controls.Add(list)
            Else
                Dim tabs As New TabControl With {.Dock = DockStyle.Fill}
                For Each item In New MasterDataKind() {MasterDataKind.Category, MasterDataKind.EquipmentType, MasterDataKind.EquipmentUnit}
                    Dim tab As New TabPage(Globalization.CultureInfo.GetCultureInfo("id-ID").TextInfo.ToTitleCase(MasterEditDialog.KindName(item)))
                    tab.Controls.Add(CreateList(repository, item))
                    tabs.TabPages.Add(tab)
                Next
                Controls.Add(tabs)
            End If
            UiHelpers.FinishPage(Me)
        End Sub
        Private Function CreateList(repository As IRentalRepository, kind As MasterDataKind) As MasterListPage
            Dim list As New MasterListPage(repository, kind)
            lists.Add(list)
            AddHandler list.SessionExpired, Sub() RaiseEvent SessionExpired(Me, EventArgs.Empty)
            Return list
        End Function
    End Class

    Public NotInheritable Class MasterListPage
        Inherits UserControl
        Implements IWorkspacePage
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly kind As MasterDataKind
        Private ReadOnly flow As New FlowLayoutPanel With {.AutoScroll = True, .Dock = DockStyle.Fill, .BackColor = Theme.Background, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Padding = New Padding(10)}
        Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Muted}
        Private ReadOnly empty As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Theme.Muted}
        Private ReadOnly addButton As New RoundedButton With {.Text = "Tambah", .Width = 110, .Height = 36, .CornerRadius = 6}
        Private ReadOnly editButton As New Button With {.Text = "Edit pilihan", .Width = 110, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand, .Enabled = False}
        Private ReadOnly reload As New Button With {.Text = "Muat ulang", .Width = 110, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly previous As New Button With {.Text = "Sebelumnya", .Width = 100, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly following As New Button With {.Text = "Berikutnya", .Width = 100, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand}
        Private ReadOnly cancel As New Button With {.Text = "Batalkan", .Width = 95, .Height = 36, .Visible = False, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#FEE2E4"), .ForeColor = ColorTranslator.FromHtml("#DC2626"), .Cursor = Cursors.Hand}
        Private records As New List(Of MasterRecord)()
        Private selectedRecord As MasterRecord = Nothing
        Private ReadOnly photoDialog As New OpenFileDialog With {.Filter = "Gambar (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", .Title = "Pilih foto alat"}
        Private ReadOnly addPhotoButton As New Button With {.Text = "Tambah Gambar", .Width = 120, .Height = 36, .FlatStyle = FlatStyle.Flat, .BackColor = ColorTranslator.FromHtml("#E5E7EB"), .ForeColor = Theme.Text, .Cursor = Cursors.Hand, .Enabled = False}
        Private offset As Integer
        Private total As Long
        Private busy As Boolean
        Private activeRequest As CancellationTokenSource
        Public Event SessionExpired As EventHandler Implements IWorkspacePage.SessionExpired
        Public ReadOnly Property PendingLoad As Task = Task.CompletedTask Implements IWorkspacePage.PendingLoad

        Public Sub New(repository As IRentalRepository, kind As MasterDataKind)
            Me.repository = repository
            Me.kind = kind
            addPhotoButton.Enabled = (kind = MasterDataKind.Category OrElse kind = MasterDataKind.EquipmentType)
            Dock = DockStyle.Fill
            Padding = New Padding(20, 16, 20, 20)
            BackColor = Theme.Background
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 90))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(layout)
            layout.Controls.Add(New Label With {.Text = "Kelola " & MasterEditDialog.KindName(kind) & " di Supabase. Data yang sudah dipakai tetap disimpan; gunakan status nonaktif jika tidak digunakan lagi.", .Dock = DockStyle.Fill, .ForeColor = Theme.Muted}, 0, 0)
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}
            For Each button As Button In New Button() {addButton, editButton, addPhotoButton, reload, previous, following, cancel}
                button.Cursor = Cursors.Hand
                toolbar.Controls.Add(button)
            Next
            layout.Controls.Add(toolbar, 0, 1)
            layout.Controls.Add(status, 0, 2)
            layout.Controls.Add(flow, 0, 3)
            layout.Controls.Add(empty, 0, 3)
            flow.Visible = False
            empty.Text = "Memuat data..."
            AddHandler reload.Click, Sub() StartLoad(offset)
            AddHandler previous.Click, Sub() StartLoad(Math.Max(0, offset - 25))
            AddHandler following.Click, Sub() StartLoad(offset + 25)
            AddHandler addButton.Click, Sub() StartEdit(New MasterRecord With {.Kind = kind})
            AddHandler editButton.Click, Sub()
                                              If selectedRecord Is Nothing Then Return
                                              StartEdit(selectedRecord.Copy())
                                          End Sub
            AddHandler addPhotoButton.Click, Sub()
                                                  If selectedRecord Is Nothing Then Return
                                                  If photoDialog.ShowDialog(FindForm()) = DialogResult.OK Then
                                                      Try
                                                          Dim safeName As String = String.Concat(selectedRecord.Name.Split(Path.GetInvalidFileNameChars())).Trim()
                                                          If safeName.Length = 0 Then safeName = "alat"
                                                          Dim ext As String = Path.GetExtension(photoDialog.FileName)
                                                          If String.IsNullOrEmpty(ext) Then ext = ".png"
                                                          Dim destPath As String = Path.Combine(Path.Combine(AppContext.BaseDirectory, "Assets"), safeName & ext)
                                                          File.Copy(photoDialog.FileName, destPath, overwrite:=True)
                                                          BindRows()
                                                      Catch ex As Exception
                                                          MessageBox.Show("Gagal menyimpan foto: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                      End Try
                                                  End If
                                              End Sub
            AddHandler cancel.Click, Sub() activeRequest?.Cancel()
            SetBusy(False)
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            StartLoad(0)
        End Sub

        Private Async Sub StartLoad(newOffset As Integer)
            If busy OrElse IsDisposed Then Return
            _PendingLoad = LoadAsync(newOffset)
            Await PendingLoad
        End Sub

        Public Async Function LoadAsync(newOffset As Integer) As Task
            If busy OrElse IsDisposed Then Return
            Using request As New CancellationTokenSource()
                activeRequest = request
                SetBusy(True)
                status.ForeColor = Theme.Muted
                status.Text = "Mengambil data dari Supabase..."
                Try
                    Dim result = Await repository.ReadMasterAsync(kind, newOffset, 25, request.Token)
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    records = result.Records
                    offset = result.Offset
                    total = result.Total
                    BindRows()
                    status.Text = If(records.Count = 0, "Belum ada data.", (offset + 1).ToString() & " sampai " & (offset + records.Count).ToString() & " dari " & total.ToString() & " data")
                Catch ex As Exception When Expected(ex)
                    HandleError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
        End Function

        Private Sub BindRows()
            flow.Controls.Clear()
            selectedRecord = Nothing
            editButton.Enabled = False
            Dim needsFoto As Boolean = kind = MasterDataKind.Category OrElse kind = MasterDataKind.EquipmentType
            For Each item In records
                flow.Controls.Add(CreateRecordCard(item, needsFoto))
            Next
            flow.Visible = records.Count > 0
            empty.Visible = Not flow.Visible
            empty.Text = "Belum ada " & MasterEditDialog.KindName(kind) & ". Klik Tambah untuk mencatat data pertama."
        End Sub

        Private Function CreateRecordCard(item As MasterRecord, needsFoto As Boolean) As Panel
            Dim card As New Panel With {
                .Size = New Size(260, 300),
                .BackColor = Theme.Card,
                .Margin = New Padding(10),
                .Cursor = Cursors.Hand,
                .Tag = item
            }

            Dim photoBox As PictureBox = Nothing
            If needsFoto Then
                Dim foto As Image = ImageHelper.LoadImageForName(item.Name, New Size(140, 140))
                photoBox = New PictureBox With {
                    .SizeMode = PictureBoxSizeMode.Zoom,
                    .BackColor = Color.FromArgb(245, 247, 246),
                    .BorderStyle = BorderStyle.FixedSingle,
                    .Size = New Size(140, 140),
                    .Location = New Point(60, 16),
                    .Image = If(foto, ImageHelper.CreatePlaceholder(New Size(140, 140))),
                    .Cursor = Cursors.Hand
                }
                card.Controls.Add(photoBox)
            End If
            Dim nameLabel As New Label With {
                .Text = item.Name,
                .Font = New Font("Segoe UI", 12, FontStyle.Bold),
                .ForeColor = Theme.Text,
                .AutoSize = False,
                .Size = New Size(200, 26),
                .Location = New Point(30, 168),
                .TextAlign = ContentAlignment.MiddleCenter,
                .Cursor = Cursors.Hand
            }
            Dim infoLabel As New Label With {
                .Text = GetCardInfo(item),
                .Font = New Font("Segoe UI", 9),
                .ForeColor = Theme.Muted,
                .AutoSize = False,
                .Size = New Size(200, 44),
                .Location = New Point(30, 196),
                .TextAlign = ContentAlignment.MiddleCenter,
                .Cursor = Cursors.Hand
            }
            Dim statusLabel As New Label With {
                .Text = GetStatusText(item),
                .Font = New Font("Segoe UI", 9, FontStyle.Bold),
                .ForeColor = If(item.IsActive, Theme.ReadyText, Theme.Maintenance),
                .AutoSize = False,
                .Size = New Size(200, 20),
                .Location = New Point(30, 244),
                .TextAlign = ContentAlignment.MiddleCenter,
                .Cursor = Cursors.Hand
            }
            card.Controls.Add(nameLabel)
            card.Controls.Add(infoLabel)
            card.Controls.Add(statusLabel)

            ' Selection handler yang dipanggil dari card maupun child control
            Dim doSelect As New Action(Sub()
                                           selectedRecord = item
                                           editButton.Enabled = True
                                           For Each ctrl As Control In flow.Controls
                                               If TypeOf ctrl Is Panel Then
                                                   ctrl.BackColor = If(ctrl Is card, ColorTranslator.FromHtml("#E8F5EE"), Theme.Card)
                                               End If
                                           Next
                                       End Sub)

            AddHandler card.Click, Sub() doSelect()
            If photoBox IsNot Nothing Then AddHandler photoBox.Click, Sub() doSelect()
            AddHandler nameLabel.Click, Sub() doSelect()
            AddHandler infoLabel.Click, Sub() doSelect()
            AddHandler statusLabel.Click, Sub() doSelect()

            ' Double-klik foto -> buka dialog ganti gambar
            If photoBox IsNot Nothing Then
            AddHandler photoBox.DoubleClick, Sub()
                                                 If photoDialog.ShowDialog(FindForm()) = DialogResult.OK Then
                                                     Try
                                                         Dim safeName As String = String.Concat(item.Name.Split(Path.GetInvalidFileNameChars())).Trim()
                                                         If safeName.Length = 0 Then safeName = "alat"
                                                         Dim ext As String = Path.GetExtension(photoDialog.FileName)
                                                         If String.IsNullOrEmpty(ext) Then ext = ".png"
                                                         Dim destPath As String = Path.Combine(Path.Combine(AppContext.BaseDirectory, "Assets"), safeName & ext)
                                                         File.Copy(photoDialog.FileName, destPath, overwrite:=True)
                                                         Using img As Image = ImageHelper.LoadImageFromFile(destPath)
                                                             photoBox.Image = New Bitmap(img)
                                                         End Using
                                                     Catch ex As Exception
                                                         MessageBox.Show("Gagal menyimpan foto: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                     End Try
                                                 End If
                                             End Sub
            End If

            AddHandler card.Paint, Sub(s As Object, e As PaintEventArgs)
                                       Using pen As New Pen(Theme.BorderLight, 1)
                                           e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
                                       End Using
                                   End Sub
            Return card
        End Function

        Private Function GetCardInfo(item As MasterRecord) As String
            Select Case kind
                Case MasterDataKind.Category
                    Return If(item.IsActive, "Aktif", "Nonaktif")
                Case MasterDataKind.EquipmentType
                    Return String.Format("Kategori: {0} | Tarif: {1}", item.ParentName, UiHelpers.Currency(item.DailyRate))
                Case MasterDataKind.EquipmentUnit
                    Return String.Format("Kode: {0} | {1}", item.Code, MasterEditDialog.StateName(item.State))
                Case MasterDataKind.Package
                    Return String.Format("Tarif: {0} | {1} jenis alat", UiHelpers.Currency(item.DailyRate), item.Components.Count)
                Case MasterDataKind.Customer
                    Return String.Format("Telp: {0}", item.Phone)
                Case Else
                    Return String.Empty
            End Select
        End Function

        Private Function GetStatusText(item As MasterRecord) As String
            Select Case kind
                Case MasterDataKind.Category, MasterDataKind.EquipmentType, MasterDataKind.Package, MasterDataKind.Customer
                    Return If(item.IsActive, "Aktif", "Nonaktif")
                Case MasterDataKind.EquipmentUnit
                    Return MasterEditDialog.StateName(item.State)
                Case Else
                    Return String.Empty
            End Select
        End Function

        Private Async Sub StartEdit(record As MasterRecord)
            If busy OrElse IsDisposed Then Return
            _PendingLoad = EditAsync(record)
            Await PendingLoad
        End Sub

        Public Async Function EditAsync(record As MasterRecord) As Task
            Dim choices As New List(Of MasterRecord)()
            Using request As New CancellationTokenSource()
                activeRequest = request
                SetBusy(True)
                status.ForeColor = Theme.Muted
                status.Text = "Menyiapkan form..."
                Try
                    Dim lookup As MasterDataKind? = If(kind = MasterDataKind.EquipmentType, CType(MasterDataKind.Category, MasterDataKind?), If(kind = MasterDataKind.EquipmentUnit OrElse kind = MasterDataKind.Package, CType(MasterDataKind.EquipmentType, MasterDataKind?), Nothing))
                    If lookup.HasValue Then
                        Dim nextOffset As Integer
                        Do
                            Dim page = Await repository.ReadMasterAsync(lookup.Value, nextOffset, 100, request.Token)
                            choices.AddRange(page.Records)
                            nextOffset += page.Records.Count
                            If nextOffset >= page.Total OrElse page.Records.Count = 0 Then Exit Do
                        Loop
                    End If
                    If IsDisposed OrElse request.IsCancellationRequested Then Return
                    cancel.Visible = False
                    Using dialog As New MasterEditDialog(repository, record, choices)
                        dialog.ShowDialog(FindForm())
                        Await dialog.PendingSave
                        If dialog.SessionExpired Then RaiseEvent SessionExpired(Me, EventArgs.Empty)
                    End Using
                Catch ex As Exception When Expected(ex)
                    HandleError(ex)
                Finally
                    activeRequest = Nothing
                    If Not IsDisposed Then SetBusy(False)
                End Try
            End Using
            If Not IsDisposed Then Await LoadAsync(offset)
        End Function

        Private Shared Function Expected(ex As Exception) As Boolean
            Return TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is System.Net.Http.HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is System.IO.InvalidDataException OrElse TypeOf ex Is System.Text.Json.JsonException
        End Function
        Private Sub HandleError(ex As Exception)
            If IsDisposed Then Return
            status.ForeColor = Theme.Maintenance
            If TypeOf ex Is UnauthorizedAccessException Then
                status.Text = ex.Message
                RaiseEvent SessionExpired(Me, EventArgs.Empty)
            ElseIf TypeOf ex Is SupabaseRequestException Then
                status.Text = ex.Message
            ElseIf TypeOf ex Is OperationCanceledException Then
                status.Text = "Pemuatan dibatalkan atau melewati batas waktu. Klik Muat ulang untuk mencoba lagi."
            Else
                status.Text = "Data belum dapat dimuat. Periksa koneksi lalu klik Muat ulang."
            End If
            If records.Count > 0 Then status.Text &= Environment.NewLine & "Data sebelumnya tetap ditampilkan."
        End Sub
        Private Sub SetBusy(value As Boolean)
            busy = value
            addButton.Enabled = Not value
            editButton.Enabled = Not value AndAlso selectedRecord IsNot Nothing
            addPhotoButton.Enabled = Not value AndAlso selectedRecord IsNot Nothing AndAlso (kind = MasterDataKind.Category OrElse kind = MasterDataKind.EquipmentType)
            reload.Enabled = Not value
            previous.Enabled = Not value AndAlso offset > 0
            following.Enabled = Not value AndAlso offset <= Integer.MaxValue - 25 AndAlso offset + records.Count < total
            cancel.Visible = value
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                activeRequest?.Cancel()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
