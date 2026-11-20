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
    Public Class ReturnPage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly actor As AppUser
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill}
        Private ReadOnly process As New RoundedButton With {.Text = "Periksa & kembalikan", .Width = 200, .Height = 36}
        Private ReadOnly countLabel As New Label With {.AutoSize = True, .Padding = New Padding(8), .ForeColor = Theme.Muted}
        Public Sub New(repository As IRentalRepository, actor As AppUser)
            Me.repository = repository
            Me.actor = actor
            Dim layout = UiHelpers.PageLayout(Me, "Periksa setiap unit sebelum menutup sewa. Unit bermasalah masuk ke perawatan.")
            Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill}
            toolbar.Controls.Add(process)
            toolbar.Controls.Add(countLabel)
            layout.Controls.Add(toolbar, 0, 1)
            UiHelpers.StyleGrid(grid)
            layout.Controls.Add(grid, 0, 2)
            AddHandler process.Click, Sub()
                                          If grid.CurrentRow Is Nothing Then Return
                                          Dim rental = repository.GetRentalById(CInt(grid.CurrentRow.Cells("Id").Value))
                                          Using dialog As New ReturnDialog(repository, actor, rental)
                                              If dialog.ShowDialog(Me) = DialogResult.OK Then RefreshRows()
                                          End Using
                                      End Sub
            RefreshRows()
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub RefreshRows()
            Dim rows = repository.GetRentals().Where(Function(r) r.Status = RentalStatus.Active OrElse r.Status = RentalStatus.Late).OrderBy(Function(r) r.DueAt).Select(Function(r) New With {.Id = r.Id, .Nomor = r.RentalNumber, .Pelanggan = repository.GetCustomerById(r.CustomerId).Name, .Unit = r.Items.Count, .Tenggat = r.DueAt.ToString("dd MMM yyyy HH:mm"), .Status = UiHelpers.StatusText(r.Status, r.DueAt)}).ToList()
            grid.DataSource = rows
            grid.Columns("Id").Visible = False
            process.Enabled = rows.Count > 0
            countLabel.Text = If(rows.Count = 0, "Semua unit sudah kembali. Tidak ada antrean pemeriksaan.", rows.Count.ToString() & " sewa menunggu pengembalian")
        End Sub
    End Class
End Namespace
