Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Pages
    Public Class UserPage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly grid As New DataGridView()
        Private ReadOnly currentUser As AppUser
        Public Sub New(repository As IRentalRepository, currentUser As AppUser)
            Me.repository = repository
            Me.currentUser = currentUser
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
            Dim title As Label = UiHelpers.LabelOf("Atur akses akun demo. Akun yang sedang digunakan tetap aktif.", 10.0F, Theme.Muted, False)
            title.Dock = DockStyle.Fill
            layout.Controls.Add(title, 0, 0)
            Dim tool As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
            layout.Controls.Add(tool, 0, 1)
            Dim toggle As New RoundedButton With {.Text = "Aktifkan / Nonaktifkan", .Width = 190, .Height = 34}
            AddHandler toggle.Click, AddressOf ToggleUser
            tool.Controls.Add(toggle)
            grid.Dock = DockStyle.Fill
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            UiHelpers.StyleGrid(grid)
            layout.Controls.Add(grid, 0, 2)
        End Sub
        Private Sub RefreshGrid()
            grid.DataSource = repository.GetUsers().Select(Function(x As AppUser) New With {.Id = x.Id, .Username = x.Username, .Nama = x.DisplayName, .Role = x.Role.ToString(), .Status = If(x.IsActive, "Aktif", "Nonaktif")}).ToList()
            If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
        End Sub
        Private Sub ToggleUser(sender As Object, e As EventArgs)
            If grid.CurrentRow Is Nothing Then Return
            Dim item As AppUser = repository.GetUsers().FirstOrDefault(Function(x As AppUser) x.Id = CInt(grid.CurrentRow.Cells("Id").Value))
            If item Is Nothing Then Return
            If item.Id = currentUser.Id Then
                MessageBox.Show("Akun yang sedang digunakan tidak dapat dinonaktifkan.", "Kelola User", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            item.IsActive = Not item.IsActive
            repository.UpdateUser(item)
            RefreshGrid()
        End Sub
    End Class
End Namespace
