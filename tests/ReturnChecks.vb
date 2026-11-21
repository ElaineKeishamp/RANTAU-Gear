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
Module ReturnChecks
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
            If watch.Elapsed.TotalSeconds > 12 Then Throw New TimeoutException("Return UI timeout")
        End While
        task.GetAwaiter().GetResult() : Application.DoEvents()
    End Sub
    Private Sub Save(dialog As ReturnWorkflowDialog)
        Field(Of CheckBox)(dialog, "confirmed").Checked = True
        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingOperation)
    End Sub
    Private Sub FillQc(dialog As ReturnWorkflowDialog, scenario As String)
        Dim grid = Field(Of DataGridView)(dialog, "grid")
        grid.Rows(0).Cells("Kondisi").Value = "Baik"
        grid.Rows(1).Cells("Kondisi").Value = If(scenario = "refund", "Baik", If(scenario = "partial" OrElse scenario = "equal", "Rusak", "Hilang"))
        grid.Rows(1).Cells("Biaya").Value = If(scenario = "refund", "0", If(scenario = "partial", "40000", If(scenario = "equal", "75000", "150000")))
        grid.Rows(1).Cells("Catatan").Value = If(scenario = "refund", "Baik", "Catatan QC uji")
    End Sub
    Private Sub Screenshot(dialog As Form, name As String)
        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-return-ui"
        Directory.CreateDirectory(output)
        Using bitmap As New Bitmap(dialog.Width, dialog.Height)
            dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
            bitmap.Save(Path.Combine(output, name & "-fixture.png"))
        End Using
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim config As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        For Each scenario In {"lost", "refund", "partial", "equal", "retry"}
            Using server As New FakeSupabaseServer With {.ReturnLateCharge = If(scenario = "refund", 0D, 25000D)}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                For Each action In {ReturnAction.Inspect, ReturnAction.Approve, ReturnAction.Settle}
                    Dim task = repo.GetReturnDetailsAsync(1, CancellationToken.None) : Wait(task)
                    Using dialog As New ReturnWorkflowDialog(repo, task.Result, action)
                        dialog.Show() : Application.DoEvents()
                        Check(Not Field(Of Button)(dialog, "saveButton").Enabled, "Action requires explicit confirmation: " & scenario & "/" & action.ToString())
                        If action = ReturnAction.Inspect Then
                            Save(dialog)
                            Check(server.InspectionsCount = 0, "Missing unit conditions cannot submit QC: " & scenario)
                            Dim grid = Field(Of DataGridView)(dialog, "grid")
                            grid.Rows(0).Cells("Kondisi").Value = "Baik"
                            grid.Rows(1).Cells("Kondisi").Value = "Hilang"
                            Save(dialog)
                            Check(server.InspectionsCount = 0, "Lost unit requires positive charge and notes: " & scenario)
                            FillQc(dialog, scenario)
                            Check(Not Field(Of CheckBox)(dialog, "confirmed").Checked, "Changing inspection invalidates old confirmation: " & scenario)
                        Else
                            Check(Field(Of DataGridView)(dialog, "grid").ReadOnly, "Approved inspection inputs cannot be altered in later stage: " & action.ToString())
                        End If
                        If action = ReturnAction.Settle Then
                            Dim text = Field(Of CheckBox)(dialog, "confirmed").Text
                            Check(text.Contains(If(scenario = "lost" OrElse scenario = "retry", "tambahan", If(scenario = "equal", "tanpa perpindahan", "Refund"))), "Confirmation describes correct money direction: " & scenario)
                            Field(Of ComboBox)(dialog, "methodBox").SelectedIndex = 1
                        End If
                        If scenario = "lost" Then
                            Screenshot(dialog, action.ToString())
                            dialog.Size = dialog.MinimumSize : Application.DoEvents() : Screenshot(dialog, action.ToString() & "-minimum")
                        End If
                        server.DropReturnReplyOnce = scenario = "retry"
                        Save(dialog)
                        If scenario = "retry" Then
                            Check(Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Lost reply freezes stage payload: " & action.ToString())
                            Save(dialog)
                            Check(server.ReturnRequestIds.TakeLast(2).Distinct().Count() = 1, "Retry UUID preserved: " & action.ToString())
                        End If
                        Check(dialog.Receipt IsNot Nothing, "Stage returns verified receipt: " & scenario & "/" & action.ToString())
                    End Using
                Next
                Check(server.InspectionsCount = 1 AndAlso server.ApprovalsCount = 1 AndAlso server.SettlementsCount = 1, "Three stages execute once each: " & scenario)
                Dim expectedRefund = If(scenario = "refund", 100000D, If(scenario = "partial", 35000D, 0D))
                Dim expectedAdditional = If(scenario = "lost" OrElse scenario = "retry", 75000D, 0D)
                Check(server.RefundPaid = expectedRefund AndAlso server.AdditionalReceived = expectedAdditional, "Refund or shortage matches approved total: " & scenario)
            End Using
        Next
        For Each role In {"front_office", "warehouse_qc"}
            Using server As New FakeSupabaseServer With {.StaffRole = role}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim task = repo.GetReturnDetailsAsync(1, CancellationToken.None) : Wait(task)
                Check((task.Result.Financial Is Nothing) = (role = "warehouse_qc"), "QC cash privacy and front-office financial view: " & role)
                Try
                    Wait(repo.ProcessReturnAsync(Guid.NewGuid(), 1, ReturnAction.Approve, New List(Of ReturnCheck)(), "cash", "", CancellationToken.None))
                    Throw New Exception("Non-admin approval should be denied")
                Catch ex As UnauthorizedAccessException
                    Check(True, "Approval restricted to admin: " & role)
                End Try
                If role = "warehouse_qc" Then
                    Try
                        Wait(repo.ProcessReturnAsync(Guid.NewGuid(), 1, ReturnAction.Settle, New List(Of ReturnCheck)(), "cash", "", CancellationToken.None))
                        Throw New Exception("QC settlement should be denied")
                    Catch ex As UnauthorizedAccessException
                        Check(True, "QC cannot refund or receive shortage")
                    End Try
                End If
            End Using
        Next
        For Each action In {ReturnAction.Inspect, ReturnAction.Approve, ReturnAction.Settle}
            Using server As New FakeSupabaseServer With {.ReturnState = If(action = ReturnAction.Inspect, "active", "returned"), .ReturnApproved = action = ReturnAction.Settle}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim detailsTask = repo.GetReturnDetailsAsync(1, CancellationToken.None) : Wait(detailsTask)
                Using dialog As New ReturnWorkflowDialog(repo, detailsTask.Result, action)
                    dialog.Show() : Application.DoEvents()
                    If action = ReturnAction.Inspect Then FillQc(dialog, "lost")
                    server.RejectReturnAction = True : Save(dialog)
                    Check(dialog.Receipt Is Nothing AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Changed phase rejects save and requires fresh details: " & action.ToString())
                    dialog.Close()
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Dim profile = repo.GetCurrentUserAsync(CancellationToken.None) : Wait(profile)
            Using host As New Form With {.Size = New Size(1200, 800)}, page As New OnlineReturnPage(repo, profile.Result)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                For Each action In {ReturnAction.Inspect, ReturnAction.Approve, ReturnAction.Settle}
                    Dim button = Field(Of List(Of Button))(page, "buttons").Single(Function(x) DirectCast(x.Tag, ReturnAction) = action)
                    Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                        AddHandler auto.Tick, Sub()
                                                  Dim dialog = Application.OpenForms.OfType(Of ReturnWorkflowDialog)().FirstOrDefault()
                                                  If dialog Is Nothing Then Return
                                                  auto.Stop()
                                                  If action = ReturnAction.Inspect Then FillQc(dialog, "lost")
                                                  Save(dialog)
                                              End Sub
                        auto.Start() : button.PerformClick() : Wait(page.PendingLoad)
                    End Using
                Next
                Check(server.ReturnSettled AndAlso server.AdditionalReceived = 75000D, "Toolbar completes QC, approval and shortage payment end to end")
                Dim grid = Field(Of DataGridView)(Field(Of OnlineDataPage)(page, "listing"), "grid")
                Check(CStr(grid.Rows(0).Cells("Tahap berikutnya").Value) = "Selesai", "Return register shows completed phase after settlement")
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        For Each incomplete In {True, False}
            Using server As New FakeSupabaseServer With {.ReturnFinancialIncomplete = incomplete, .ReturnFinancialUnbalanced = Not incomplete}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Try
                    Wait(repo.GetReturnDetailsAsync(1, CancellationToken.None))
                    Throw New Exception("Invalid money summary should be rejected")
                Catch ex As Exception When TypeOf ex Is IO.InvalidDataException OrElse TypeOf ex Is System.Text.Json.JsonException
                    Check(server.SettlementsCount = 0, "Incomplete or unbalanced money summary rejected before confirmation: " & incomplete.ToString())
                End Try
            End Using
        Next
        Return passed
    End Function
End Module
