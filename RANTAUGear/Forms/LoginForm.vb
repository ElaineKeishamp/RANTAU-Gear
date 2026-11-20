Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.IO
Imports System.Net.Http
Imports System.Net.Mail
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Models
Imports RantauGear.UI

Namespace Forms
    Public Class LoginForm
        Inherits Form

        Private ReadOnly usernameBox As New TextBox()
        Private ReadOnly passwordBox As New TextBox()
        Private ReadOnly errorLabel As New Label()
        Private ReadOnly loginButton As New RoundedButton()
        Private ReadOnly showButton As New Button()
        Private ReadOnly cancelLoginButton As New Button With {.Text = "Batalkan", .Width = 400, .Height = 34, .Visible = False, .Cursor = Cursors.Hand}
        Private ReadOnly gatewayFactory As Func(Of SupabaseGateway)
        Private ReadOnly openWorkspace As Action(Of AppUser, IRentalRepository)
        Private request As CancellationTokenSource
        Private loggingIn As Boolean
        Private sessionEnded As Boolean
        Private pendingWorkspaceLoads As Task = Task.CompletedTask

        Public Sub New(Optional gatewayFactory As Func(Of SupabaseGateway) = Nothing, Optional openWorkspace As Action(Of AppUser, IRentalRepository) = Nothing)
            Me.gatewayFactory = If(gatewayFactory, Function() New SupabaseGateway(SupabaseOptions.LoadForApplication()))
            Me.openWorkspace = If(openWorkspace, AddressOf ShowWorkspace)
            BuildUI()
        End Sub

        Private Sub BuildUI()
            Text = "RANTAU Gear Rental - Login"
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(900, 600)
            Size = New Size(1000, 680)
            BackColor = Theme.Background
            Font = Theme.UiFont
            FormBorderStyle = FormBorderStyle.Sizable
            AutoScaleMode = AutoScaleMode.Dpi
            AcceptButton = loginButton
            MyBase.CancelButton = cancelLoginButton
            Dim split As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1}
            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))
            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60))
            Controls.Add(split)
            Dim brand As New TableLayoutPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Primary, .Padding = New Padding(42), .ColumnCount = 1, .RowCount = 4}
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 130))
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 80))
            brand.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            brand.RowStyles.Add(New RowStyle(SizeType.Absolute, 8))
            split.Controls.Add(brand, 0, 0)
            brand.Controls.Add(New Label With {.Text = "RANTAU" & Environment.NewLine & "Gear Rental", .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 26, FontStyle.Bold), .ForeColor = Color.White, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
            brand.Controls.Add(New Label With {.Text = "Rental alat outdoor dan" & Environment.NewLine & "dokumentasi", .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 13), .ForeColor = Color.FromArgb(230, 245, 238)}, 0, 1)
            brand.Controls.Add(New Label With {.Text = "Akses petugas toko" & Environment.NewLine & Environment.NewLine & "Masuk dengan email akun Supabase." & Environment.NewLine & "Koneksi internet diperlukan.", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.TopLeft, .Padding = New Padding(0, 24, 0, 0), .ForeColor = Color.White}, 0, 2)
            brand.Controls.Add(New Panel With {.BackColor = Theme.Accent, .Width = 75, .Height = 6, .Anchor = AnchorStyles.Left Or AnchorStyles.Bottom}, 0, 3)
            Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1}
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420))
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            split.Controls.Add(right, 1, 0)
            Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True, .Padding = New Padding(0, 22, 0, 22)}
            right.Controls.Add(flow, 1, 0)
            flow.Controls.Add(New Label With {.Text = "Masuk ke RANTAU Gear", .Width = 400, .Height = 42, .Font = Theme.TitleFont, .ForeColor = Theme.Text, .Margin = New Padding(0, 0, 0, 4)})
            flow.Controls.Add(New Label With {.Text = "Gunakan akun petugas yang sudah didaftarkan.", .Width = 400, .Height = 30, .ForeColor = Theme.Muted, .Margin = New Padding(0, 0, 0, 20)})
            flow.Controls.Add(New Label With {.Text = "Email", .Width = 400, .Height = 24, .Font = New Font("Segoe UI", 10, FontStyle.Bold), .ForeColor = Theme.Text})
            usernameBox.Width = 400
            usernameBox.Font = Theme.UiFont
            usernameBox.AccessibleName = "Email akun petugas"
            usernameBox.PlaceholderText = "nama@contoh.com"
            usernameBox.Margin = New Padding(0, 0, 0, 14)
            flow.Controls.Add(usernameBox)
            flow.Controls.Add(New Label With {.Text = "Password", .Width = 400, .Height = 24, .Font = New Font("Segoe UI", 10, FontStyle.Bold), .ForeColor = Theme.Text})
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
            flow.Controls.Add(passwordRow)
            errorLabel.Width = 400
            errorLabel.Height = 64
            errorLabel.ForeColor = Theme.Maintenance
            errorLabel.AccessibleName = "Status login"
            flow.Controls.Add(errorLabel)
            loginButton.Text = "Masuk"
            loginButton.Width = 400
            loginButton.Height = 42
            loginButton.Margin = New Padding(0, 0, 0, 8)
            AddHandler loginButton.Click, AddressOf LoginClicked
            flow.Controls.Add(loginButton)
            AddHandler cancelLoginButton.Click, Sub() request?.Cancel()
            flow.Controls.Add(cancelLoginButton)
            flow.Controls.Add(New Label With {.Text = "Belum memiliki akun atau akses ditolak?" & Environment.NewLine & "Hubungi Super Admin toko.", .Width = 400, .Height = 58, .ForeColor = Theme.Muted, .Font = New Font("Segoe UI", 9), .Margin = New Padding(0, 12, 0, 0)})
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub

        Private Sub TogglePassword(sender As Object, e As EventArgs)
            passwordBox.UseSystemPasswordChar = Not passwordBox.UseSystemPasswordChar
            showButton.Text = If(passwordBox.UseSystemPasswordChar, "Tampil", "Sembunyi")
        End Sub

        Private Async Sub LoginClicked(sender As Object, e As EventArgs)
            Await LoginAsync()
        End Sub

        Private Async Function LoginAsync() As Task
            If loggingIn Then Return
            errorLabel.ForeColor = Theme.Maintenance
            If String.IsNullOrWhiteSpace(usernameBox.Text) OrElse String.IsNullOrEmpty(passwordBox.Text) Then
                errorLabel.Text = "Email dan password wajib diisi."
                Return
            End If
            Dim email As MailAddress = Nothing
            If Not MailAddress.TryCreate(usernameBox.Text.Trim(), email) OrElse email.Address <> usernameBox.Text.Trim() Then
                errorLabel.Text = "Masukkan email akun petugas yang valid."
                Return
            End If
            Dim wasVisible = Visible
            loggingIn = True
            sessionEnded = False
            Dim failure As Exception = Nothing
            Dim logoutFailed As Boolean
            Using attempt As New CancellationTokenSource()
                request = attempt
                SetBusy(True)
                Try
                    Using gateway = gatewayFactory(), sessions As New SupabaseSessionManager(gateway)
                        Try
                            Await sessions.SignInAsync(email.Address, passwordBox.Text, attempt.Token)
                            Dim repository As IRentalRepository = New SupabaseRentalRepository(gateway, sessions)
                            Dim user = Await repository.GetCurrentUserAsync(attempt.Token)
                            attempt.Token.ThrowIfCancellationRequested()
                            passwordBox.Clear()
                            cancelLoginButton.Visible = False
                            Hide()
                            openWorkspace(user, repository)
                            Await pendingWorkspaceLoads
                            If wasVisible AndAlso Not IsDisposed Then
                                Show()
                                errorLabel.Text = "Menutup sesi Supabase..."
                            End If
                        Catch ex As Exception When IsExpectedFailure(ex)
                            failure = ex
                        End Try
                        Using logout As New CancellationTokenSource(TimeSpan.FromSeconds(5))
                            Try
                                Await sessions.SignOutAsync(logout.Token)
                            Catch ex As Exception When IsExpectedFailure(ex)
                                logoutFailed = True
                            End Try
                        End Using
                    End Using
                Catch ex As Exception When IsExpectedFailure(ex)
                    failure = ex
                Finally
                    request = Nothing
                    loggingIn = False
                    If Not IsDisposed Then
                        passwordBox.Clear()
                        passwordBox.UseSystemPasswordChar = True
                        showButton.Text = "Tampil"
                        SetBusy(False)
                        errorLabel.ForeColor = Theme.Maintenance
                        errorLabel.Text = FailureMessage(failure, attempt.IsCancellationRequested)
                        If failure Is Nothing AndAlso sessionEnded Then errorLabel.Text = "Sesi atau hak akses berubah. Silakan masuk kembali."
                        If logoutFailed Then errorLabel.Text &= If(errorLabel.Text.Length = 0, "", Environment.NewLine) & "Sesi lokal dihapus; logout server belum dapat dikonfirmasi."
                        If wasVisible Then Show()
                        usernameBox.Focus()
                    End If
                End Try
            End Using
        End Function

        Private Sub ShowWorkspace(user As AppUser, repository As IRentalRepository)
            Using main As New MainForm(user, repository)
                main.ShowDialog(Me)
                sessionEnded = main.SessionEnded
                pendingWorkspaceLoads = main.WaitForPendingReadsAsync()
            End Using
        End Sub

        Private Sub SetBusy(value As Boolean)
            usernameBox.Enabled = Not value
            passwordBox.Enabled = Not value
            showButton.Enabled = Not value
            loginButton.Enabled = Not value
            loginButton.Text = If(value, "Memeriksa akun...", "Masuk")
            cancelLoginButton.Visible = value
            If value Then
                errorLabel.ForeColor = Theme.Muted
                errorLabel.Text = "Memeriksa akun dan profil petugas di Supabase..."
            End If
        End Sub

        Private Shared Function IsExpectedFailure(ex As Exception) As Boolean
            Return TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse
                TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is FormatException OrElse TypeOf ex Is KeyNotFoundException
        End Function

        Private Shared Function FailureMessage(ex As Exception, cancelled As Boolean) As String
            If ex Is Nothing Then Return String.Empty
            If TypeOf ex Is OperationCanceledException Then Return If(cancelled, "Login dibatalkan.", "Koneksi melewati batas waktu. Silakan coba lagi.")
            If TypeOf ex Is HttpRequestException Then Return "Tidak dapat menghubungi Supabase. Periksa koneksi internet dan coba lagi."
            If TypeOf ex Is SupabaseRequestException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidOperationException Then Return ex.Message
            Return "Respons layanan belum dapat dibaca. Silakan coba lagi atau hubungi administrator."
        End Function

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            request?.Cancel()
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
