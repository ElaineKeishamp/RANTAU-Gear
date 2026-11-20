Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.UI
Namespace Pages
    Public Class DashboardPage
        Inherits UserControl
        Public Sub New(repository As IRentalRepository)
            Dock = DockStyle.Fill
            Font = Theme.UiFont
            BackColor = Theme.Background
            Padding = New Padding(28, 20, 28, 24)
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For Each height As Integer In {58, 124, 66, 42}
                layout.RowStyles.Add(New RowStyle(SizeType.Absolute, height))
            Next
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Controls.Add(layout)
            Dim heading = UiHelpers.LabelOf("Peralatan siap. Perjalanan dimulai.", 21, Theme.Primary, True)
            heading.Dock = DockStyle.Fill
            layout.Controls.Add(heading, 0, 0)
            Dim stats As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 1}
            For n = 0 To 3
                stats.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
            Next
            layout.Controls.Add(stats, 0, 1)
            AddMetric(stats, 0, "Total peralatan", repository.GetUnits().Count, Theme.Text)
            AddMetric(stats, 1, "Siap disewa", repository.GetUnits().Where(Function(u) u.Status = UnitStatus.Ready).Count(), Theme.Primary)
            AddMetric(stats, 2, "Sedang disewa", repository.GetUnits().Where(Function(u) u.Status = UnitStatus.Rented).Count(), Theme.Rented)
            AddMetric(stats, 3, "Dalam perawatan", repository.GetUnits().Where(Function(u) u.Status = UnitStatus.Maintenance).Count(), Theme.Maintenance)
            Dim active = repository.GetRentals().Where(Function(r) r.Status = RentalStatus.Active OrElse r.Status = RentalStatus.Late).OrderBy(Function(r) r.DueAt).ToList()
            Dim late = active.Where(Function(r) r.DueAt < DateTime.Now).Count()
            Dim notice As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(14, 0, 14, 0), .BackColor = Color.FromArgb(248, 237, 215), .ForeColor = Theme.Rented, .Text = If(late > 0, late.ToString() & " sewa melewati tenggat. Prioritaskan pengembalian dan pemeriksaan unit.", "Tidak ada sewa terlambat. Periksa daftar pengembalian berikutnya."), .Margin = New Padding(0, 12, 0, 8)}
            layout.Controls.Add(notice, 0, 2)
            Dim title = UiHelpers.LabelOf("Pengembalian berikutnya  /  " & active.Count.ToString() & " sewa berjalan", 12, Theme.Text, True)
            title.Dock = DockStyle.Fill
            title.TextAlign = ContentAlignment.MiddleLeft
            layout.Controls.Add(title, 0, 3)
            Dim grid As New DataGridView With {.Dock = DockStyle.Fill}
            UiHelpers.StyleGrid(grid)
            grid.DataSource = active.Select(Function(r) New With {.Nomor = r.RentalNumber, .Pelanggan = repository.GetCustomerById(r.CustomerId).Name, .Unit = r.Items.Count, .Tenggat = r.DueAt.ToString("dd MMM yyyy HH:mm"), .Status = UiHelpers.StatusText(r.Status, r.DueAt)}).ToList()
            layout.Controls.Add(grid, 0, 4)
            If active.Count = 0 Then
                grid.Visible = False
                Dim empty = UiHelpers.LabelOf("Belum ada sewa berjalan. Buat transaksi melalui menu Booking.", 12, Theme.Muted, False)
                empty.Dock = DockStyle.Fill
                layout.Controls.Add(empty, 0, 4)
            End If
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub AddMetric(parent As TableLayoutPanel, column As Integer, caption As String, value As Integer, color As Color)
            Dim panel As New RoundedPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(18, 12, 18, 12), .Margin = New Padding(0, 0, If(column = 3, 0, 12), 0), .CornerRadius = 8}
            parent.Controls.Add(panel, column, 0)
            Dim content As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))
            content.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            panel.Controls.Add(content)
            content.Controls.Add(UiHelpers.LabelOf(caption, 10, Theme.Muted, False), 0, 0)
            Dim figure = UiHelpers.LabelOf(value.ToString("D2"), 28, color, True)
            figure.Dock = DockStyle.Fill
            content.Controls.Add(figure, 0, 1)
        End Sub
    End Class
End Namespace
