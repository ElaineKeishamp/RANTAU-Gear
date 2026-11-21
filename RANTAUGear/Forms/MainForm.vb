Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Threading.Tasks
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
        Private ReadOnly onlinePages As New List(Of IWorkspacePage)()
        Private activeKey As String = "dashboard"
        Public ReadOnly Property SessionEnded As Boolean

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
            BackColor = Theme.Background
            Font = Theme.UiFont
            Dim shell As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
            shell.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 238))
            shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Controls.Add(shell)
            Dim sidebar As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Sidebar, .Padding = New Padding(18), .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0)}
            sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
            sidebar.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            sidebar.RowStyles.Add(New RowStyle(SizeType.Absolute, 110))
            shell.Controls.Add(sidebar, 0, 0)
            sidebar.Controls.Add(New Label With {.Text = "RANTAU Gear", .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 19, FontStyle.Bold), .ForeColor = Color.White, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
            Dim menu As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .BackColor = Theme.Sidebar}
            sidebar.Controls.Add(menu, 0, 1)
            AddMenu(menu, "Dashboard", "dashboard")
            If repository.IsOnline AndAlso (currentUser.Role = UserRole.SuperAdmin OrElse currentUser.Role = UserRole.FrontOffice) Then AddMenu(menu, "Pelanggan", "customers")
            If currentUser.Role = UserRole.SuperAdmin Then
                AddMenu(menu, "Katalog Unit", "catalog")
                AddMenu(menu, "Paket Bundling", "packages")
            End If
            If currentUser.Role = UserRole.SuperAdmin OrElse currentUser.Role = UserRole.FrontOffice Then AddMenu(menu, "Booking", "booking")
            If currentUser.Role = UserRole.SuperAdmin OrElse currentUser.Role = UserRole.GudangQc Then AddMenu(menu, "Pengembalian / QC", "return-qc")
            If currentUser.Role <> UserRole.GudangQc Then AddMenu(menu, "Laporan", "reports")
            If currentUser.Role = UserRole.SuperAdmin Then AddMenu(menu, "Kelola User", "users")
            Dim account As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Sidebar, .ColumnCount = 1, .RowCount = 3}
            account.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
            account.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
            account.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            sidebar.Controls.Add(account, 0, 2)
            account.Controls.Add(New Label With {.Text = currentUser.DisplayName, .Dock = DockStyle.Fill, .ForeColor = Color.White, .Font = New Font("Segoe UI", 10, FontStyle.Bold)}, 0, 0)
            Dim roleText = If(currentUser.Role = UserRole.SuperAdmin, "Super Admin", If(currentUser.Role = UserRole.FrontOffice, "Front office", "Gudang / QC"))
            account.Controls.Add(New Label With {.Text = roleText, .Dock = DockStyle.Fill, .ForeColor = Color.LightGray, .Font = New Font("Segoe UI", 9)}, 0, 1)
            Dim logout As New RoundedButton With {.Text = "Keluar akun", .NormalColor = Color.FromArgb(80, 95, 88), .Dock = DockStyle.Bottom, .Height = 38}
            AddHandler logout.Click, Sub() Close()
            account.Controls.Add(logout, 0, 2)
            Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .BackColor = Theme.Background, .Margin = New Padding(0)}
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            right.RowStyles.Add(New RowStyle(SizeType.Absolute, 78))
            right.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            right.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
            shell.Controls.Add(right, 1, 0)
            Dim header As New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.Card, .Padding = New Padding(28, 17, 28, 10)}
            right.Controls.Add(header, 0, 0)
            pageTitle.Font = Theme.TitleFont
            pageTitle.ForeColor = Theme.Text
            pageTitle.Dock = DockStyle.Left
            pageTitle.AutoSize = True
            header.Controls.Add(pageTitle)
            clockLabel.Text = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("dd MMMM yyyy HH:mm") & " WIB"
            clockLabel.ForeColor = Theme.Muted
            clockLabel.Dock = DockStyle.Right
            clockLabel.AutoSize = True
            header.Controls.Add(clockLabel)
            AddHandler clockTimer.Tick, Sub() clockLabel.Text = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("dd MMMM yyyy HH:mm") & " WIB"
            clockTimer.Start()
            contentPanel.Dock = DockStyle.Fill
            contentPanel.BackColor = Theme.Background
            right.Controls.Add(contentPanel, 0, 1)
            Dim footer = If(repository.IsOnline, "Supabase  |  Data master tersimpan online  |  Form transaksi belum diaktifkan", "Pratinjau demo lokal untuk pengujian; perubahan tidak disimpan online")
            right.Controls.Add(New Label With {.Dock = DockStyle.Fill, .Text = footer, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(28, 0, 16, 0), .ForeColor = Theme.Muted, .Font = New Font("Segoe UI", 9)}, 0, 2)
            Navigate("dashboard", "Dashboard")
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub

        Private Sub AddMenu(container As FlowLayoutPanel, caption As String, key As String)
            Dim button As New Button With {.Text = "  " & caption, .Tag = key, .Width = 190, .Height = 44, .FlatStyle = FlatStyle.Flat, .BackColor = Theme.Sidebar, .ForeColor = Color.FromArgb(220, 232, 226), .TextAlign = ContentAlignment.MiddleLeft, .Font = Theme.UiFont, .Cursor = Cursors.Hand, .Margin = New Padding(0, 2, 0, 2)}
            button.FlatAppearance.BorderSize = 0
            AddHandler button.MouseEnter, Sub() button.BackColor = Color.FromArgb(42, 75, 61)
            AddHandler button.MouseLeave, Sub() button.BackColor = If(activeKey = key, Theme.Primary, Theme.Sidebar)
            AddHandler button.Click, Sub() Navigate(CStr(button.Tag), caption)
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
            If repository.IsOnline Then
                Select Case key
                    Case "catalog" : page = New MasterDataPage(repository)
                    Case "packages" : page = New MasterDataPage(repository, MasterDataKind.Package)
                    Case "customers" : page = New MasterDataPage(repository, MasterDataKind.Customer)
                    Case Else : page = New OnlineDataPage(repository, key)
                End Select
                Dim online = DirectCast(page, IWorkspacePage)
                onlinePages.Add(online)
                AddHandler online.SessionExpired, Sub()
                                                     _SessionEnded = True
                                                     Close()
                                                 End Sub
            Else
                If key = "dashboard" Then page = New DashboardPage(repository)
                If key = "catalog" Then page = New CatalogPage(repository)
                If key = "packages" Then page = New PackagePage(repository)
                If key = "users" Then page = New UserPage(repository, currentUser)
                If key = "booking" Then page = New BookingPage(repository, currentUser)
                If key = "return-qc" Then page = New ReturnPage(repository, currentUser)
                If key = "reports" Then page = New ReportsPage(repository)
            End If
            If page IsNot Nothing Then contentPanel.Controls.Add(page)
        End Sub

        Public Function WaitForPendingReadsAsync() As Task
            Return Task.WhenAll(onlinePages.Select(Function(page) page.PendingLoad))
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then clockTimer.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
