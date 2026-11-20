Option Strict On
Option Explicit On
Option Infer On

Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Data.Supabase
Imports RantauGear.Models

Module OnlineRepositoryChecks
    Private NotInheritable Class TestClock
        Inherits TimeProvider
        Public Property Current As DateTimeOffset = DateTimeOffset.UtcNow
        Public Overrides Function GetUtcNow() As DateTimeOffset
            Return Current
        End Function
    End Class

    Public Async Function RunAsync() As Task(Of Integer)
        Dim passed As Integer
        Dim check As Action(Of Boolean, String) = Sub(ok, label)
                                                     If Not ok Then Throw New Exception("FAIL: " & label)
                                                     passed += 1
                                                     Console.WriteLine("PASS: " & label)
                                                 End Sub
        Dim options As New SupabaseOptions("https://fixture.supabase.co", "sb_publishable_TEST_ONLY")
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(options, client)
            Dim clock As New TestClock()
            Using sessions As New SupabaseSessionManager(gateway, clock)
                Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
                check(sessions.UserId = FakeSupabaseServer.StaffId, "Session identity comes from Auth user endpoint")
                Dim repo As New SupabaseRentalRepository(gateway, sessions)
                Dim user = Await repo.GetCurrentUserAsync(CancellationToken.None)
                check(user.Role = UserRole.SuperAdmin AndAlso user.AuthUserId = FakeSupabaseServer.StaffId, "Repository maps server UUID and staff role")
                check(server.LastReadPath.Contains("id=eq." & FakeSupabaseServer.StaffId.ToString("D")), "Profile read filters by verified Auth identity")
                Dim page = Await repo.ReadPageAsync("dashboard", 25, 25, CancellationToken.None)
                check(page.TotalRows = 61 AndAlso page.Rows.Rows.Count = 25 AndAlso CStr(page.Rows.Rows(0)("Nomor sewa")) = "UJI-026", "Repository uses server pagination and exact total beyond first page")
                check(CDate(page.Rows.Rows(0)("Jadwal ambil")).Hour = 10, "UTC schedules display as WIB")
                check(CStr(page.Rows.Rows(0)("Status")) = "Dipesan", "Online reservation state is not shown as active checkout")
                page.Rows.Dispose()
                Dim catalog = Await repo.ReadPageAsync("catalog", 0, 25, CancellationToken.None)
                check(CStr(catalog.Rows.Rows(0)("Jenis alat")) = "reserved", "Names are preserved even when equal to a status code")
                check(CDec(catalog.Rows.Rows(0)("Tarif per hari")) = 100000D, "Online rates retain decimal values")
                catalog.Rows.Dispose()
                Try
                    repo.GetUnits()
                    Throw New Exception("Synchronous online access succeeded")
                Catch ex As NotSupportedException
                    check(True, "Online repository cannot fall back to synchronous network or demo data")
                End Try
                clock.Current = clock.Current.AddMinutes(61)
                Dim concurrent = Enumerable.Range(1, 8).Select(Function(index) sessions.GetAccessTokenAsync(CancellationToken.None)).ToArray()
                Dim tokens = Await Task.WhenAll(concurrent)
                check(server.RefreshCount = 1 AndAlso tokens.Distinct().Count() = 1, "Concurrent expired reads share one refresh and rotated token")
                server.RejectReadOnce = True
                user = Await repo.GetCurrentUserAsync(CancellationToken.None)
                check(server.RefreshCount = 2 AndAlso user.IsActive, "A rejected read refreshes once and retries safely")
                server.Active = False
                Try
                    Await repo.GetCurrentUserAsync(CancellationToken.None)
                    Throw New Exception("Inactive staff accepted")
                Catch ex As UnauthorizedAccessException
                    check(True, "Inactive staff cannot continue after login")
                End Try
                server.Active = True
                server.StaffRole = "front_office"
                Try
                    Await repo.GetCurrentUserAsync(CancellationToken.None)
                    Throw New Exception("Changed role silently accepted")
                Catch ex As UnauthorizedAccessException
                    check(True, "Role changes require a fresh login")
                End Try
                Await sessions.SignOutAsync(CancellationToken.None)
                check(server.LogoutCount = 1 AndAlso sessions.UserId = Guid.Empty, "Logout revokes server session and clears local identity")
                Try
                    Await sessions.GetAccessTokenAsync(CancellationToken.None)
                    Throw New Exception("Logged-out token reused")
                Catch ex As UnauthorizedAccessException
                    check(True, "Logged-out tokens cannot be reused")
                End Try
            End Using
        End Using
        For Each role In {"front_office", "warehouse_qc", "unknown"}
            Using server As New FakeSupabaseServer With {.StaffRole = role}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), sessions As New SupabaseSessionManager(gateway)
                Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
                Dim repo As New SupabaseRentalRepository(gateway, sessions)
                Try
                    Dim user = Await repo.GetCurrentUserAsync(CancellationToken.None)
                    check((role = "front_office" AndAlso user.Role = UserRole.FrontOffice) OrElse (role = "warehouse_qc" AndAlso user.Role = UserRole.GudangQc), "Server role maps correctly: " & role)
                    Try
                        Await repo.ReadPageAsync("users", 0, 25, CancellationToken.None)
                        Throw New Exception("Restricted page accepted")
                    Catch ex As UnauthorizedAccessException
                        check(True, "Restricted page rejected for " & role)
                    End Try
                Catch ex As UnauthorizedAccessException When role = "unknown"
                    check(True, "Unknown roles never default to administrator")
                End Try
            End Using
        Next
        Using server As New FakeSupabaseServer With {.HasProfile = False}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), sessions As New SupabaseSessionManager(gateway)
            Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
            Try
                Await New SupabaseRentalRepository(gateway, sessions).GetCurrentUserAsync(CancellationToken.None)
                Throw New Exception("Missing staff profile accepted")
            Catch ex As UnauthorizedAccessException
                check(True, "Valid Auth account without staff profile is denied")
            End Try
        End Using
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(options, client)
            Dim clock As New TestClock()
            Using sessions As New SupabaseSessionManager(gateway, clock)
                Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
                server.InvalidRefresh = True
                clock.Current = clock.Current.AddHours(2)
                Try
                    Await sessions.GetAccessTokenAsync(CancellationToken.None)
                    Throw New Exception("Expired refresh accepted")
                Catch ex As UnauthorizedAccessException
                    check(sessions.UserId = Guid.Empty, "Expired refresh clears session instead of using demo login")
                End Try
            End Using
        End Using
        Using server As New FakeSupabaseServer(), client As New HttpClient(server), gateway As New SupabaseGateway(options, client), sessions As New SupabaseSessionManager(gateway)
            Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
            server.Offline = True
            Try
                Await sessions.SignOutAsync(CancellationToken.None)
                Throw New Exception("Offline logout unexpectedly succeeded")
            Catch ex As HttpRequestException
                check(sessions.UserId = Guid.Empty, "Local logout still clears identity while offline")
            End Try
        End Using
        Using server As New FakeSupabaseServer With {.RejectEveryRead = True}, client As New HttpClient(server), gateway As New SupabaseGateway(options, client), sessions As New SupabaseSessionManager(gateway)
            Await sessions.SignInAsync("staff@example.test", "TEST_PASSWORD", CancellationToken.None)
            Try
                Await New SupabaseRentalRepository(gateway, sessions).GetCurrentUserAsync(CancellationToken.None)
                Throw New Exception("Permanently rejected session accepted")
            Catch ex As UnauthorizedAccessException
                check(server.RefreshCount = 1, "Repeated 401 ends the session path after one refresh instead of looping")
            End Try
        End Using
        Return passed
    End Function
End Module
