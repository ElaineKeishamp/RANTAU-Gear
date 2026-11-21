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
Module BookingChecks
    Private passed As Integer
    Private Sub Check(ok As Boolean, label As String)
        If Not ok Then Throw New Exception("FAIL: " & label)
        passed += 1 : Console.WriteLine("PASS: " & label)
    End Sub
    Private Function Field(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.NonPublic Or BindingFlags.Instance).GetValue(target), T)
    End Function
    Private Sub Wait(task As Task)
        Dim clock = Stopwatch.StartNew()
        While Not task.IsCompleted
            Application.DoEvents() : Thread.Sleep(5)
            If clock.Elapsed.TotalSeconds > 12 Then Throw New TimeoutException("Booking test timeout")
        End While
        task.GetAwaiter().GetResult() : Application.DoEvents()
    End Sub
    Private Sub Fill(dialog As ReservationDialog)
        Field(Of ComboBox)(dialog, "customerBox").SelectedIndex = 0
        Field(Of DataGridView)(dialog, "items").Rows(0).Cells("Jumlah").Value = "1"
        Field(Of DateTimePicker)(dialog, "pickup").Value = New DateTime(2027, 1, 1, 10, 0, 0)
        Field(Of DateTimePicker)(dialog, "due").Value = New DateTime(2027, 1, 2, 10, 0, 0)
    End Sub
    Private Sub Preview(dialog As ReservationDialog)
        Field(Of Button)(dialog, "previewButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Private Sub Save(dialog As ReservationDialog)
        Field(Of CheckBox)(dialog, "confirmed").Checked = True
        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim config As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        For Each mode In {"normal", "lost", "stock", "price", "package"}
            Using server As New FakeSupabaseServer With {.StaffRole = "front_office"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim optionsTask = repo.GetReservationOptionsAsync(CancellationToken.None) : Wait(optionsTask)
                Check(optionsTask.Result.Customers.Count = 1 AndAlso optionsTask.Result.Types.Count = 1 AndAlso optionsTask.Result.Packages.Count = 1, "Front office loads typed reservation options: " & mode)
                Using dialog As New ReservationDialog(repo, optionsTask.Result)
                    dialog.Show() : Application.DoEvents()
                    Preview(dialog)
                    Check(server.ReservationCount = 0 AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Empty reservation cannot write DP: " & mode)
                    Fill(dialog)
                    If mode = "package" Then Field(Of ComboBox)(dialog, "packageBox").SelectedIndex = 1
                    Preview(dialog)
                    Check(Field(Of Label)(dialog, "summary").Text.Contains(If(mode = "package", "50.000", "100.000")) AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Server quote shown before cash confirmation: " & mode)
                    If mode = "normal" Then
                        Field(Of DateTimePicker)(dialog, "due").Value = New DateTime(2027, 1, 3, 10, 0, 0)
                        Check(Not Field(Of CheckBox)(dialog, "confirmed").Enabled AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Changing schedule invalidates quote and payment confirmation")
                        Preview(dialog)
                        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-booking-ui"
                        Directory.CreateDirectory(output)
                        Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                            dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
                            bitmap.Save(Path.Combine(output, "reservation-quote-fixture.png"))
                        End Using
                    End If
                    If mode = "package" Then Check(Field(Of DataGridView)(dialog, "items").ReadOnly, "Package components cannot be edited independently")
                    server.ObjectReservationReply = mode = "package"
                    server.DropReservationReplyOnce = mode = "lost"
                    server.RejectBookingStock = mode = "stock"
                    server.ChangeBookingPrice = mode = "price"
                    Save(dialog)
                    If mode = "lost" Then
                        Check(server.ReservationCount = 1 AndAlso Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Lost reply freezes confirmed payment payload")
                        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingOperation)
                        Check(server.ReservationIds.Count = 2 AndAlso server.ReservationIds.Distinct().Count() = 1 AndAlso server.DpCount = 1, "Explicit retry keeps same UUID and single DP")
                    End If
                    If mode = "stock" OrElse mode = "price" Then
                        Check(server.DpCount = 0 AndAlso Not Field(Of CheckBox)(dialog, "confirmed").Checked AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Rejected save requires fresh preview and confirms no DP: " & mode)
                        Check(Field(Of Label)(dialog, "message").Text.Contains(If(mode = "stock", "Stok", "Tarif")), "Controlled actionable booking error: " & mode)
                        dialog.Close()
                    Else
                        Check(dialog.Receipt IsNot Nothing AndAlso server.ReservationCount = 1 AndAlso server.DpCount = 1, "Reservation and DP receipt returned: " & mode)
                    End If
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer With {.StaffRole = "warehouse_qc"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Try
                Wait(repo.GetReservationOptionsAsync(CancellationToken.None))
                Throw New Exception("QC options should be denied")
            Catch ex As UnauthorizedAccessException
                Check(True, "QC cannot access customer reservation options")
            End Try
        End Using
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using host As New Form With {.Size = New Size(1100, 760)}, page As New OnlineBookingPage(repo)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                    AddHandler auto.Tick, Sub()
                                              Dim dialog = Application.OpenForms.OfType(Of ReservationDialog)().FirstOrDefault()
                                              If dialog Is Nothing Then Return
                                              auto.Stop() : Fill(dialog) : Preview(dialog) : Save(dialog)
                                          End Sub
                    auto.Start() : Field(Of Button)(page, "addButton").PerformClick() : Wait(page.PendingLoad)
                End Using
                Check(server.ReservationCount = 1 AndAlso server.DpCount = 1 AndAlso Field(Of Label)(page, "message").Text.Contains("UJI-RESERVASI"), "Booking toolbar opens modal, saves DP, shows receipt and refreshes list")
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        Return passed
    End Function
End Module
