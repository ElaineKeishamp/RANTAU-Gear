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

Module MasterDataChecks
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
        Dim timer = Stopwatch.StartNew()
        While Not task.IsCompleted
            Application.DoEvents()
            If timer.Elapsed.TotalSeconds > 12 Then Throw New TimeoutException("Master UI did not finish")
            Thread.Sleep(5)
        End While
        task.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub
    Private Iterator Function AllControls(parent As Control) As IEnumerable(Of Control)
        For Each item As Control In parent.Controls
            Yield item
            For Each child In AllControls(item)
                Yield child
            Next
        Next
    End Function
    Private Function Choices(repo As IRentalRepository, kind As MasterDataKind) As List(Of MasterRecord)
        If kind = MasterDataKind.EquipmentType Then
            Dim task = repo.ReadMasterAsync(MasterDataKind.Category, 0, 100, CancellationToken.None) : Wait(task) : Return task.Result.Records
        End If
        If kind = MasterDataKind.EquipmentUnit OrElse kind = MasterDataKind.Package Then
            Dim task = repo.ReadMasterAsync(MasterDataKind.EquipmentType, 0, 100, CancellationToken.None) : Wait(task) : Return task.Result.Records
        End If
        Return New List(Of MasterRecord)()
    End Function
    Private Sub Fill(dialog As MasterEditDialog, kind As MasterDataKind, name As String)
        Field(Of TextBox)(dialog, "nameBox").Text = name
        Select Case kind
            Case MasterDataKind.EquipmentType
                Field(Of ComboBox)(dialog, "parentBox").SelectedIndex = 0
                Field(Of NumericUpDown)(dialog, "priceBox").Value = 100000
                Field(Of NumericUpDown)(dialog, "replacementBox").Value = 1000000
            Case MasterDataKind.EquipmentUnit
                Field(Of ComboBox)(dialog, "parentBox").SelectedIndex = 0
                Field(Of TextBox)(dialog, "codeBox").Text = name
                Field(Of CheckBox)(dialog, "qcBox").Checked = True
            Case MasterDataKind.Package
                Field(Of NumericUpDown)(dialog, "priceBox").Value = 50000
                Field(Of DataGridView)(dialog, "componentsGrid").Rows(0).Cells("Quantity").Value = "2"
            Case MasterDataKind.Customer
                Field(Of TextBox)(dialog, "phoneBox").Text = "080000123"
                Field(Of TextBox)(dialog, "notesBox").Text = "Alamat fixture"
        End Select
    End Sub
    Public Function Run() As Integer
        passed = 0
        Dim output = "D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-master-ui"
        Directory.CreateDirectory(output)
        Dim options As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        For Each kind As MasterDataKind In [Enum].GetValues(Of MasterDataKind)()
            Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim initial = repo.ReadMasterAsync(kind, 0, 25, CancellationToken.None) : Wait(initial)
                Check(initial.Result.Total = 1 AndAlso initial.Result.Records(0).RowVersion = 1, "Reads typed master and version: " & kind.ToString())
                Using dialog As New MasterEditDialog(repo, New MasterRecord With {.Kind = kind}, Choices(repo, kind))
                    dialog.Show() : Application.DoEvents()
                    Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                    Check(Not dialog.SavedId.HasValue AndAlso server.SaveCount = 0, "Invalid form does not send master mutation: " & kind.ToString())
                    Fill(dialog, kind, "MASTER-UI-" & kind.ToString())
                    Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                        dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, dialog.Size))
                        bitmap.Save(Path.Combine(output, kind.ToString() & "-fixture.png"), Imaging.ImageFormat.Png)
                    End Using
                    Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                    Check(dialog.SavedId.HasValue AndAlso server.MasterCount(SupabaseRentalRepository.MasterResource(kind)) = 2, "Form saves online master: " & kind.ToString())
                End Using
            End Using
        Next
        Using server As New FakeSupabaseServer With {.DropSaveReplyOnce = True}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using dialog As New MasterEditDialog(repo, New MasterRecord With {.Kind = MasterDataKind.Customer}, New List(Of MasterRecord)())
                dialog.Show() : Fill(dialog, MasterDataKind.Customer, "Customer retry")
                Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                Check(server.SaveCount = 1 AndAlso Not Field(Of TableLayoutPanel)(dialog, "fields").Enabled, "Lost save reply locks fields without automatically resubmitting")
                Field(Of TextBox)(dialog, "nameBox").Text = "Must not replace frozen payload"
                Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                Check(dialog.SavedId.HasValue AndAlso server.SaveRequestIds.Distinct().Count() = 1 AndAlso server.MasterCount("customers") = 2, "Explicit retry reuses identical request without duplicating customer")
            End Using
        End Using
        Using server As New FakeSupabaseServer With {.ConflictNextSave = True}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Dim page = repo.ReadMasterAsync(MasterDataKind.Category, 0, 25, CancellationToken.None) : Wait(page)
            Using dialog As New MasterEditDialog(repo, page.Result.Records(0), New List(Of MasterRecord)())
                dialog.Show() : Field(Of TextBox)(dialog, "nameBox").Text = "Stale edit"
                Field(Of Button)(dialog, "saveButton").PerformClick() : Wait(dialog.PendingSave)
                Check(Not Field(Of Button)(dialog, "saveButton").Enabled AndAlso Field(Of Label)(dialog, "message").Text.Contains("muat ulang"), "Stale edit requires reloading instead of overwriting another operator")
                dialog.Close()
            End Using
        End Using
        For Each role In {"front_office", "warehouse_qc"}
            Using server As New FakeSupabaseServer With {.StaffRole = role}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
                Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
                Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
                Dim customer As New MasterRecord With {.Kind = MasterDataKind.Customer, .Name = "Front customer", .Phone = "0800"}
                If role = "front_office" Then
                    Dim save = repo.SaveMasterAsync(Guid.NewGuid(), customer, CancellationToken.None) : Wait(save)
                    Check(save.Result > 0, "Front office can save customer master")
                Else
                    Try
                        Wait(repo.SaveMasterAsync(Guid.NewGuid(), customer, CancellationToken.None))
                        Throw New Exception("QC master write accepted")
                    Catch ex As UnauthorizedAccessException
                        Check(server.SaveCount = 0, "QC cannot send customer master mutation")
                    End Try
                End If
                Try
                    Wait(repo.SaveMasterAsync(Guid.NewGuid(), New MasterRecord With {.Kind = MasterDataKind.Category, .Name = "Forbidden"}, CancellationToken.None))
                    Throw New Exception("Non-admin inventory write accepted")
                Catch ex As UnauthorizedAccessException
                    Check(True, "Non-admin inventory edit blocked: " & role)
                End Try
            End Using
        Next
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(options, client), session As New SupabaseSessionManager(gateway)
            Wait(session.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None))
            Dim repo As IRentalRepository = New SupabaseRentalRepository(gateway, session)
            Using host As New Form With {.Size = New Size(1100, 760)}, page As New MasterDataPage(repo)
                host.Controls.Add(page) : host.Show() : Application.DoEvents() : Wait(page.PendingLoad)
                Dim tabs = AllControls(page).OfType(Of TabControl)().Single()
                For index = 0 To tabs.TabPages.Count - 1
                    tabs.SelectedIndex = index : Application.DoEvents() : Wait(page.PendingLoad)
                    Check(AllControls(tabs.SelectedTab).OfType(Of DataGridView)().Single().Rows.Count = 1, "Catalog tab loads actual master list " & index.ToString())
                Next
                tabs.SelectedIndex = 0 : Application.DoEvents()
                Dim list = AllControls(tabs.SelectedTab).OfType(Of MasterListPage)().Single()
                Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                    AddHandler auto.Tick, Sub()
                                              Dim dialog = Application.OpenForms.OfType(Of MasterEditDialog)().FirstOrDefault()
                                              If dialog Is Nothing Then Return
                                              auto.Stop()
                                              Fill(dialog, MasterDataKind.Category, "Category from toolbar")
                                              Field(Of Button)(dialog, "saveButton").PerformClick()
                                          End Sub
                    auto.Start()
                    Field(Of Button)(list, "addButton").PerformClick()
                    Wait(list.PendingLoad)
                End Using
                Check(Field(Of DataGridView)(list, "grid").Rows.Count = 2, "Toolbar opens editor and refreshes list after save")
                Using auto As New System.Windows.Forms.Timer With {.Interval = 30}
                    AddHandler auto.Tick, Sub()
                                              Dim dialog = Application.OpenForms.OfType(Of MasterEditDialog)().FirstOrDefault()
                                              If dialog Is Nothing Then Return
                                              auto.Stop()
                                              Field(Of TextBox)(dialog, "nameBox").Text = "Category edited from toolbar"
                                              Field(Of Button)(dialog, "saveButton").PerformClick()
                                          End Sub
                    auto.Start()
                    Field(Of Button)(list, "editButton").PerformClick()
                    Wait(list.PendingLoad)
                End Using
                Check(Field(Of DataGridView)(list, "grid").Rows.Cast(Of DataGridViewRow)().Any(Function(row) CStr(row.Cells("Nama").Value) = "Category edited from toolbar"), "Toolbar edit persists and reloads current selected identity")
                Using bitmap As New Bitmap(host.Width, host.Height)
                    host.DrawToBitmap(bitmap, New Rectangle(Point.Empty, host.Size))
                    bitmap.Save(Path.Combine(output, "catalog-tabs-fixture.png"), Imaging.ImageFormat.Png)
                End Using
                host.Close() : Wait(page.PendingLoad)
            End Using
        End Using
        Return passed
    End Function
End Module
