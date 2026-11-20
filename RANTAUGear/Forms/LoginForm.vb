Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Services
Imports RantauGear.UI

Namespace Forms
    Public Class LoginForm
        Inherits Form
        Private ReadOnly repository As IRentalRepository = New DummyRepository()
        Private ReadOnly usernameBox As New TextBox()
        Private ReadOnly passwordBox As New TextBox()
        Private ReadOnly errorLabel As New Label()
        Private ReadOnly loginButton As New RoundedButton()
        Private ReadOnly showButton As New Button()

        Public Sub New()
            BuildUI()
        End Sub

        Private Sub BuildUI()
            Text = "RANTAU Gear Rental - Login"
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(900, 600)
            Size = New Size(1000, 640)
            BackColor = Theme.Background
            Font = Theme.UiFont
            FormBorderStyle = FormBorderStyle.Sizable
            MaximizeBox = True
            MinimizeBox = True
            AutoScaleMode = AutoScaleMode.Dpi
            AcceptButton = loginButton

            Dim split As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1}
            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40.0F))
            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60.0F))
            Controls.Add(split)

            Dim brand As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Primary, .Padding = New Padding(42), .ColumnCount = 1, .RowCount = 4}
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 130.0F))
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 80.0F))
            brand.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 8.0F))
            split.Controls.Add(brand, 0, 0)
            Dim brandTitle As New Label With {.Text = "RANTAU" & Environment.NewLine & "Gear Rental", .AutoSize = False, .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 26.0F, FontStyle.Bold), .ForeColor = Color.White, .TextAlign = ContentAlignment.MiddleLeft}
            brand.Controls.Add(brandTitle, 0, 0)
            Dim brandSub As New Label With {.Text = "Rental alat outdoor &" & Environment.NewLine & "dokumentasi", .AutoSize = False, .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 13.0F), .ForeColor = Color.FromArgb(230, 245, 238), .TextAlign = ContentAlignment.TopLeft}
            brand.Controls.Add(brandSub, 0, 1)
            Dim demoNote As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.BottomLeft, .ForeColor = Color.White, .Text = "MODE DEMO LOKAL" & Environment.NewLine & Environment.NewLine & "Data contoh untuk mencoba alur rental." & Environment.NewLine & "Perubahan hanya tersimpan selama aplikasi terbuka." & Environment.NewLine & Environment.NewLine & "Database online belum terhubung."}
            brand.Controls.Add(demoNote, 0, 2)
            Dim accent As New Panel With {.BackColor = Theme.Accent, .Width = 75, .Height = 6, .Anchor = AnchorStyles.Left Or AnchorStyles.Bottom}
            brand.Controls.Add(accent, 0, 3)

            Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Background, .ColumnCount = 3, .RowCount = 1}
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420.0F))
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            split.Controls.Add(right, 1, 0)
            Dim formFlow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True, .Padding = New Padding(0, 22, 0, 22), .BackColor = Theme.Background}
            right.Controls.Add(formFlow, 1, 0)

            Dim title As New Label With {.Text = "Selamat datang kembali", .AutoSize = False, .Width = 400, .Height = 42, .Font = Theme.TitleFont, .ForeColor = Theme.Text, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(0, 0, 0, 4)}
            formFlow.Controls.Add(title)
            Dim subtitle As New Label With {.Text = "Masuk untuk mengelola penyewaan RANTAU Gear.", .AutoSize = False, .Width = 400, .Height = 30, .ForeColor = Theme.Muted, .Margin = New Padding(0, 0, 0, 20)}
            formFlow.Controls.Add(subtitle)
            Dim usernameLabel As New Label With {.Text = "Username", .AutoSize = False, .Width = 400, .Height = 24, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .ForeColor = Theme.Text}
            formFlow.Controls.Add(usernameLabel)
            usernameBox.Width = 400
            usernameBox.Height = 34
            usernameBox.Font = Theme.UiFont
            usernameBox.AccessibleName = "Username"
            usernameBox.Margin = New Padding(0, 0, 0, 14)
            formFlow.Controls.Add(usernameBox)
            Dim passwordLabel As New Label With {.Text = "Password", .AutoSize = False, .Width = 400, .Height = 24, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .ForeColor = Theme.Text}
            formFlow.Controls.Add(passwordLabel)
            Dim passwordRow As New Panel With {.Width = 400, .Height = 34, .Margin = New Padding(0, 0, 0, 14)}
            passwordBox.Dock = DockStyle.Fill
            passwordBox.Font = Theme.UiFont
            passwordBox.UseSystemPasswordChar = True
            passwordBox.AccessibleName = "Password"
            passwordRow.Controls.Add(passwordBox)
            showButton.Text = "Tampil"
            showButton.Dock = DockStyle.Right
            showButton.Width = 82
            showButton.Cursor = Cursors.Hand
            showButton.FlatStyle = FlatStyle.Flat
            showButton.FlatAppearance.BorderSize = 0
            showButton.ForeColor = Theme.Primary
            showButton.BackColor = Theme.Background
            AddHandler showButton.Click, AddressOf TogglePassword
            passwordRow.Controls.Add(showButton)
            formFlow.Controls.Add(passwordRow)

            errorLabel.AutoSize = False
            errorLabel.Width = 400
            errorLabel.Height = 30
            errorLabel.ForeColor = Color.Firebrick
            formFlow.Controls.Add(errorLabel)
            loginButton.Text = "Masuk"
            loginButton.Width = 400
            loginButton.Height = 42
            loginButton.Margin = New Padding(0, 0, 0, 18)
            AddHandler loginButton.Click, AddressOf LoginClicked
            formFlow.Controls.Add(loginButton)
            Dim demo As New Label With {.Text = "Akun demo:" & Environment.NewLine & "superadmin / admin123" & Environment.NewLine & "front / front123" & Environment.NewLine & "gudang / gudang123", .AutoSize = False, .Width = 400, .Height = 82, .ForeColor = Theme.Muted, .Font = New Font("Segoe UI", 9.0F), .Margin = New Padding(0)}
            formFlow.Controls.Add(demo)
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub

        Private Sub TogglePassword(sender As Object, e As EventArgs)
            passwordBox.UseSystemPasswordChar = Not passwordBox.UseSystemPasswordChar
            showButton.Text = If(passwordBox.UseSystemPasswordChar, "Tampil", "Sembunyi")
        End Sub

        Private Sub LoginClicked(sender As Object, e As EventArgs)
            Dim user = New AuthService(repository).Login(usernameBox.Text, passwordBox.Text)
            If user Is Nothing Then
                errorLabel.Text = "Username atau password tidak sesuai."
                Return
            End If
            Hide()
            Using main As New MainForm(user, repository)
                main.ShowDialog(Me)
            End Using
            passwordBox.Clear()
            errorLabel.Text = String.Empty
            Show()
            usernameBox.Focus()
        End Sub
    End Class
End Namespace
