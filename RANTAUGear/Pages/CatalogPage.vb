Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Pages
    Public Class CatalogPage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly grid As New DataGridView()
        Private ReadOnly searchBox As New TextBox()
        Private ReadOnly resultLabel As New Label With {.AutoSize = True, .ForeColor = Theme.Muted, .Padding = New Padding(8, 5, 0, 0)}
        Private ReadOnly folderOpenDialog As New OpenFileDialog With {.Filter = "Gambar (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", .Title = "Pilih foto unit"}
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
            BuildUI()
            RefreshGrid()
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub BuildUI()
            Dock = DockStyle.Fill
            BackColor = Theme.Background
            Padding = New Padding(28, 18, 28, 24)
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .BackColor = Theme.Background}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 45.0F))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Controls.Add(layout)
            Dim title As Label = UiHelpers.LabelOf("Daftar peralatan dan tarif sewa. Status alat yang masih disewa hanya dapat diubah melalui QC.", 10.0F, Theme.Muted, False)
            title.Dock = DockStyle.Fill
            layout.Controls.Add(title, 0, 0)
            Dim tools As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Background}
            layout.Controls.Add(tools, 0, 1)
            searchBox.Width = 240
            searchBox.AccessibleName = "Cari unit"
            searchBox.PlaceholderText = "Cari kode atau nama unit..."
            AddHandler searchBox.TextChanged, Sub(sender As Object, e As EventArgs) RefreshGrid()
            tools.Controls.Add(UiHelpers.LabelOf("Cari unit", 10, Theme.Text, False))
            tools.Controls.Add(searchBox)
            Dim addImageButton As New RoundedButton With {.Text = "Tambah Gambar", .Width = 120, .Height = 34, .CornerRadius = 6}
            AddHandler addImageButton.Click, AddressOf TambahGambar
            tools.Controls.Add(addImageButton)
            Dim maintenance As New RoundedButton With {.Text = "Set Maintenance", .Width = 145, .Height = 34, .NormalColor = Theme.Maintenance}
            AddHandler maintenance.Click, AddressOf SetMaintenance
            tools.Controls.Add(maintenance)
            Dim ready As New RoundedButton With {.Text = "Set Ready", .Width = 110, .Height = 34, .NormalColor = Theme.Ready}
            AddHandler ready.Click, AddressOf SetReady
            tools.Controls.Add(ready)
            tools.Controls.Add(resultLabel)
            grid.Dock = DockStyle.Fill
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            UiHelpers.StyleGrid(grid)
            layout.Controls.Add(grid, 0, 2)
        End Sub
        Private Sub RefreshGrid()
            Dim rows = repository.GetUnits().Where(Function(x As Unit) x.Code.IndexOf(searchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0 OrElse x.Name.IndexOf(searchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0).Select(Function(x As Unit) New With {.Id = x.Id, .Kode = x.Code, .Nama = x.Name, .Kategori = repository.GetCategoryById(x.CategoryId).Name, .TarifHari = UiHelpers.Currency(x.DailyRate), .Status = x.Status.ToString()}).ToList()
            grid.DataSource = rows
            If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
            If grid.Columns.Contains("TarifHari") Then grid.Columns("TarifHari").HeaderText = "Tarif / hari"
            resultLabel.Text = If(rows.Count = 0, "Unit tidak ditemukan.", rows.Count.ToString() & " unit")
        End Sub
        Private Function SelectedUnit() As Unit
            If grid.CurrentRow Is Nothing Then Return Nothing
            Return repository.GetUnitById(CInt(grid.CurrentRow.Cells("Id").Value))
        End Function
        Private Sub SetMaintenance(sender As Object, e As EventArgs)
            Dim item As Unit = SelectedUnit()
            If item Is Nothing Then Return
            If Not CanChangeStatus(item) Then Return
            item.Status = UnitStatus.Maintenance
            repository.UpdateUnit(item)
            RefreshGrid()
        End Sub
        Private Sub SetReady(sender As Object, e As EventArgs)
            Dim item As Unit = SelectedUnit()
            If item Is Nothing Then Return
            If Not CanChangeStatus(item) Then Return
            item.Status = UnitStatus.Ready
            repository.UpdateUnit(item)
            RefreshGrid()
        End Sub
        Private Sub TambahGambar(sender As Object, e As EventArgs)
            If folderOpenDialog.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Dim item As Unit = SelectedUnit()
            If item Is Nothing Then
                MessageBox.Show("Pilih unit terlebih dahulu dari daftar.", "Tambah Gambar", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Try
                Dim safeName As String = String.Concat(item.Code.Split(Path.GetInvalidFileNameChars())).Trim()
                If safeName.Length = 0 Then safeName = "unit"
                Dim ext As String = Path.GetExtension(folderOpenDialog.FileName)
                If String.IsNullOrEmpty(ext) Then ext = ".png"
                Dim destPath As String = Path.Combine(Path.Combine(AppContext.BaseDirectory, "Assets"), safeName & ext)
                File.Copy(folderOpenDialog.FileName, destPath, overwrite:=True)
                MessageBox.Show("Foto berhasil disimpan untuk unit " & item.Name, "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Gagal menyimpan foto: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Sub
        Private Function CanChangeStatus(item As Unit) As Boolean
            If repository.GetRentals().Any(Function(r) (r.Status = RentalStatus.Active OrElse r.Status = RentalStatus.Late) AndAlso r.Items.Any(Function(i) i.UnitId = item.Id)) Then
                MessageBox.Show("Unit masih disewa. Selesaikan pengembalian dan QC terlebih dahulu.", "Status unit", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return False
            End If
            Return True
        End Function
    End Class
End Namespace
