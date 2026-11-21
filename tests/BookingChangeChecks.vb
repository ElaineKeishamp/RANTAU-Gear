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
Module BookingChangeChecks
    Private passed As Integer
    Private Sub Check(ok As Boolean, label As String)
        If Not ok Then Throw New Exception("FAIL: " & label)
        passed += 1 : Console.WriteLine("PASS: " & label)
    End Sub
    Private Function Field(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.NonPublic Or BindingFlags.Instance).GetValue(target), T)
    End Function
    Private Sub Wait(task As Task)
        Dim watch = Stopwatch.StartNew()
        While Not task.IsCompleted
            Application.DoEvents() : Thread.Sleep(5)
            If watch.Elapsed.TotalSeconds > 12 Then Throw New TimeoutException("Booking change UI timeout")
        End While
        task.GetAwaiter().GetResult() : Application.DoEvents()
    End Sub
    Private Sub Preview(dialog As BookingChangeDialog)
        Field(Of Button)(dialog, "previewButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Private Sub Save(dialog As BookingChangeDialog)
        Field(Of CheckBox)(dialog, "confirmed").Checked = True
        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Private Sub Prepare(dialog As BookingChangeDialog, action As BookingChangeAction)
        If action = BookingChangeAction.Extend Then Preview(dialog)
        If action = BookingChangeAction.Cancel Then Field(Of TextBox)(dialog, "reasonBox").Text = "Pelanggan membatalkan perjalanan"
    End Sub
    Private Sub Screenshot(dialog As Form, name As String)
        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-changes-ui"
        Directory.CreateDirectory(output)
        Using bitmap As New Bitmap(dialog.Width, dialog.Height)
            dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
            bitmap.Save(Path.Combine(output, name & "-fixture.png"))
        End Using
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim config As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        For Each action In {BookingChangeAction.Extend, BookingChangeAction.Cancel, BookingChangeAction.Expire}
            For Each retry In {False, True}
                Using server As New FakeSupabaseServer With {.StaffRole = "front_office", .ChangeState = If(action = BookingChangeAction.Extend, "active", "reserved"), .ChangeDeadlinePassed = action = BookingChangeAction.Expire}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                    Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                    Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                    Dim task = repo.GetBookingChangeDetailsAsync(1, CancellationToken.None) : Wait(task)
                    Using dialog As New BookingChangeDialog(repo, task.Result, action)
                        dialog.Show() : Application.DoEvents()
                        Check(Not Field(Of Button)(dialog, "saveButton").Enabled, "Change requires confirmation: " & action.ToString())
                        If action = BookingChangeAction.Cancel Then
                            Save(dialog)
                            Check(server.ChangeCancelCount = 0 AndAlso Field(Of Label)(dialog, "message").Text.Contains("Alasan"), "Cancellation cannot save empty reason")
                        End If
                        Prepare(dialog, action)
                        If action = BookingChangeAction.Extend Then
                            Check(Field(Of Label)(dialog, "summary").Text.Contains("100.000") AndAlso Not Field(Of CheckBox)(dialog, "confirmed").Checked, "Server extension quote shown before receiving money")
                            Field(Of DateTimePicker)(dialog, "due").Value = task.Result.DueAt.ToOffset(TimeSpan.FromHours(7)).DateTime.AddHours(23)
                            Check(Not Field(Of CheckBox)(dialog, "confirmed").Enabled, "Changed due date invalidates extension confirmation")
                            Preview(dialog)
                        Else
                            Check(Field(Of Label)(dialog, "summary").Text.Contains("DP"), "Cancellation or expiry displays retained DP: " & action.ToString())
                        End If
                        If Not retry Then
                            Screenshot(dialog, action.ToString())
                            dialog.Size = dialog.MinimumSize : Application.DoEvents() : Screenshot(dialog, action.ToString() & "-minimum")
                        End If
                        server.DropChangeReplyOnce = retry
                        Save(dialog)
                        If retry Then
                            Check(Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Lost reply freezes mutation payload: " & action.ToString())
                            Field(Of TextBox)(dialog, "reasonBox").Text = "Changed after uncertain response"
                            Save(dialog)
                            Check(server.ChangeRequestIds.Count = 2 AndAlso server.ChangeRequestIds.Distinct().Count() = 1, "Same request UUID reused: " & action.ToString())
                        End If
                        Check(dialog.Receipt IsNot Nothing, "Verified lifecycle receipt returned: " & action.ToString())
                        Check(If(action = BookingChangeAction.Extend, server.ChangePaymentCount = 1 AndAlso server.ChangePaid = 100000D, If(action = BookingChangeAction.Cancel, server.ChangeCancelCount = 1, server.ChangeExpireCount = 1)), "Financial/event effect occurs once: " & action.ToString())
                    End Using
                End Using
            Next
        Next
        For Each code In {"RG_CHANGE_STOCK", "RG_CHANGE_QUOTE"}
            Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim task = repo.GetBookingChangeDetailsAsync(1, CancellationToken.None) : Wait(task)
                Using dialog As New BookingChangeDialog(repo, task.Result, BookingChangeAction.Extend)
                    dialog.Show() : Preview(dialog) : server.RejectChangeCode = code : Save(dialog)
                    Check(dialog.Receipt Is Nothing AndAlso server.ChangePaymentCount = 0 AndAlso Not Field(Of CheckBox)(dialog, "confirmed").Checked, "Changed stock or quote cannot record extra rent: " & code)
                    dialog.Close()
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer With {.ChangeDue = DateTimeOffset.Parse("2027-01-01T12:00:00+07:00")}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Dim task = repo.GetBookingChangeDetailsAsync(1, CancellationToken.None) : Wait(task)
            Using dialog As New BookingChangeDialog(repo, task.Result, BookingChangeAction.Extend)
                dialog.Show()
                Field(Of DateTimePicker)(dialog, "due").Value = New DateTime(2027, 1, 1, 13, 0, 0)
                Preview(dialog)
                Check(Field(Of CheckBox)(dialog, "confirmed").Text.Contains("tanpa tambahan"), "Zero additional fee explicitly confirmed")
                Save(dialog)
                Check(dialog.Receipt IsNot Nothing AndAlso server.ChangePaid = 0 AndAlso server.ChangePaymentCount = 0, "Same billed day extension creates no cash entry")
            End Using
        End Using
        For Each mode In {"early_expiry", "late_extend"}
            Using server As New FakeSupabaseServer With {.ChangeState = If(mode = "early_expiry", "reserved", "active"), .ChangeLate = mode = "late_extend"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim task = repo.GetBookingChangeDetailsAsync(1, CancellationToken.None) : Wait(task)
                Try
                    Using dialog As New BookingChangeDialog(repo, task.Result, If(mode = "early_expiry", BookingChangeAction.Expire, BookingChangeAction.Extend))
                        Throw New Exception("Invalid timing should be blocked")
                    End Using
                Catch ex As InvalidOperationException
                    Check(True, "Server timing prevents invalid lifecycle action: " & mode)
                End Try
            End Using
        Next
        Using server As New FakeSupabaseServer With {.StaffRole = "warehouse_qc"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Try
                Wait(repo.GetBookingChangeDetailsAsync(1, CancellationToken.None))
                Throw New Exception("QC should be denied")
            Catch ex As UnauthorizedAccessException
                Check(True, "QC cannot load customer lifecycle details")
            End Try
        End Using
        For Each action In {BookingChangeAction.Extend, BookingChangeAction.Cancel, BookingChangeAction.Expire}
            Using server As New FakeSupabaseServer With {.ChangeState = If(action = BookingChangeAction.Extend, "active", "reserved"), .ChangeDeadlinePassed = action = BookingChangeAction.Expire}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Using host As New Form With {.Size = New Size(1200, 800)}, page As New OnlineBookingPage(repo)
                    host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                    Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                        AddHandler auto.Tick, Sub()
                                                  Dim dialog = Application.OpenForms.OfType(Of BookingChangeDialog)().FirstOrDefault()
                                                  If dialog Is Nothing Then Return
                                                  auto.Stop() : Prepare(dialog, action) : Save(dialog)
                                              End Sub
                        auto.Start()
                        Field(Of List(Of Button))(page, "changeButtons").Single(Function(button) DirectCast(button.Tag, BookingChangeAction) = action).PerformClick()
                        Wait(page.PendingLoad)
                    End Using
                    Check(Field(Of Label)(page, "message").Text.Contains(BookingChangeDialog.Caption(action)) AndAlso server.ChangeRequestIds.Count = 1, "Booking toolbar opens correct action and refreshes list: " & action.ToString())
                    host.Close() : Wait(page.PendingLoad)
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Dim profile = repo.GetCurrentUserAsync(CancellationToken.None) : Wait(profile)
            Using main As New MainForm(profile.Result, repo)
                main.WindowState = FormWindowState.Normal : main.Size = New Size(1100, 700)
                main.Show() : Application.DoEvents()
                main.WindowState = FormWindowState.Normal : main.Size = main.MinimumSize : Application.DoEvents()
                Field(Of List(Of Button))(main, "menuButtons").Single(Function(button) CStr(button.Tag) = "booking").PerformClick()
                Dim page = DirectCast(Field(Of Panel)(main, "contentPanel").Controls(0), OnlineBookingPage)
                Wait(page.PendingLoad)
                Check(Field(Of List(Of Button))(page, "changeButtons").All(Function(button) button.Parent.ClientRectangle.Contains(button.Bounds)), "All lifecycle toolbar buttons fit minimum main window")
                Screenshot(main, "booking-minimum")
                main.Close() : Wait(main.WaitForPendingReadsAsync())
            End Using
        End Using
        Return passed
    End Function
End Module
