Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms

Namespace UI
    Public Module UiHelpers
        Public Function CreateRoundPath(rectangle As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(2, radius * 2)
            Dim safeRectangle As Rectangle = New Rectangle(rectangle.X, rectangle.Y, Math.Max(1, rectangle.Width - 1), Math.Max(1, rectangle.Height - 1))
            path.AddArc(safeRectangle.X, safeRectangle.Y, diameter, diameter, 180, 90)
            path.AddArc(safeRectangle.Right - diameter, safeRectangle.Y, diameter, diameter, 270, 90)
            path.AddArc(safeRectangle.Right - diameter, safeRectangle.Bottom - diameter, diameter, diameter, 0, 90)
            path.AddArc(safeRectangle.X, safeRectangle.Bottom - diameter, diameter, diameter, 90, 90)
            path.CloseFigure()
            Return path
        End Function
        Public Function Currency(value As Decimal) As String
            Return value.ToString("C0", CultureInfo.GetCultureInfo("id-ID"))
        End Function
        Public Function LabelOf(text As String, size As Single, color As Color, bold As Boolean) As Label
            Dim label As New Label With {.Text = text, .AutoSize = True, .ForeColor = color, .Font = New Font("Segoe UI", size, If(bold, FontStyle.Bold, FontStyle.Regular))}
            Return label
        End Function
        Public Sub StyleGrid(grid As DataGridView)
            ' Konfigurasi utama tabel
            grid.BackgroundColor = Theme.Card
            grid.BorderStyle = BorderStyle.None
            grid.EnableHeadersVisualStyles = False

            ' Header styling
            grid.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#E6EFEA")
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text
            grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
            grid.ColumnHeadersDefaultCellStyle.Padding = New Padding(10, 0, 10, 0)
            grid.ColumnHeadersHeight = 44

            ' Cell styling
            grid.DefaultCellStyle.Font = Theme.UiFont
            grid.DefaultCellStyle.ForeColor = Theme.Text
            grid.DefaultCellStyle.Padding = New Padding(10, 0, 10, 0)
            grid.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#E8F5EE")
            grid.DefaultCellStyle.SelectionForeColor = Theme.Text

            ' Alternate row styling
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 249)
            grid.AlternatingRowsDefaultCellStyle.Font = Theme.UiFont
            grid.AlternatingRowsDefaultCellStyle.ForeColor = Theme.Text

            ' Konfigurasi baris
            grid.RowTemplate.Height = 44
            grid.AllowUserToResizeRows = False

            ' Border dan grid
            grid.RowHeadersVisible = False
            grid.AllowUserToAddRows = False
            grid.AllowUserToDeleteRows = False
            grid.ReadOnly = True
            grid.MultiSelect = False
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            grid.GridColor = Theme.BorderLight

            ' Auto-sizing kolom
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            End Sub

            Public Sub ApplyColumnAlignments(grid As DataGridView)
                ' Helper untuk mengatur alignment kolom dengan benar
                ' Gunakan method ini setelah StyleGrid() untuk set alignment sesuai tipe kolom
                For Each column As DataGridViewColumn In grid.Columns
                    Select Case column.Name.ToLower()
                        Case "nomor", "pelanggan", "keterangan"
                            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft
                        Case "tgl", "tanggal", "jadwal", "created", "updated"
                            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
                        Case "status", "state"
                            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
                        Case Else
                            ' Jika kolom berisi nilai Rpiah (contains "Rp" atau numeric), rata kanan
                            If column.Name.ToLower().Contains("nilai") OrElse column.Name.ToLower().Contains("dp") OrElse column.Name.ToLower().Contains("sewa") OrElse column.Name.ToLower().Contains("harga") Then
                                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
                            End If
                    End Select
                Next
            End Sub
        Public Function PageLayout(owner As UserControl, description As String) As TableLayoutPanel
            owner.Dock = DockStyle.Fill
            owner.BackColor = Theme.Background
            owner.Font = Theme.UiFont
            owner.Padding = New Padding(28, 18, 28, 24)
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim caption = LabelOf(description, 10, Theme.Muted, False)
            caption.Dock = DockStyle.Fill
            layout.Controls.Add(caption, 0, 0)
            owner.Controls.Add(layout)
            Return layout
        End Function
        Public Function StatusText(status As Models.RentalStatus, dueAt As DateTime) As String
            If (status = Models.RentalStatus.Active OrElse status = Models.RentalStatus.Late) AndAlso dueAt < DateTime.Now Then Return "Terlambat"
            Select Case status
                Case Models.RentalStatus.Active : Return "Disewa"
                Case Models.RentalStatus.Completed : Return "Selesai"
                Case Models.RentalStatus.Cancelled : Return "Dibatalkan"
                Case Models.RentalStatus.Late : Return "Terlambat"
                Case Else : Return "Draf"
            End Select
        End Function
        Public Sub FinishPage(owner As UserControl)
            owner.Font = Theme.UiFont
            owner.AutoScaleMode = AutoScaleMode.Dpi
            owner.AutoScaleDimensions = New SizeF(96, 96)
        End Sub
    End Module
End Namespace
