Option Strict On
Option Explicit On
Option Infer On
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Forms
Imports RantauGear.Models
Imports RantauGear.Services
Imports RantauGear.Pages

Module Checks
    Private passed As Integer
    Private Sub Check(condition As Boolean, description As String)
        If Not condition Then Throw New Exception("FAIL: " & description)
        passed += 1
        Console.WriteLine("PASS: " & description)
    End Sub
    Private Sub Rejected(action As Action, description As String)
        Try
            action()
        Catch ex As InvalidOperationException
            Check(True, description)
            Return
        End Try
        Throw New Exception("FAIL: " & description)
    End Sub
    Private Function Field(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.NonPublic Or BindingFlags.Instance).GetValue(target), T)
    End Function
    Private Sub InvokeHandler(target As Object, name As String)
        target.GetType().GetMethod(name, BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(target, {Nothing, EventArgs.Empty})
    End Sub
    Private Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each nested In Descendants(child)
                Yield nested
            Next
        Next
    End Function
    <STAThread>
    Public Sub Main(args As String())
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        passed += SupabaseChecks.RunAsync().GetAwaiter().GetResult()
        Dim repo As New DummyRepository()
        Dim admin = repo.GetUsers()(0)
        Dim front = repo.GetUsers()(1)
        Dim qc = repo.GetUsers()(2)
        Dim auth As New AuthService(repo)
        Check(auth.Login(" SUPERADMIN ", "admin123") Is admin, "Demo login trims username and ignores username case")
        Check(auth.Login("superadmin", "wrong") Is Nothing, "Wrong password rejected")
        front.IsActive = False
        Check(auth.Login("front", "front123") Is Nothing, "Inactive account rejected")
        front.IsActive = True
        Check(Not auth.CanAccess(front, "users") AndAlso Not auth.CanAccess(qc, "booking"), "Role navigation restricted")
        Dim service As New RentalService(repo)
        Dim customer As New Customer With {.Name = "Pelanggan uji", .Phone = "0800000000", .IdentityVerified = True}
        Dim startAt As New DateTime(2026, 11, 19, 10, 0, 0)
        Rejected(Sub() service.Checkout(customer, {1}, startAt, startAt, 0, admin), "Invalid time interval rejected")
        Rejected(Sub() service.Checkout(customer, {}, startAt, startAt.AddDays(1), 0, admin), "Empty unit selection rejected")
        Rejected(Sub() service.Checkout(customer, {1, 1}, startAt, startAt.AddDays(1), 0, admin), "Duplicate physical units rejected")
        Rejected(Sub() service.Checkout(customer, {4}, startAt, startAt.AddDays(1), 0, admin), "Maintenance unit rejected")
        Rejected(Sub() service.Checkout(customer, {1}, startAt, startAt.AddDays(1), 0, qc), "Warehouse role cannot create booking")
        customer.IdentityVerified = False
        Rejected(Sub() service.Checkout(customer, {1}, startAt, startAt.AddDays(1), 0, admin), "Identity verification required")
        customer.IdentityVerified = True
        Dim rental = service.Checkout(customer, {1}, startAt, startAt.AddDays(1), 100000, front)
        Check(rental.Subtotal = 85000 AndAlso rental.Total = 185000 AndAlso repo.GetUnitById(1).Status = UnitStatus.Rented, "Checkout calculates one-day price and locks inventory")
        Rejected(Sub() service.Checkout(customer, {1}, startAt, startAt.AddDays(1), 0, admin), "Double booking blocked")
        Dim fine As New FineCalculator()
        Check(fine.CalculateLateFine(rental, rental.DueAt.AddMinutes(30)) = 0, "30 minute grace period includes boundary")
        Check(fine.CalculateLateFine(rental, rental.DueAt.AddMinutes(31)) = 8500, "First late minute after grace charges one hour")
        Check(fine.CalculateLateFine(rental, rental.DueAt.AddMinutes(91)) = 17000, "Late hours round upward")
        Rejected(Sub() service.CompleteReturn(rental.Id, {}, qc, rental.DueAt), "Return requires all units inspected")
        Dim checks As New List(Of Inspection) From {New Inspection With {.UnitId = 1, .Condition = "Perlu perawatan", .FineAmount = 25000}}
        Rejected(Sub() service.CompleteReturn(rental.Id, checks, qc, rental.DueAt), "Damage requires notes")
        checks(0).Notes = "Tiang perlu diperbaiki"
        Rejected(Sub() service.CompleteReturn(rental.Id, checks, front, rental.DueAt), "Front office cannot complete QC")
        service.CompleteReturn(rental.Id, checks, qc, rental.DueAt.AddMinutes(31))
        Check(rental.Status = RentalStatus.Completed AndAlso rental.DamageFine = 25000 AndAlso rental.LateFine = 8500 AndAlso repo.GetUnitById(1).Status = UnitStatus.Maintenance, "Return records damage, lateness and maintenance")
        Check(fine.CalculateLateFine(rental) = 8500, "Completed rental fines stop at actual return time")
        Check(repo.GetInspections().Any(Function(i) i.RentalId = rental.Id AndAlso i.InspectorName = qc.DisplayName), "QC inspector recorded")
        Rejected(Sub() service.CompleteReturn(rental.Id, checks, qc, rental.DueAt.AddHours(1)), "Duplicate return rejected")
        Dim second = service.Checkout(customer, {3}, startAt, startAt.AddHours(25), 0, admin)
        Check(second.Subtotal = 130000, "25 hours bills two daily periods")
        service.CompleteReturn(second.Id, {New Inspection With {.UnitId = 3, .Condition = "Baik"}}, qc, second.DueAt)
        Check(repo.GetUnitById(3).Status = UnitStatus.Ready, "Good return releases inventory")
        Dim bundleRepo As New DummyRepository()
        Dim bundleService As New RentalService(bundleRepo)
        Dim bundle = bundleRepo.GetPackages()(0)
        Dim bundled = bundleService.Checkout(New Customer With {.Name = "Uji paket", .Phone = "0800000000", .IdentityVerified = True}, bundle.Items.Select(Function(i) i.UnitId), startAt, startAt.AddDays(1), 0, bundleRepo.GetUsers()(0), bundle.Id)
        Check(bundled.Subtotal = bundle.DailyPrice AndAlso bundled.Items.Count = bundle.Items.Count, "Bundle uses package price and reserves every physical unit")
        Dim availability As New AvailabilityService(repo)
        Check(Not availability.IsUnitAvailable(13, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2)), "Overdue unreturned unit stays unavailable for future periods")
        Check(Not availability.IsUnitAvailable(3, startAt, startAt), "Availability rejects invalid interval")
        Using login As New LoginForm()
            Check(login.AcceptButton Is Field(Of RantauGear.UI.RoundedButton)(login, "loginButton"), "Enter key is connected to login")
            Dim passwordBox = Field(Of TextBox)(login, "passwordBox")
            Check(passwordBox.UseSystemPasswordChar, "Login masks password initially")
            InvokeHandler(login, "TogglePassword")
            Check(Not passwordBox.UseSystemPasswordChar, "Show password control reveals demo field")
            InvokeHandler(login, "TogglePassword")
            Check(passwordBox.UseSystemPasswordChar, "Show password control restores masking")
            InvokeHandler(login, "LoginClicked")
            Check(Field(Of Label)(login, "errorLabel").Text.Contains("tidak sesuai"), "Invalid login shows inline validation without opening main window")
        End Using
        For Each actor In repo.GetUsers()
            Using main As New MainForm(actor, repo)
                Dim actual = Descendants(main).OfType(Of Button)().Where(Function(b) TypeOf b.Tag Is String).Select(Function(b) CStr(b.Tag)).OrderBy(Function(key) key).ToArray()
                Dim expected = {"dashboard", "catalog", "packages", "booking", "return-qc", "reports", "users"}.Where(Function(key) auth.CanAccess(actor, key)).OrderBy(Function(key) key).ToArray()
                Check(actual.SequenceEqual(expected), "Visible navigation matches role " & actor.Role.ToString())
            End Using
        Next
        Using booking As New BookingDialog(New DummyRepository(), admin)
            InvokeHandler(booking, "SaveBooking")
            Check(Field(Of Label)(booking, "errorText").Text.Contains("wajib diisi"), "Booking form rejects empty customer fields")
            Check(booking.CancelButton IsNot Nothing, "Booking Escape key has a cancel target")
        End Using
        Using report As New ReportsPage(repo)
            Dim csvMethod = GetType(ReportsPage).GetMethod("Csv", BindingFlags.NonPublic Or BindingFlags.Static)
            Dim encoded = CStr(csvMethod.Invoke(Nothing, {"Nama, ""kutipan"""}))
            Check(encoded = """Nama, """"kutipan""""""", "CSV quotes commas and doubles embedded quotes")
            Dim formula = CStr(csvMethod.Invoke(Nothing, {"=1+1"}))
            Check(formula.StartsWith("""'="), "CSV prevents formula execution for formula-prefixed text")
            Field(Of DateTimePicker)(report, "fromDate").Value = DateTime.Today.AddDays(1)
            Check(Field(Of Label)(report, "totals").Text.Contains("Tanggal awal"), "Report rejects reversed date filter")
        End Using
        Console.WriteLine(passed.ToString() & " checks passed.")
        If args.Contains("--check") Then Return
        Application.Run(New MainForm(New DummyRepository().GetUsers()(0), New DummyRepository()))
    End Sub
End Module
