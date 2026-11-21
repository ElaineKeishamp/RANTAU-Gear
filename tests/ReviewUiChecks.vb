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
Module ReviewUiChecks
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
            If watch.Elapsed.TotalSeconds > 15 Then Throw New TimeoutException("Review UI timeout")
        End While
        task.GetAwaiter().GetResult() : Application.DoEvents()
    End Sub
    Private Sub Screenshot(form As Form, name As String)
        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-review-ui"
        Directory.CreateDirectory(output)
        Using bitmap As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, form.Size))
            bitmap.Save(Path.Combine(output, name & "-fixture.png"))
        End Using
    End Sub
    Private Sub Fill(dialog As StaffEditorDialog, operation As String)
        Field(Of TextBox)(dialog, "nameBox").Text = "Staff uji baru"
        If operation = "create" Then Field(Of TextBox)(dialog, "emailBox").Text = "staff@fixture.test"
        If operation <> "update" Then
            Field(Of TextBox)(dialog, "passwordBox").Text = "TEST_ONLY_PASSWORD"
            Field(Of TextBox)(dialog, "confirmBox").Text = "TEST_ONLY_PASSWORD"
        End If
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim config As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using host As New Form With {.Size = New Size(1250, 900)}, page As New OnlineReportsPage(repo)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                Dim grid = Field(Of DataGridView)(page, "grid")
                Check(grid.Rows.Count = 25, "Cash report is server paginated")
                Check(Field(Of Label)(page, "summary").Text.Contains("135.000") AndAlso Field(Of Label)(page, "summary").Text.Contains("nonkas"), "Cash summary separates net cash and noncash deposit deductions")
                Check(CDate(grid.Rows(0).Cells("Waktu (WIB)").Value).Hour = 7, "Cash timestamps rendered in WIB")
                Field(Of Button)(page, "following").PerformClick() : Wait(page.PendingLoad)
                Check(Field(Of Label)(page, "status").Text.Contains("26 sampai 50"), "Report next page uses server offset")
                Field(Of Button)(page, "previous").PerformClick() : Wait(page.PendingLoad)
                Check(Field(Of Label)(page, "status").Text.Contains("1 sampai 25"), "Report previous page returns to first server page")
                Field(Of Button)(page, "exportButton").PerformClick() : Wait(page.PendingLoad)
                Check(File.Exists(page.LastExportPath), "Full filtered report CSV created")
                Dim csv = File.ReadAllText(page.LastExportPath)
                Check(csv.Contains("UJI-60") AndAlso csv.Contains("'=HYPERLINK"), "CSV includes all pages and neutralizes spreadsheet formulas")
                Check(File.ReadAllBytes(page.LastExportPath).Take(3).SequenceEqual(New Byte() {&HEF, &HBB, &HBF}), "CSV uses UTF-8 BOM for Excel")
                Field(Of DateTimePicker)(page, "fromDate").Value = Field(Of DateTimePicker)(page, "toDate").Value.AddDays(1)
                Check(Not Field(Of Button)(page, "exportButton").Enabled, "Changed filters cannot export stale report")
                Field(Of Button)(page, "loadButton").PerformClick() : Wait(page.PendingLoad)
                Check(grid.Rows.Count = 25 AndAlso Not Field(Of Button)(page, "exportButton").Enabled, "Invalid period preserves old table with export disabled")
                Field(Of DateTimePicker)(page, "fromDate").Value = Field(Of DateTimePicker)(page, "toDate").Value.Date.AddDays(-1)
                Field(Of ComboBox)(page, "kindBox").SelectedIndex = 1
                Field(Of ComboBox)(page, "statusBox").SelectedIndex = 5
                Field(Of Button)(page, "loadButton").PerformClick() : Wait(page.PendingLoad)
                Check(grid.Columns.Contains("Pelanggan") AndAlso Field(Of Label)(page, "summary").Text.Contains("jadwal ambil"), "Rental report uses pickup period and customer history")
                Screenshot(host, "rental-report")
                Field(Of ComboBox)(page, "kindBox").SelectedIndex = 0
                Field(Of Button)(page, "loadButton").PerformClick() : Wait(page.PendingLoad)
                Screenshot(host, "cash-report")
                server.DelayReads = True
                Field(Of Button)(page, "loadButton").PerformClick() : Application.DoEvents()
                Field(Of Button)(page, "cancel").PerformClick() : Wait(page.PendingLoad)
                Check(grid.Rows.Count > 0, "Cancelled report request preserves previous data")
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        For Each operation In {"create", "update", "password"}
            For Each retry In {False, True}
                Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                    Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                    Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                    Dim task = repo.ReadStaffAsync(0, 25, CancellationToken.None) : Wait(task)
                    Dim record = If(operation = "create", New StaffRecord(), task.Result.Rows(0))
                    Using dialog As New StaffEditorDialog(repo, record, operation, FakeSupabaseServer.StaffId)
                        dialog.Show() : Application.DoEvents()
                        If operation = "create" Then
                            Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                            Check(server.StaffWriteCount = 0, "Incomplete staff input cannot create Auth user")
                        End If
                        Fill(dialog, operation)
                        If operation <> "update" Then
                            Check(Field(Of TextBox)(dialog, "passwordBox").UseSystemPasswordChar, "Staff password hidden by default: " & operation)
                            Field(Of Button)(dialog, "showButton").PerformClick()
                            Check(Not Field(Of TextBox)(dialog, "passwordBox").UseSystemPasswordChar, "Show password button works: " & operation)
                            Field(Of Button)(dialog, "showButton").PerformClick()
                        Else
                            Check(Not Field(Of ComboBox)(dialog, "roleBox").Enabled AndAlso Not Field(Of CheckBox)(dialog, "activeBox").Enabled, "Self edit cannot disable or demote admin in UI")
                        End If
                        If Not retry Then
                            Screenshot(dialog, "staff-" & operation)
                            dialog.Size = dialog.MinimumSize : Application.DoEvents() : Screenshot(dialog, "staff-" & operation & "-minimum")
                        End If
                        server.DropStaffReplyOnce = retry
                        Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                        If retry Then
                            Check(Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Unknown staff result freezes payload: " & operation)
                            Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                            Check(server.StaffWriteIds.Count = 2 AndAlso server.StaffWriteIds.Distinct().Count() = 1, "Staff retry reuses same UUID: " & operation)
                        End If
                        Check(dialog.SavedId.HasValue AndAlso server.StaffWriteCount = 1, "Staff mutation saved once: " & operation)
                        If operation = "password" Then Check(dialog.SessionExpired, "Changing own password asks for new login")
                    End Using
                End Using
            Next
        Next
        Using server As New FakeSupabaseServer With {.RejectStaffVersion = True}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Dim task = repo.ReadStaffAsync(0, 25, CancellationToken.None) : Wait(task)
            Using dialog As New StaffEditorDialog(repo, task.Result.Rows(0), "update", FakeSupabaseServer.StaffId)
                dialog.Show() : Fill(dialog, "update")
                Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                Check(Not dialog.SavedId.HasValue AndAlso Not Field(Of Button)(dialog, "saveButton").Enabled, "Stale staff edit requires reload")
                dialog.Close()
            End Using
        End Using
        For Each role In {"front_office", "warehouse_qc"}
            Using server As New FakeSupabaseServer With {.StaffRole = role}, client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Try
                    Wait(repo.ReadStaffAsync(0, 25, CancellationToken.None))
                    Throw New Exception("Staff list should be denied")
                Catch ex As UnauthorizedAccessException
                    Check(True, "Non-admin staff management blocked: " & role)
                End Try
                If role = "warehouse_qc" Then
                    Try
                        Wait(repo.ReadReportAsync(New ReportQuery With {.FromDate = DateTime.Today, .ToDate = DateTime.Today}, 0, 25, CancellationToken.None))
                        Throw New Exception("QC reports should be denied")
                    Catch ex As UnauthorizedAccessException
                        Check(True, "QC cash report blocked at repository")
                    End Try
                End If
            End Using
        Next
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(config, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using host As New Form With {.Size = New Size(1200, 850)}, page As New StaffManagementPage(repo, FakeSupabaseServer.StaffId)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                    AddHandler auto.Tick, Sub()
                                              Dim dialog = Application.OpenForms.OfType(Of StaffEditorDialog)().FirstOrDefault()
                                              If dialog Is Nothing Then Return
                                              auto.Stop() : Fill(dialog, "create")
                                              Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                                          End Sub
                    auto.Start() : Field(Of Button)(page, "addButton").PerformClick() : Wait(page.PendingLoad)
                End Using
                Check(Field(Of DataGridView)(page, "grid").Rows.Count = 2, "Add staff toolbar opens editor and refreshes account list")
                Dim grid = Field(Of DataGridView)(page, "grid")
                Dim target = DirectCast(grid.Rows(1).Cells("Id").Value, Guid)
                For Each operation In {"update", "password"}
                    Dim row = grid.Rows.Cast(Of DataGridViewRow)().Single(Function(item) DirectCast(item.Cells("Id").Value, Guid) = target)
                    grid.CurrentCell = row.Cells("Nama")
                    Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                        AddHandler auto.Tick, Sub()
                                                  Dim dialog = Application.OpenForms.OfType(Of StaffEditorDialog)().FirstOrDefault()
                                                  If dialog Is Nothing Then Return
                                                  auto.Stop() : Fill(dialog, operation)
                                                  If operation = "update" Then Field(Of TextBox)(dialog, "nameBox").Text = "Staff diedit dari toolbar"
                                                  Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                                              End Sub
                        auto.Start() : Field(Of Button)(page, If(operation = "update", "editButton", "passwordButton")).PerformClick() : Wait(page.PendingLoad)
                    End Using
                    Check(server.StaffWriteCount = If(operation = "update", 2, 3), "Staff toolbar targets selected bound account: " & operation)
                Next
                Field(Of Button)(page, "reload").PerformClick() : Wait(page.PendingLoad)
                Check(grid.Rows.Cast(Of DataGridViewRow)().Any(Function(item) CStr(item.Cells("Nama").Value) = "Staff diedit dari toolbar"), "Reload preserves staff changes from server")
                Check(Not Field(Of Button)(page, "following").Enabled, "Staff next page disabled when no more accounts")
                Screenshot(host, "staff-list")
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        Dim query As New ReportQuery With {.FromDate = DateTime.Today, .ToDate = DateTime.Today}
        Dim sample As New ReportSnapshot With {.Kind = "cash", .Total = 10001, .Rows = New List(Of System.Text.Json.JsonElement)(), .Summary = New Dictionary(Of String, Decimal)(), .SnapshotAt = DateTimeOffset.UtcNow}
        Try
            ReportFiles.ExportCsv(query, sample)
            Throw New Exception("Partial report export must be rejected")
        Catch ex As InvalidOperationException
            Check(True, "Over-limit export never silently exports only a partial report")
        End Try
        sample.Total = 0 : sample.Summary("cash_net") = -1000D
        Dim negativeCsv = File.ReadAllText(ReportFiles.ExportCsv(query, sample))
        Check(negativeCsv.Contains("-1000") AndAlso Not negativeCsv.Contains("'-1000"), "Negative net cash stays numeric while formula text is neutralized")
        Dim prior = AppContext.GetData("RantauGear.ExportDirectory")
        Try
            AppContext.SetData("RantauGear.ExportDirectory", "Laporan-portable-test")
            Dim portable = ReportFiles.ExportCsv(query, sample)
            Check(Path.GetDirectoryName(portable) = Path.Combine(AppContext.BaseDirectory, "Laporan-portable-test"), "Published export path resolves beside application")
        Finally
            AppContext.SetData("RantauGear.ExportDirectory", prior)
        End Try
        Return passed
    End Function
End Module
