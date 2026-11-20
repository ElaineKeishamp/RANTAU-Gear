Option Strict On
Option Explicit On
Option Infer On

Imports System.Linq
Imports RantauGear.Data
Imports RantauGear.Models

Namespace Services
    Public Class RentalService
        Private ReadOnly repository As IRentalRepository
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
        End Sub

        Public Function Checkout(customer As Customer, unitIds As IEnumerable(Of Integer), startAt As DateTime, dueAt As DateTime, deposit As Decimal, actor As AppUser, Optional packageId As Integer = 0) As Rental
            If actor Is Nothing OrElse Not actor.IsActive OrElse (actor.Role <> UserRole.SuperAdmin AndAlso actor.Role <> UserRole.FrontOffice) Then Throw New InvalidOperationException("Akun ini tidak dapat membuat booking.")
            If customer Is Nothing OrElse String.IsNullOrWhiteSpace(customer.Name) OrElse String.IsNullOrWhiteSpace(customer.Phone) Then Throw New InvalidOperationException("Nama dan nomor telepon pelanggan wajib diisi.")
            If Not customer.IdentityVerified Then Throw New InvalidOperationException("Konfirmasi verifikasi identitas pelanggan terlebih dahulu.")
            If dueAt <= startAt Then Throw New InvalidOperationException("Waktu kembali harus setelah waktu pengambilan.")
            If deposit < 0 Then Throw New InvalidOperationException("Deposit tidak boleh negatif.")
            Dim ids = unitIds.ToList()
            If ids.Count = 0 OrElse ids.Distinct().Count() <> ids.Count Then Throw New InvalidOperationException("Pilih minimal satu unit tanpa duplikasi.")
            Dim availability As New AvailabilityService(repository)
            For Each id In ids
                Dim unit = repository.GetUnitById(id)
                If unit Is Nothing OrElse unit.Status <> UnitStatus.Ready OrElse Not availability.IsUnitAvailable(id, startAt, dueAt) Then Throw New InvalidOperationException("Unit tidak tersedia. Periksa kembali pilihan alat.")
            Next
            Dim bundle As Package = Nothing
            If packageId <> 0 Then
                bundle = repository.GetPackageById(packageId)
                If bundle Is Nothing OrElse Not bundle.Items.All(Function(i) i.Quantity = 1) OrElse Not bundle.Items.Select(Function(i) i.UnitId).OrderBy(Function(id) id).SequenceEqual(ids.OrderBy(Function(id) id)) Then Throw New InvalidOperationException("Isi paket tidak sesuai dengan unit yang dipilih.")
            End If
            Dim days = CDec(Math.Max(1, Math.Ceiling((dueAt - startAt).TotalDays)))
            Dim subtotal = If(bundle Is Nothing, ids.Sum(Function(id) repository.GetUnitById(id).DailyRate), bundle.DailyPrice) * days
            Dim idRental = repository.NextRentalId()
            If customer.Id = 0 Then
                customer.Id = repository.NextCustomerId()
                repository.AddCustomer(customer)
            ElseIf repository.GetCustomerById(customer.Id) Is Nothing Then
                Throw New InvalidOperationException("Pelanggan tidak ditemukan.")
            End If
            Dim rental As New Rental With {.Id = idRental, .RentalNumber = "RNT-" & idRental.ToString("D4"), .CustomerId = customer.Id, .StartAt = startAt, .DueAt = dueAt, .Status = RentalStatus.Active, .Subtotal = subtotal, .Deposit = deposit, .Total = subtotal + deposit, .CreatedBy = actor.Username}
            For Each id In ids
                Dim unit = repository.GetUnitById(id)
                rental.Items.Add(New RentalItem With {.Id = idRental * 1000 + rental.Items.Count + 1, .RentalId = idRental, .UnitId = id, .ItemName = unit.Name, .DailyRate = unit.DailyRate, .Quantity = 1, .PackageId = packageId})
            Next
            repository.AddRental(rental)
            For Each id In ids
                Dim unit = repository.GetUnitById(id)
                unit.Status = UnitStatus.Rented
                repository.UpdateUnit(unit)
            Next
            Return rental
        End Function

        Public Sub CompleteReturn(rentalId As Integer, results As IEnumerable(Of Inspection), actor As AppUser, returnedAt As DateTime)
            If actor Is Nothing OrElse Not actor.IsActive OrElse (actor.Role <> UserRole.SuperAdmin AndAlso actor.Role <> UserRole.GudangQc) Then Throw New InvalidOperationException("Akun ini tidak dapat memproses pengembalian.")
            Dim rental = repository.GetRentalById(rentalId)
            If rental Is Nothing OrElse (rental.Status <> RentalStatus.Active AndAlso rental.Status <> RentalStatus.Late) Then Throw New InvalidOperationException("Sewa ini sudah selesai atau tidak ditemukan.")
            If returnedAt < rental.StartAt Then Throw New InvalidOperationException("Waktu kembali tidak boleh sebelum waktu pengambilan.")
            Dim checks = results.ToList()
            If checks.Count <> rental.Items.Count OrElse checks.Select(Function(i) i.UnitId).Distinct().Count() <> checks.Count OrElse Not rental.Items.All(Function(item) checks.Any(Function(i) i.UnitId = item.UnitId)) Then Throw New InvalidOperationException("Setiap unit wajib diperiksa satu kali.")
            For Each check In checks
                If check.FineAmount < 0 OrElse (check.Condition <> "Baik" AndAlso check.Condition <> "Perlu perawatan") Then Throw New InvalidOperationException("Kondisi atau denda tidak valid.")
                If (check.Condition <> "Baik" OrElse check.FineAmount > 0) AndAlso String.IsNullOrWhiteSpace(check.Notes) Then Throw New InvalidOperationException("Isi catatan untuk unit bermasalah atau denda kerusakan.")
            Next
            rental.LateFine = New FineCalculator().CalculateLateFine(rental, returnedAt)
            rental.DamageFine = checks.Sum(Function(i) i.FineAmount)
            For Each check In checks
                check.Id = repository.NextInspectionId()
                check.RentalId = rental.Id
                check.InspectionType = "Pengembalian"
                check.InspectionDate = returnedAt
                check.InspectorName = actor.DisplayName
                repository.AddInspection(check)
                Dim unit = repository.GetUnitById(check.UnitId)
                unit.Status = If(check.Condition = "Baik", UnitStatus.Ready, UnitStatus.Maintenance)
                repository.UpdateUnit(unit)
                Dim item = rental.Items.Single(Function(i) i.UnitId = check.UnitId)
                item.ConditionOnReturn = check.Condition
                item.ReturnNotes = check.Notes
                item.DamageFine = check.FineAmount
            Next
            If rental.LateFine > 0 Then repository.AddFine(New Fine With {.Id = repository.NextFineId(), .RentalId = rental.Id, .FineType = "Keterlambatan", .Description = "Melewati toleransi 30 menit.", .Amount = rental.LateFine, .CreatedAt = returnedAt})
            If rental.DamageFine > 0 Then repository.AddFine(New Fine With {.Id = repository.NextFineId(), .RentalId = rental.Id, .FineType = "Kerusakan", .Description = "Sesuai pemeriksaan setiap unit.", .Amount = rental.DamageFine, .CreatedAt = returnedAt})
            rental.ReturnedAt = returnedAt
            rental.Status = RentalStatus.Completed
            rental.Total = rental.Subtotal + rental.Deposit + rental.LateFine + rental.DamageFine
            repository.UpdateRental(rental)
        End Sub
    End Class
End Namespace
