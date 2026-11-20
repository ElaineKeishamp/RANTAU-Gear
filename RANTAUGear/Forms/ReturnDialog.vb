Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.Services
Imports RantauGear.UI
Namespace Forms
    Public Class ReturnDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly actor As AppUser
        Private ReadOnly rental As Rental
        Private ReadOnly grid As New DataGridView With {.Dock = DockStyle.Fill, .AutoGenerateColumns = False}
        Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Primary}
        Private ReadOnly errorText As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Maintenance}
        Public Sub New(repository As IRentalRepository, actor As AppUser, rental As Rental)
            Me.repository = repository
            Me.actor = actor
            Me.rental = rental
            Text = "Pengembalian " & rental.RentalNumber
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(880, 590)
            MinimumSize = New Size(780, 540)
            Font = Theme.UiFont
            BackColor = Theme.Background
            AutoScaleMode = AutoScaleMode.Dpi
            MinimizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(24), .ColumnCount = 1, .RowCount = 5}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 80))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            Controls.Add(layout)
            Dim caption = UiHelpers.LabelOf("Isi kondisi, denda kerusakan (Rp), dan catatan setiap unit." & Environment.NewLine & "Toleransi terlambat 30 menit; setelahnya 10% tarif harian per jam, dibulatkan ke atas.", 10, Theme.Muted, False)
            caption.Dock = DockStyle.Fill
            layout.Controls.Add(caption, 0, 0)
            UiHelpers.StyleGrid(grid)
            grid.ReadOnly = False
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "UnitId", .Visible = False})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Unit", .HeaderText = "Peralatan", .ReadOnly = True, .FillWeight = 140})
            grid.Columns.Add(New DataGridViewComboBoxColumn With {.Name = "Kondisi", .HeaderText = "Kondisi", .DataSource = New String() {"Baik", "Perlu perawatan"}})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Denda", .HeaderText = "Denda (Rp)", .FillWeight = 75})
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Catatan", .HeaderText = "Catatan", .FillWeight = 140})
            For Each item In rental.Items
                grid.Rows.Add(item.UnitId, repository.GetUnitById(item.UnitId).Code & " | " & item.ItemName, "Baik", "0", "")
            Next
            layout.Controls.Add(grid, 0, 1)
            layout.Controls.Add(summary, 0, 2)
            layout.Controls.Add(errorText, 0, 3)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
            Dim save As New RoundedButton With {.Text = "Selesaikan pengembalian", .Width = 230, .Height = 38}
            Dim cancel As New Button With {.Text = "Batal", .DialogResult = DialogResult.Cancel, .Width = 90, .Height = 38, .Cursor = Cursors.Hand}
            actions.Controls.Add(save)
            actions.Controls.Add(cancel)
            layout.Controls.Add(actions, 0, 4)
            CancelButton = cancel
            AddHandler save.Click, AddressOf SaveReturn
            AddHandler grid.CellEndEdit, Sub() UpdateSummary()
            UpdateSummary()
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub
        Private Function ReadChecks() As List(Of Inspection)
            Dim checks As New List(Of Inspection)
            For Each row As DataGridViewRow In grid.Rows
                Dim damage As Decimal
                If Not Decimal.TryParse(Convert.ToString(row.Cells("Denda").Value), damage) OrElse damage < 0 Then Throw New InvalidOperationException("Denda harus berupa angka nol atau lebih besar.")
                checks.Add(New Inspection With {.UnitId = CInt(row.Cells("UnitId").Value), .Condition = Convert.ToString(row.Cells("Kondisi").Value), .FineAmount = damage, .Notes = Convert.ToString(row.Cells("Catatan").Value)})
            Next
            Return checks
        End Function
        Private Sub UpdateSummary()
            Try
                Dim late = New FineCalculator().CalculateLateFine(rental)
                Dim damage = ReadChecks().Sum(Function(i) i.FineAmount)
                Dim balance = rental.Deposit - late - damage
                summary.Text = "Denda terlambat: " & UiHelpers.Currency(late) & "    |    Kerusakan: " & UiHelpers.Currency(damage) & Environment.NewLine & "Deposit: " & UiHelpers.Currency(rental.Deposit) & Environment.NewLine & If(balance >= 0, "Estimasi deposit dikembalikan: " & UiHelpers.Currency(balance), "Estimasi tambahan dibayar: " & UiHelpers.Currency(-balance))
                errorText.Text = String.Empty
            Catch ex As InvalidOperationException
                errorText.Text = ex.Message
            End Try
        End Sub
        Private Sub SaveReturn(sender As Object, e As EventArgs)
            grid.EndEdit()
            Try
                Dim service As New RentalService(repository)
                service.CompleteReturn(rental.Id, ReadChecks(), actor, DateTime.Now)
                Dim balance = rental.Deposit - rental.LateFine - rental.DamageFine
                MessageBox.Show("Pengembalian selesai." & Environment.NewLine & If(balance >= 0, "Kembalikan deposit: " & UiHelpers.Currency(balance), "Tambahan pembayaran: " & UiHelpers.Currency(-balance)), "Pengembalian tercatat", MessageBoxButtons.OK, MessageBoxIcon.Information)
                DialogResult = DialogResult.OK
            Catch ex As InvalidOperationException
                errorText.Text = ex.Message
            End Try
        End Sub
    End Class
End Namespace
