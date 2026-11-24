Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.Forms
Imports RantauGear.UI
Namespace Pages
    Public Class BookingPage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly actor As AppUser
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly search As New TextBox With {.Width = 280, .PlaceholderText = "Cari nomor atau nama pelanggan", .AccessibleName = "Cari booking"}
        Private ReadOnly countLabel As New Label With {.AutoSize = True, .ForeColor = Theme.Muted, .Padding = New Padding(8)}
        Public Sub New(repository As IRentalRepository, actor As AppUser)
            Me.repository = repository
            Me.actor = actor
            Dim layout = UiHelpers.PageLayout(Me, "Catat penyewaan alat atau paket. Tarif harian dibulatkan ke atas per 24 jam.")
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
            Dim create As New RoundedButton With {.Text = "Buat booking", .Width = 150, .Height = 36, .CornerRadius = 8}
            AddHandler create.Click, Sub()
                                         Using dialog As New BookingDialog(repository, actor)
                                             If dialog.ShowDialog(Me) = DialogResult.OK Then RefreshRows()
                                         End Using
                                     End Sub
            toolbar.Controls.Add(create)
            toolbar.Controls.Add(UiHelpers.LabelOf("Cari booking", 10, Theme.Text, False))
            search.Height = 34
            search.BorderStyle = BorderStyle.FixedSingle
            toolbar.Controls.Add(search)
            toolbar.Controls.Add(countLabel)
            layout.Controls.Add(toolbar, 0, 1)
            UiHelpers.StyleGrid(grid)
            UiHelpers.ApplyColumnAlignments(grid)
            layout.Controls.Add(grid, 0, 2)
            AddHandler search.TextChanged, Sub() RefreshRows()
            RefreshRows()
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub RefreshRows()
            Dim rows = repository.GetRentals().OrderByDescending(Function(r) r.Id).Where(Function(r) r.RentalNumber.Contains(search.Text, StringComparison.OrdinalIgnoreCase) OrElse repository.GetCustomerById(r.CustomerId).Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).Select(Function(r) New With {.Nomor = r.RentalNumber, .Pelanggan = repository.GetCustomerById(r.CustomerId).Name, .Ambil = r.StartAt.ToString("dd MMM HH:mm"), .Kembali = r.DueAt.ToString("dd MMM HH:mm"), .Sewa = UiHelpers.Currency(r.Subtotal), .Deposit = UiHelpers.Currency(r.Deposit), .Status = UiHelpers.StatusText(r.Status, r.DueAt)}).ToList()
            grid.DataSource = rows
            countLabel.Text = If(rows.Count = 0, "Tidak ada booking yang cocok.", rows.Count.ToString() & " transaksi")
        End Sub
    End Class
End Namespace
