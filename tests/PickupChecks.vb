Option Strict On
Option Explicit On
Option Infer On
Imports System.ComponentModel
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
Module PickupChecks
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
            If watch.Elapsed.TotalSeconds > 12 Then Throw New TimeoutException("Pickup test timeout")
        End While
        task.GetAwaiter().GetResult() : Application.DoEvents()
    End Sub
    Private Sub Confirm(dialog As PickupDialog)
        Field(Of CheckBox)(dialog, "moneyConfirmed").Checked = True
        Field(Of CheckBox)(dialog, "unitsConfirmed").Checked = True
    End Sub
    Private Sub Save(dialog As PickupDialog)
        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim config As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        For Each mode In {"normal", "lost", "unit", "window", "empty"}
            Using server As New FakeSupabaseServer With {.StaffRole = "front_office"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim task = repo.GetPickupDetailsAsync(1, CancellationToken.None) : Wait(task)
                Check(task.Result.RentBalance = 75000D AndAlso task.Result.DepositBalance = 100000D, "Typed pickup balances from server: " & mode)
                If mode = "empty" Then task.Result.Units.Clear()
                Using dialog As New PickupDialog(repo, task.Result)
                    dialog.Show() : Application.DoEvents()
                    Check(Not Field(Of Button)(dialog, "saveButton").Enabled, "Pickup requires actual money and unit confirmation: " & mode)
                    Confirm(dialog) : Save(dialog)
                    Check(server.PickedUpCount = 0, "No selected units cannot activate rental: " & mode)
                    If mode = "empty" Then
                        Check(Field(Of Label)(dialog, "message").Text.Length > 0, "Empty ready stock has readable state")
                        dialog.Close() : Continue For
                    End If
                    Dim grid = Field(Of DataGridView)(dialog, "unitsGrid")
                    grid.Rows(0).Cells("Pilih").Value = True
                    grid.Rows(1).Cells("Pilih").Value = True
                    Check(Not Field(Of CheckBox)(dialog, "moneyConfirmed").Checked, "Changing selected units clears prior money confirmation: " & mode)
                    Confirm(dialog) : Save(dialog)
                    Check(server.PickedUpCount = 0 AndAlso Field(Of Label)(dialog, "message").Text.Contains("Jumlah"), "Incorrect physical quantity rejected before write: " & mode)
                    grid.Rows(1).Cells("Pilih").Value = False
                    Field(Of ComboBox)(dialog, "methodBox").SelectedIndex = 1
                    Confirm(dialog)
                    If mode = "normal" Then
                        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-pickup-ui"
                        Directory.CreateDirectory(output)
                        Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                            dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
                            bitmap.Save(Path.Combine(output, "pickup-ready-fixture.png"))
                        End Using
                        dialog.Size = dialog.MinimumSize : Application.DoEvents()
                        Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                            dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
                            bitmap.Save(Path.Combine(output, "pickup-minimum-fixture.png"))
                        End Using
                    End If
                    server.DropPickupReplyOnce = mode = "lost"
                    server.RejectPickupUnit = mode = "unit"
                    server.PickupWindowClosed = mode = "window"
                    Save(dialog)
                    If mode = "lost" Then
                        Check(server.PickedUpCount = 1 AndAlso Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Lost pickup reply freezes unit IDs and payment fields")
                        Save(dialog)
                        Check(server.PickupIds.Count = 2 AndAlso server.PickupIds.Distinct().Count() = 1, "Pickup retry uses same request UUID")
                    End If
                    If mode = "unit" OrElse mode = "window" Then
                        Check(dialog.Receipt Is Nothing AndAlso server.PickupRentReceived = 0 AndAlso server.PickupDepositReceived = 0, "Rejected pickup receives no money: " & mode)
                        Check(Not Field(Of Button)(dialog, "saveButton").Enabled AndAlso Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Stale pickup requires closing and fresh detail load: " & mode)
                        dialog.Close()
                    Else
                        Check(dialog.Receipt IsNot Nothing AndAlso dialog.Receipt.State = "active", "Composite RPC pickup receipt activates rental: " & mode)
                        Check(server.PickedUpCount = 1 AndAlso server.PickupRentReceived = 75000D AndAlso server.PickupDepositReceived = 100000D, "Rent balance and deposit recorded once and separately: " & mode)
                    End If
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer With {.StaffRole = "warehouse_qc"}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Try
                Wait(repo.GetPickupDetailsAsync(1, CancellationToken.None))
                Throw New Exception("QC should be rejected")
            Catch ex As UnauthorizedAccessException
                Check(True, "QC cannot load customer pickup payment details")
            End Try
            Try
                Wait(repo.PickupReservationAsync(Guid.NewGuid(), 1, New List(Of Long) From {30}, "cash", "", CancellationToken.None))
                Throw New Exception("QC pickup should be rejected")
            Catch ex As UnauthorizedAccessException
                Check(True, "QC cannot activate rental or receive deposit")
            End Try
        End Using
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using host As New Form With {.Size = New Size(1200, 800)}, page As New OnlineBookingPage(repo)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                Dim listing = Field(Of OnlineDataPage)(page, "listing")
                Dim grid = Field(Of DataGridView)(listing, "grid")
                grid.Sort(grid.Columns("Nomor sewa"), ListSortDirection.Descending)
                grid.CurrentCell = grid.Rows(3).Cells("Nomor sewa")
                Dim selected = CLng(grid.Rows(3).Cells("RecordId").Value)
                Check(listing.SelectedRecordId.GetValueOrDefault() = selected AndAlso Not grid.Columns("RecordId").Visible, "Selected rental identity follows bound row after sorting")
                Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                    AddHandler auto.Tick, Sub()
                                              Dim dialog = Application.OpenForms.OfType(Of PickupDialog)().FirstOrDefault()
                                              If dialog Is Nothing Then Return
                                              auto.Stop()
                                              Field(Of DataGridView)(dialog, "unitsGrid").Rows(0).Cells("Pilih").Value = True
                                              Confirm(dialog) : Save(dialog)
                                          End Sub
                    auto.Start() : Field(Of Button)(page, "pickupButton").PerformClick() : Wait(page.PendingLoad)
                End Using
                Check(server.LastPickupRentalId = selected AndAlso server.PickedUpCount = 1, "Toolbar processes exact selected rental and refreshes Booking")
                Dim refreshed = grid.Rows.Cast(Of DataGridViewRow)().Single(Function(row) CLng(row.Cells("RecordId").Value) = selected)
                Check(CStr(refreshed.Cells("Status").Value) = "Disewa", "Booking shows active state after pickup")
                Check(CDec(refreshed.Cells("Sewa dibayar").Value) = 100000D AndAlso CDec(refreshed.Cells("Deposit diterima").Value) = 100000D, "Booking displays actual rent and deposit received after pickup")
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        Return passed
    End Function
End Module
