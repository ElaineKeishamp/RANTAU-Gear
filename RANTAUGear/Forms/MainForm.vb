Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.Pages
Imports RantauGear.UI
Imports RantauGear.Services

Namespace Forms
    Public Class MainForm
        Inherits Form
        Private ReadOnly currentUser As AppUser
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly contentPanel As New Panel()
        Private ReadOnly pageTitle As New Label()
        Private ReadOnly clockLabel As New Label()
        Private ReadOnly clockTimer As New Timer With {.Interval = 1000}
        Private ReadOnly menuButtons As New List(Of Button)()
        Private activeKey As String = "dashboard"

        Public Sub New(user As AppUser, repository As IRentalRepository)
            currentUser = user
            Me.repository = repository
            BuildUI()
        End Sub

        Private Sub BuildUI()
            Text = "RANTAU Gear Rental"
            AutoScaleMode = AutoScaleMode.Dpi
            WindowState = FormWindowState.Maximized
            MinimumSize = New Size(1100, 700)
            Size = New Size(1280, 760)
            FormBorderStyle = FormBorderStyle.Sizable
            MaximizeBox = True
            MinimizeBox = True
            BackColor = Theme.Background
            Font = Theme.UiFont

            Dim shell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
            shell.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 238))
            shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Controls.Add(shell)
            Dim sidebar As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Sidebar, .Padding = New Padding(18), .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0)}
            sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
            sidebar.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 110.0F))
            shell.Controls.Add(sidebar, 0, 0)
            Dim logo As New Label With {.Text = "RANTAU Gear", .AutoSize = False, .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 19.0F, FontStyle.Bold), .ForeColor = Color.White, .TextAlign = ContentAlignment.MiddleLeft}
            sidebar.Controls.Add(logo, 0, 0)
            Dim menu As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .BackColor = Theme.Sidebar}
            sidebar.Controls.Add(menu, 0, 1)
            AddMenu(menu, "Dashboard", "dashboard")
            If currentUser.Role = UserRole.SuperAdmin Then
                AddMenu(menu, "Katalog Unit", "catalog")
                AddMenu(menu, "Paket Bundling", "packages")
            End If
            If currentUser.Role = UserRole.SuperAdmin OrElse currentUser.Role = UserRole.FrontOffice Then AddMenu(menu, "Booking", "booking")
            If currentUser.Role = UserRole.SuperAdmin OrElse currentUser.Role = UserRole.GudangQc Then AddMenu(menu, "Pengembalian & QC", "return-qc")
            If currentUser.Role <> UserRole.GudangQc Then AddMenu(menu, "Laporan", "reports")
            If currentUser.Role = UserRole.SuperAdmin Then AddMenu(menu, "Kelola User", "users")

            Dim account As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Sidebar, .ColumnCount = 1, .RowCount = 3}
            account.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
            account.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
            account.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            sidebar.Controls.Add(account, 0, 2)
            Dim nameLabel As New Label With {.Text = currentUser.DisplayName, .AutoSize = False, .Dock = DockStyle.Fill, .ForeColor = Color.White, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)}
            account.Controls.Add(nameLabel, 0, 0)
            Dim roleLabel As New Label With {.Text = currentUser.Role.ToString(), .AutoSize = False, .Dock = DockStyle.Fill, .ForeColor = Color.LightGray, .Font = New Font("Segoe UI", 9.0F)}
            account.Controls.Add(roleLabel, 0, 1)
            Dim logout As New RoundedButton With {.Text = "Keluar akun", .NormalColor = Color.FromArgb(80, 95, 88), .Dock = DockStyle.Bottom, .Height = 38, .Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom}
            AddHandler logout.Click, Sub(sender As Object, e As EventArgs) Close()
            account.Controls.Add(logout, 0, 2)

            Dim rightShell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .BackColor = Theme.Background}
            rightShell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            rightShell.RowStyles.Add(New RowStyle(SizeType.Absolute, 78.0F))
            rightShell.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            rightShell.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
            rightShell.Margin = New Padding(0)
            shell.Controls.Add(rightShell, 1, 0)
            Dim header As New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.Card, .Padding = New Padding(28, 17, 28, 10)}
            rightShell.Controls.Add(header, 0, 0)
            pageTitle.Text = "Dashboard"
            pageTitle.Font = Theme.TitleFont
            pageTitle.ForeColor = Theme.Text
            pageTitle.Dock = DockStyle.Left
            pageTitle.AutoSize = True
            header.Controls.Add(pageTitle)
            clockLabel.Text = DateTime.Now.ToString("dd MMMM yyyy HH:mm")
            clockLabel.ForeColor = Theme.Muted
            clockLabel.Dock = DockStyle.Right
            clockLabel.AutoSize = True
            header.Controls.Add(clockLabel)
            AddHandler clockTimer.Tick, Sub(sender As Object, e As EventArgs) clockLabel.Text = DateTime.Now.ToString("dd MMMM yyyy HH:mm")
            clockTimer.Start()

            contentPanel.Dock = DockStyle.Fill
            contentPanel.Padding = New Padding(0)
            contentPanel.BackColor = Theme.Background
            rightShell.Controls.Add(contentPanel, 0, 1)
            Dim demoStatus As New Label With {.Dock = DockStyle.Fill, .Text = "Demo lokal  |  Data contoh dan perubahan hanya tersimpan selama aplikasi terbuka  |  Supabase belum terhubung", .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(28, 0, 0, 0), .ForeColor = Theme.Muted, .Font = New Font("Segoe UI", 9)}
            rightShell.Controls.Add(demoStatus, 0, 2)
            Navigate("dashboard", "Dashboard")
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub

        Private Sub AddMenu(container As FlowLayoutPanel, caption As String, key As String)
            Dim button As New Button With {.Text = "  " & caption, .Tag = key, .Width = 190, .Height = 44, .FlatStyle = FlatStyle.Flat, .BackColor = Theme.Sidebar, .ForeColor = Color.FromArgb(220, 232, 226), .TextAlign = ContentAlignment.MiddleLeft, .Font = Theme.UiFont, .Cursor = Cursors.Hand, .Margin = New Padding(0, 2, 0, 2)}
            button.FlatAppearance.BorderSize = 0
            AddHandler button.MouseEnter, Sub(sender As Object, e As EventArgs) button.BackColor = Color.FromArgb(42, 75, 61)
            AddHandler button.MouseLeave, Sub(sender As Object, e As EventArgs) button.BackColor = If(activeKey = key, Theme.Primary, Theme.Sidebar)
            AddHandler button.Click, Sub(sender As Object, e As EventArgs) Navigate(CStr(button.Tag), caption)
            container.Controls.Add(button)
            menuButtons.Add(button)
        End Sub

        Private Sub Navigate(key As String, caption As String)
            If Not New AuthService(repository).CanAccess(currentUser, key) Then Return
            activeKey = key
            For Each button In menuButtons
                button.BackColor = If(CStr(button.Tag) = key, Theme.Primary, Theme.Sidebar)
                button.AccessibleDescription = If(CStr(button.Tag) = key, "Halaman aktif", "Buka halaman")
            Next
            pageTitle.Text = caption
            While contentPanel.Controls.Count > 0
                contentPanel.Controls(0).Dispose()
            End While
            Dim page As UserControl = Nothing
            If key = "dashboard" Then page = New DashboardPage(repository)
            If key = "catalog" Then page = New CatalogPage(repository)
            If key = "packages" Then page = New PackagePage(repository)
            If key = "users" Then page = New UserPage(repository, currentUser)
            If key = "booking" Then page = New BookingPage(repository, currentUser)
            If key = "return-qc" Then page = New ReturnPage(repository, currentUser)
            If key = "reports" Then page = New ReportsPage(repository)
            If page Is Nothing Then
                ShowWelcome()
            Else
                contentPanel.Controls.Add(page)
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then clockTimer.Dispose()
            MyBase.Dispose(disposing)
        End Sub

        Private Sub ShowWelcome()
            contentPanel.Controls.Clear()
            Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(24)}
            contentPanel.Controls.Add(card)
            Dim cardLayout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
            cardLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
            cardLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            card.Controls.Add(cardLayout)
            cardLayout.Controls.Add(UiHelpers.LabelOf("RANTAU Gear Rental", 24.0F, Theme.Primary, True), 0, 0)
            Dim info As Label = UiHelpers.LabelOf("Shell aplikasi siap digunakan. Halaman operasional akan dimuat melalui menu di sisi kiri.", 11.0F, Theme.Muted, False)
            info.Dock = DockStyle.Fill
            cardLayout.Controls.Add(info, 0, 1)
        End Sub
    End Class
End Namespace
