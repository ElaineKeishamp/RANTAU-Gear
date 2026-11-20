Option Strict On
Option Explicit On
Option Infer On

Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Data.Supabase
Imports RantauGear.Forms
Imports RantauGear.Models
Imports RantauGear.Pages
Imports RantauGear.Services

Module OnlineUiChecks
    Private Const OutputRoot As String = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-online-ui"
    Private passed As Integer

    Private Sub Check(ok As Boolean, label As String)
        If Not ok Then Throw New Exception("FAIL: " & label)
        passed += 1
        Console.WriteLine("PASS: " & label)
    End Sub

    Private Function Field(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.NonPublic Or BindingFlags.Instance).GetValue(target), T)
    End Function

    Private Sub Wait(task As Task)
        Dim timeout = Stopwatch.StartNew()
        While Not task.IsCompleted
            Application.DoEvents()
            If timeout.Elapsed > TimeSpan.FromSeconds(12) Then Throw New TimeoutException("UI check did not complete")
            Thread.Sleep(5)
        End While
        task.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub

    Private Function LoginTask(form As LoginForm) As Task
        Return DirectCast(GetType(LoginForm).GetMethod("LoginAsync", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(form, Nothing), Task)
    End Function

    Private Iterator Function ControlsOf(parent As Control) As IEnumerable(Of Control)
        For Each item As Control In parent.Controls
            Yield item
            For Each nested In ControlsOf(item)
                Yield nested
            Next
        Next
    End Function

    Private Sub Credentials(form As LoginForm, Optional password As String = "TEST_PASSWORD")
        Field(Of TextBox)(form, "usernameBox").Text = "staff@example.test"
        Field(Of TextBox)(form, "passwordBox").Text = password
    End Sub

    Private Sub Save(form As Form, name As String)
        form.PerformLayout()
        Application.DoEvents()
        Using bitmap As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, form.Size))
            bitmap.Save(Path.Combine(OutputRoot, name), Imaging.ImageFormat.Png)
        End Using
    End Sub

    Public Function Run() As Integer
        passed = 0
        Directory.CreateDirectory(OutputRoot)
        Dim options As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        Using server As New FakeSupabaseServer(), client As New HttpClient(server)
            Dim opened As Boolean
            Using form As New LoginForm(Function() New SupabaseGateway(options, client), Sub(user, repo) opened = user.Role = UserRole.SuperAdmin AndAlso repo.IsOnline)
                form.Show()
                Application.DoEvents()
                Save(form, "login-online.png")
                Field(Of Button)(form, "showButton").PerformClick()
                Check(Not Field(Of TextBox)(form, "passwordBox").UseSystemPasswordChar, "Show-password button responds to a real click")
                Field(Of Button)(form, "showButton").PerformClick()
                Check(form.SelectNextControl(Field(Of TextBox)(form, "usernameBox"), True, True, True, True), "Login permits native Tab navigation")
                GetType(Form).GetMethod("ProcessDialogKey", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(form, {Keys.Enter})
                Check(Field(Of Label)(form, "errorLabel").Text.Contains("wajib diisi"), "Enter activates login validation")
                Credentials(form, "wrong")
                Wait(LoginTask(form))
                Check(Not opened AndAlso Field(Of Label)(form, "errorLabel").Text.Contains("tidak sesuai"), "Wrong online password cannot open workspace")
                Credentials(form)
                Wait(LoginTask(form))
                Check(opened AndAlso server.LogoutCount = 1, "Online login loads staff role and revokes session after workspace exits")
                Check(Field(Of TextBox)(form, "passwordBox").Text.Length = 0 AndAlso Field(Of TextBox)(form, "passwordBox").UseSystemPasswordChar, "Login clears and remasks password after attempt")
                Check(Field(Of RantauGear.UI.RoundedButton)(form, "loginButton").Enabled, "Login controls recover after async attempt")
                form.Size = New Size(900, 600)
                Save(form, "login-online-minimum.png")
                Check(form.AutoScaleMode = AutoScaleMode.Dpi, "Online login retains DPI scaling")
                form.Close()
            End Using
        End Using
        For Each mode In {"missing", "inactive"}
            Using server As New FakeSupabaseServer With {.HasProfile = mode <> "missing", .Active = mode <> "inactive"}, client As New HttpClient(server)
                Dim opened As Boolean
                Using form As New LoginForm(Function() New SupabaseGateway(options, client), Sub(user, repo) opened = True)
                    Credentials(form)
                    Wait(LoginTask(form))
                    Check(Not opened AndAlso server.LogoutCount = 1, "Login rejects and cleans up " & mode & " staff profile")
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer With {.DelayReads = True}, client As New HttpClient(server)
            Dim opened As Boolean
            Using form As New LoginForm(Function() New SupabaseGateway(options, client), Sub(user, repo) opened = True)
                form.Show()
                Credentials(form)
                Dim login = LoginTask(form)
                Application.DoEvents()
                Check(Not Field(Of RantauGear.UI.RoundedButton)(form, "loginButton").Enabled AndAlso Not login.IsCompleted, "Pending login visibly disables repeat submission")
                Check(LoginTask(form).IsCompleted, "Duplicate login attempt does not start another request")
                GetType(Form).GetMethod("ProcessDialogKey", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(form, {Keys.Escape})
                Wait(login)
                Check(Not opened AndAlso Field(Of Label)(form, "errorLabel").Text.Contains("dibatalkan"), "Escape cancels pending login and restores the form")
                form.Close()
            End Using
        End Using
        Using server As New FakeSupabaseServer With {.Offline = True}, client As New HttpClient(server)
            Dim opened As Boolean
            Using form As New LoginForm(Function() New SupabaseGateway(options, client), Sub(user, repo) opened = True)
                Credentials(form)
                Wait(LoginTask(form))
                Check(Not opened AndAlso Field(Of Label)(form, "errorLabel").Text.Contains("internet"), "Offline login shows connection error without demo fallback")
            End Using
        End Using
        For Each role In {"super_admin", "front_office", "warehouse_qc"}
            Using server As New FakeSupabaseServer With {.StaffRole = role}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim profile = repo.GetCurrentUserAsync(CancellationToken.None)
                Wait(profile)
                Dim user = profile.Result
                Using main As New MainForm(user, repo)
                    main.WindowState = FormWindowState.Normal
                    main.Size = New Size(1280, 800)
                    main.Show()
                    Application.DoEvents()
                    Dim buttons = ControlsOf(main).OfType(Of Button)().Where(Function(button) TypeOf button.Tag Is String).ToArray()
                    Dim expected = {"dashboard", "catalog", "packages", "booking", "return-qc", "reports", "users"}.Where(Function(key) New AuthService(repo).CanAccess(user, key)).OrderBy(Function(key) key)
                    Check(buttons.Select(Function(button) CStr(button.Tag)).OrderBy(Function(key) key).SequenceEqual(expected), "Online navigation matches server role " & role)
                    For Each button In buttons
                        button.PerformClick()
                        Application.DoEvents()
                        Dim page = ControlsOf(main).OfType(Of OnlineDataPage)().Single()
                        Wait(page.PendingLoad)
                        Check(Field(Of DataGridView)(page, "grid").DataSource IsNot Nothing, "Online menu opens data page " & role & "/" & CStr(button.Tag))
                    Next
                    buttons.Single(Function(button) CStr(button.Tag) = "dashboard").PerformClick()
                    Application.DoEvents()
                    Dim dashboard = ControlsOf(main).OfType(Of OnlineDataPage)().Single()
                    Wait(dashboard.PendingLoad)
                    Field(Of Button)(dashboard, "following").PerformClick()
                    Wait(dashboard.PendingLoad)
                    Check(CStr(Field(Of DataGridView)(dashboard, "grid").Rows(0).Cells(0).Value) = "UJI-026", "Next page reads the next server page for " & role)
                    Field(Of Button)(dashboard, "previous").PerformClick()
                    Wait(dashboard.PendingLoad)
                    Check(CStr(Field(Of DataGridView)(dashboard, "grid").Rows(0).Cells(0).Value) = "UJI-001", "Previous page returns to first server page for " & role)
                    Field(Of Button)(dashboard, "following").PerformClick()
                    Wait(dashboard.PendingLoad)
                    Field(Of Button)(dashboard, "first").PerformClick()
                    Wait(dashboard.PendingLoad)
                    server.DelayReads = True
                    Dim delayed = dashboard.LoadPageAsync(0)
                    Field(Of Button)(dashboard, "cancel").PerformClick()
                    Wait(delayed)
                    Check(Field(Of Label)(dashboard, "status").Text.Contains("dibatalkan"), "Cancel preserves previous data for " & role)
                    server.DelayReads = False
                    server.Offline = True
                    Wait(dashboard.LoadPageAsync(0))
                    Check(Field(Of Label)(dashboard, "status").Text.Contains("internet") AndAlso Field(Of DataGridView)(dashboard, "grid").Rows.Count = 25, "Read error retains clearly marked previous data for " & role)
                    server.Offline = False
                    Wait(dashboard.LoadPageAsync(0))
                    Field(Of Button)(dashboard, "reload").PerformClick()
                    Wait(dashboard.PendingLoad)
                    Check(Field(Of Label)(dashboard, "status").Text.Contains("Diperbarui"), "Reload button refreshes online data for " & role)
                    If role = "super_admin" Then Save(main, "online-admin-fixture.png")
                    If role = "warehouse_qc" Then Save(main, "online-qc-fixture.png")
                    server.EmptyData = True
                    Wait(dashboard.LoadPageAsync(0))
                    Check(Field(Of Label)(dashboard, "empty").Visible AndAlso Field(Of Label)(dashboard, "status").Text.Contains("dari 0 data"), "Empty online database has an explicit state for " & role)
                    If role = "super_admin" Then Save(main, "online-empty-fixture.png")
                    server.EmptyData = False
                    server.DelayReads = True
                    Dim pendingClose = dashboard.LoadPageAsync(0)
                    Dim other = buttons.First(Function(button) CStr(button.Tag) <> "dashboard")
                    other.PerformClick()
                    Application.DoEvents()
                    Dim replacement = ControlsOf(main).OfType(Of OnlineDataPage)().Single()
                    server.DelayReads = False
                    Field(Of Button)(replacement, "cancel").PerformClick()
                    Wait(replacement.PendingLoad)
                    Wait(pendingClose)
                    Check(pendingClose.IsCompletedSuccessfully, "Navigation cancels disposed page without using a disposed session for " & role)
                    buttons.Single(Function(button) CStr(button.Tag) = "dashboard").PerformClick()
                    Application.DoEvents()
                    dashboard = ControlsOf(main).OfType(Of OnlineDataPage)().Single()
                    Wait(dashboard.PendingLoad)
                    server.Active = False
                    Wait(dashboard.LoadPageAsync(0))
                    Check(main.SessionEnded, "Inactive session returns to login for " & role)
                    Wait(main.WaitForPendingReadsAsync())
                End Using
                server.Active = True
                Using logoutForm As New MainForm(user, repo)
                    logoutForm.Show()
                    Application.DoEvents()
                    Wait(ControlsOf(logoutForm).OfType(Of OnlineDataPage)().Single().PendingLoad)
                    ControlsOf(logoutForm).OfType(Of Button)().Single(Function(button) button.Text = "Keluar akun").PerformClick()
                    Wait(logoutForm.WaitForPendingReadsAsync())
                    Check(Not logoutForm.Visible AndAlso Not logoutForm.SessionEnded, "Logout button exits workspace for " & role)
                End Using
                Wait(session.SignOutAsync(CancellationToken.None))
            End Using
        Next
        Return passed
    End Function
End Module
