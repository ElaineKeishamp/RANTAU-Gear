Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Pages
    Public Class PackagePage
        Inherits UserControl
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly packageList As New ListBox()
        Private ReadOnly detail As New Panel()
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
            BuildUI()
            UiHelpers.FinishPage(Me)
        End Sub
        Private Sub BuildUI()
            Dock = DockStyle.Fill
            BackColor = Theme.Background
            Padding = New Padding(28, 18, 28, 24)
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .BackColor = Theme.Background}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Controls.Add(layout)
            Dim title As Label = UiHelpers.LabelOf("Pilih paket untuk melihat isi, tarif, dan kesiapan setiap unit. Penyewaan dicatat melalui Booking.", 10.0F, Theme.Muted, False)
            title.Dock = DockStyle.Fill
            layout.Controls.Add(title, 0, 0)
            Dim split As New SplitContainer With {.Dock = DockStyle.Fill, .BackColor = Theme.Background}
            layout.Controls.Add(split, 0, 1)
            AddHandler split.SizeChanged, Sub() split.SplitterDistance = Math.Min(280, Math.Max(50, split.Width \ 3))
            packageList.Dock = DockStyle.Fill
            packageList.Font = Theme.UiFont
            packageList.BorderStyle = BorderStyle.None
            packageList.ItemHeight = 38
            packageList.IntegralHeight = False
            For Each item As Package In repository.GetPackages()
                packageList.Items.Add(item.Name)
            Next
            AddHandler packageList.SelectedIndexChanged, AddressOf PackageSelected
            split.Panel1.Controls.Add(packageList)
            detail.Dock = DockStyle.Fill
            detail.Padding = New Padding(24)
            detail.AutoScroll = True
            split.Panel2.Controls.Add(detail)
            If packageList.Items.Count > 0 Then packageList.SelectedIndex = 0
            If packageList.Items.Count = 0 Then detail.Controls.Add(UiHelpers.LabelOf("Belum ada paket bundling.", 12, Theme.Muted, False))
        End Sub
        Private Sub PackageSelected(sender As Object, e As EventArgs)
            While detail.Controls.Count > 0
                detail.Controls(0).Dispose()
            End While
            If packageList.SelectedIndex < 0 Then Return
            Dim item As Package = repository.GetPackages()(packageList.SelectedIndex)
            Dim content As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True}
            detail.Controls.Add(content)
            Dim heading = UiHelpers.LabelOf(item.Name, 18.0F, Theme.Primary, True)
            heading.Margin = New Padding(0, 0, 0, 10)
            content.Controls.Add(heading)
            Dim price As Label = UiHelpers.LabelOf(UiHelpers.Currency(item.DailyPrice) & " / hari", 12.0F, Theme.Accent, True)
            price.Margin = New Padding(0, 0, 0, 18)
            content.Controls.Add(price)
            For Each packageItem As PackageItem In item.Items
                Dim unit As Unit = repository.GetUnitById(packageItem.UnitId)
                Dim status As String = If(unit.Status = UnitStatus.Ready, "Stok mencukupi", "Stok kurang")
                Dim line As Label = UiHelpers.LabelOf(unit.Name & "  x" & packageItem.Quantity & "   -   " & status, 10.0F, If(unit.Status = UnitStatus.Ready, Theme.Ready, Theme.Maintenance), False)
                line.Margin = New Padding(0, 0, 0, 12)
                content.Controls.Add(line)
            Next
        End Sub
    End Class
End Namespace
