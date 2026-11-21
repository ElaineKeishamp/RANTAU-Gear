Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Models

Namespace Data
    Public Class DummyRepository
        Implements IRentalRepository
        Public Function ReadMasterAsync(kind As MasterDataKind, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of MasterRecordPage) Implements IRentalRepository.ReadMasterAsync
            Throw New NotSupportedException("Data master dikelola pada repository online.")
        End Function
        Public Function SaveMasterAsync(requestId As Guid, record As MasterRecord, cancellationToken As CancellationToken) As Task(Of Long) Implements IRentalRepository.SaveMasterAsync
            Throw New NotSupportedException("Data master dikelola pada repository online.")
        End Function
        Public ReadOnly Property IsOnline As Boolean Implements IRentalRepository.IsOnline
            Get
                Return False
            End Get
        End Property
        Public Function GetCurrentUserAsync(cancellationToken As CancellationToken) As Task(Of AppUser) Implements IRentalRepository.GetCurrentUserAsync
            Throw New NotSupportedException("Repository demo tidak memiliki sesi online.")
        End Function
        Public Function ReadPageAsync(section As String, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of RepositoryPage) Implements IRentalRepository.ReadPageAsync
            Throw New NotSupportedException("Gunakan pembaca demo lokal untuk pratinjau pengujian.")
        End Function
        Public Function SignOutAsync(cancellationToken As CancellationToken) As Task Implements IRentalRepository.SignOutAsync
            Return Task.CompletedTask
        End Function

        Private ReadOnly categories As New List(Of Category)()
        Private ReadOnly units As New List(Of Unit)()
        Private ReadOnly packages As New List(Of Package)()
        Private ReadOnly customers As New List(Of Customer)()
        Private ReadOnly rentals As New List(Of Rental)()
        Private ReadOnly inspections As New List(Of Inspection)()
        Private ReadOnly fines As New List(Of Fine)()
        Private ReadOnly users As New List(Of AppUser)()

        Public Sub New()
            SeedCategories()
            SeedUnits()
            SeedPackages()
            SeedUsers()
            SeedCustomers()
            SeedRentals()
            SeedHistory()
        End Sub

        Public Function GetCategories() As List(Of Category) Implements IRentalRepository.GetCategories
            Return categories
        End Function
        Public Function GetUnits() As List(Of Unit) Implements IRentalRepository.GetUnits
            Return units
        End Function
        Public Function GetPackages() As List(Of Package) Implements IRentalRepository.GetPackages
            Return packages
        End Function
        Public Function GetCustomers() As List(Of Customer) Implements IRentalRepository.GetCustomers
            Return customers
        End Function
        Public Function GetRentals() As List(Of Rental) Implements IRentalRepository.GetRentals
            Return rentals
        End Function
        Public Function GetInspections() As List(Of Inspection) Implements IRentalRepository.GetInspections
            Return inspections
        End Function
        Public Function GetFines() As List(Of Fine) Implements IRentalRepository.GetFines
            Return fines
        End Function
        Public Function GetUsers() As List(Of AppUser) Implements IRentalRepository.GetUsers
            Return users
        End Function
        Public Function GetUnitById(id As Integer) As Unit Implements IRentalRepository.GetUnitById
            Return units.FirstOrDefault(Function(x As Unit) x.Id = id)
        End Function
        Public Function GetCategoryById(id As Integer) As Category Implements IRentalRepository.GetCategoryById
            Return categories.FirstOrDefault(Function(x As Category) x.Id = id)
        End Function
        Public Function GetPackageById(id As Integer) As Package Implements IRentalRepository.GetPackageById
            Return packages.FirstOrDefault(Function(x As Package) x.Id = id)
        End Function
        Public Function GetCustomerById(id As Integer) As Customer Implements IRentalRepository.GetCustomerById
            Return customers.FirstOrDefault(Function(x As Customer) x.Id = id)
        End Function
        Public Function GetRentalById(id As Integer) As Rental Implements IRentalRepository.GetRentalById
            Return rentals.FirstOrDefault(Function(x As Rental) x.Id = id)
        End Function

        Public Sub AddUnit(item As Unit) Implements IRentalRepository.AddUnit
            units.Add(item)
        End Sub
        Public Sub UpdateUnit(item As Unit) Implements IRentalRepository.UpdateUnit
            Dim oldItem As Unit = GetUnitById(item.Id)
            If oldItem Is Nothing Then Return
            oldItem.Code = item.Code
            oldItem.Name = item.Name
            oldItem.CategoryId = item.CategoryId
            oldItem.HourlyRate = item.HourlyRate
            oldItem.DailyRate = item.DailyRate
            oldItem.Status = item.Status
            oldItem.Description = item.Description
        End Sub
        Public Sub DeleteUnit(id As Integer) Implements IRentalRepository.DeleteUnit
            Dim item As Unit = GetUnitById(id)
            If item IsNot Nothing Then units.Remove(item)
        End Sub
        Public Sub AddCustomer(item As Customer) Implements IRentalRepository.AddCustomer
            customers.Add(item)
        End Sub
        Public Sub AddRental(item As Rental) Implements IRentalRepository.AddRental
            rentals.Add(item)
        End Sub
        Public Sub UpdateRental(item As Rental) Implements IRentalRepository.UpdateRental
            Dim oldItem As Rental = GetRentalById(item.Id)
            If oldItem Is Nothing Then Return
            rentals(rentals.IndexOf(oldItem)) = item
        End Sub
        Public Sub AddInspection(item As Inspection) Implements IRentalRepository.AddInspection
            inspections.Add(item)
        End Sub
        Public Sub AddFine(item As Fine) Implements IRentalRepository.AddFine
            fines.Add(item)
        End Sub
        Public Sub AddUser(item As AppUser) Implements IRentalRepository.AddUser
            users.Add(item)
        End Sub
        Public Sub UpdateUser(item As AppUser) Implements IRentalRepository.UpdateUser
            Dim oldItem As AppUser = users.FirstOrDefault(Function(x As AppUser) x.Id = item.Id)
            If oldItem Is Nothing Then Return
            oldItem.Username = item.Username
            oldItem.DisplayName = item.DisplayName
            oldItem.Password = item.Password
            oldItem.Role = item.Role
            oldItem.IsActive = item.IsActive
        End Sub
        Public Function NextUnitId() As Integer Implements IRentalRepository.NextUnitId
            Return If(units.Count = 0, 1, units.Max(Function(x As Unit) x.Id) + 1)
        End Function
        Public Function NextCustomerId() As Integer Implements IRentalRepository.NextCustomerId
            Return If(customers.Count = 0, 1, customers.Max(Function(x As Customer) x.Id) + 1)
        End Function
        Public Function NextRentalId() As Integer Implements IRentalRepository.NextRentalId
            Return If(rentals.Count = 0, 1, rentals.Max(Function(x As Rental) x.Id) + 1)
        End Function
        Public Function NextInspectionId() As Integer Implements IRentalRepository.NextInspectionId
            Return If(inspections.Count = 0, 1, inspections.Max(Function(x As Inspection) x.Id) + 1)
        End Function
        Public Function NextFineId() As Integer Implements IRentalRepository.NextFineId
            Return If(fines.Count = 0, 1, fines.Max(Function(x As Fine) x.Id) + 1)
        End Function
        Public Function NextUserId() As Integer Implements IRentalRepository.NextUserId
            Return If(users.Count = 0, 1, users.Max(Function(x As AppUser) x.Id) + 1)
        End Function

        Private Sub SeedCategories()
            Dim names() As String = {"Tenda", "Perlengkapan Tidur", "Alat Masak", "Tas & Carrier", "Penerangan", "Kamera", "Lensa", "Drone & Aksesori"}
            For i As Integer = 0 To names.Length - 1
                categories.Add(New Category With {.Id = i + 1, .Name = names(i)})
            Next
        End Sub

        Private Sub SeedUnits()
            AddUnit(1, "TND-001", "Tenda Dome 4P", 1, 15000D, 85000D, UnitStatus.Ready)
            AddUnit(2, "TND-002", "Tenda Dome 4P", 1, 15000D, 85000D, UnitStatus.Rented)
            AddUnit(3, "TND-003", "Tenda Dome 2P", 1, 12000D, 65000D, UnitStatus.Ready)
            AddUnit(4, "TND-004", "Tenda Dome 2P", 1, 12000D, 65000D, UnitStatus.Maintenance)
            AddUnit(5, "SLP-001", "Sleeping Bag Polar", 2, 8000D, 45000D, UnitStatus.Ready)
            AddUnit(6, "SLP-002", "Sleeping Bag Polar", 2, 8000D, 45000D, UnitStatus.Ready)
            AddUnit(7, "MTR-001", "Matras Camping", 2, 5000D, 25000D, UnitStatus.Ready)
            AddUnit(8, "MTR-002", "Matras Camping", 2, 5000D, 25000D, UnitStatus.Rented)
            AddUnit(9, "MSK-001", "Kompor Portable", 3, 10000D, 50000D, UnitStatus.Ready)
            AddUnit(10, "MSK-002", "Nesting Set 4 Person", 3, 12000D, 60000D, UnitStatus.Ready)
            AddUnit(11, "MSK-003", "Gas Kaleng Portable", 3, 5000D, 25000D, UnitStatus.Ready)
            AddUnit(12, "CAR-001", "Carrier 60L", 4, 15000D, 80000D, UnitStatus.Ready)
            AddUnit(13, "CAR-002", "Carrier 60L", 4, 15000D, 80000D, UnitStatus.Rented)
            AddUnit(14, "CAR-003", "Daypack 25L", 4, 8000D, 40000D, UnitStatus.Ready)
            AddUnit(15, "LMP-001", "Headlamp LED", 5, 4000D, 20000D, UnitStatus.Ready)
            AddUnit(16, "LMP-002", "Lamp Camping LED", 5, 5000D, 25000D, UnitStatus.Ready)
            AddUnit(17, "LMP-003", "Powerbank Outdoor", 5, 7000D, 35000D, UnitStatus.Maintenance)
            AddUnit(18, "CAM-001", "Mirrorless Sony A6400", 6, 75000D, 400000D, UnitStatus.Ready)
            AddUnit(19, "CAM-002", "Action Cam GoPro", 6, 50000D, 250000D, UnitStatus.Rented)
            AddUnit(20, "CAM-003", "Kamera Vlog Compact", 6, 60000D, 300000D, UnitStatus.Ready)
            AddUnit(21, "LNS-001", "Lensa 50mm f/1.8", 7, 30000D, 150000D, UnitStatus.Ready)
            AddUnit(22, "LNS-002", "Lensa Wide 16mm", 7, 45000D, 225000D, UnitStatus.Ready)
            AddUnit(23, "DRN-001", "Drone DJI Mini", 8, 100000D, 500000D, UnitStatus.Ready)
            AddUnit(24, "DRN-002", "Tripod Kamera", 8, 15000D, 75000D, UnitStatus.Ready)
            AddUnit(25, "DRN-003", "Gimbal Smartphone", 8, 25000D, 125000D, UnitStatus.Maintenance)
            AddUnit(26, "DRN-004", "Mic Wireless", 8, 30000D, 150000D, UnitStatus.Ready)
        End Sub

        Private Sub AddUnit(id As Integer, code As String, name As String, categoryId As Integer, hourly As Decimal, daily As Decimal, status As UnitStatus)
            units.Add(New Unit With {.Id = id, .Code = code, .Name = name, .CategoryId = categoryId, .HourlyRate = hourly, .DailyRate = daily, .Status = status, .Description = name & " untuk kebutuhan outdoor dan dokumentasi."})
        End Sub

        Private Sub SeedPackages()
            packages.Add(New Package With {.Id = 1, .Name = "Paket Camping 4 Orang", .Description = "Camping lengkap untuk empat orang.", .DailyPrice = 275000D, .Items = New List(Of PackageItem) From {New PackageItem With {.Id = 1, .PackageId = 1, .UnitId = 1, .Quantity = 1}, New PackageItem With {.Id = 2, .PackageId = 1, .UnitId = 5, .Quantity = 1}, New PackageItem With {.Id = 3, .PackageId = 1, .UnitId = 6, .Quantity = 1}, New PackageItem With {.Id = 4, .PackageId = 1, .UnitId = 7, .Quantity = 1}, New PackageItem With {.Id = 5, .PackageId = 1, .UnitId = 9, .Quantity = 1}}})
            packages.Add(New Package With {.Id = 2, .Name = "Paket Camping 2 Orang", .Description = "Camping ringkas untuk dua orang.", .DailyPrice = 165000D, .Items = New List(Of PackageItem) From {New PackageItem With {.Id = 6, .PackageId = 2, .UnitId = 3, .Quantity = 1}, New PackageItem With {.Id = 7, .PackageId = 2, .UnitId = 5, .Quantity = 1}, New PackageItem With {.Id = 8, .PackageId = 2, .UnitId = 7, .Quantity = 1}, New PackageItem With {.Id = 9, .PackageId = 2, .UnitId = 9, .Quantity = 1}}})
            packages.Add(New Package With {.Id = 3, .Name = "Paket Pendakian", .Description = "Perlengkapan dasar pendakian.", .DailyPrice = 225000D, .Items = New List(Of PackageItem) From {New PackageItem With {.Id = 10, .PackageId = 3, .UnitId = 3, .Quantity = 1}, New PackageItem With {.Id = 11, .PackageId = 3, .UnitId = 12, .Quantity = 1}, New PackageItem With {.Id = 12, .PackageId = 3, .UnitId = 5, .Quantity = 1}, New PackageItem With {.Id = 13, .PackageId = 3, .UnitId = 15, .Quantity = 1}}})
            packages.Add(New Package With {.Id = 4, .Name = "Paket Vlog Trip", .Description = "Kamera dan aksesori untuk vlog.", .DailyPrice = 650000D, .Items = New List(Of PackageItem) From {New PackageItem With {.Id = 14, .PackageId = 4, .UnitId = 18, .Quantity = 1}, New PackageItem With {.Id = 15, .PackageId = 4, .UnitId = 21, .Quantity = 1}, New PackageItem With {.Id = 16, .PackageId = 4, .UnitId = 24, .Quantity = 1}, New PackageItem With {.Id = 17, .PackageId = 4, .UnitId = 26, .Quantity = 1}}})
        End Sub

        Private Sub SeedUsers()
            users.Add(New AppUser With {.Id = 1, .Username = "superadmin", .DisplayName = "Administrator", .Password = "admin123", .Role = UserRole.SuperAdmin})
            users.Add(New AppUser With {.Id = 2, .Username = "front", .DisplayName = "Front Office", .Password = "front123", .Role = UserRole.FrontOffice})
            users.Add(New AppUser With {.Id = 3, .Username = "gudang", .DisplayName = "Gudang dan QC", .Password = "gudang123", .Role = UserRole.GudangQc})
        End Sub

        Private Sub SeedCustomers()
            customers.Add(New Customer With {.Id = 1, .Name = "Raka Pratama", .Phone = "081234567890", .Address = "Bandung", .GuaranteeType = "KTM ditahan", .Deposit = 300000D, .IdentityVerified = True})
            customers.Add(New Customer With {.Id = 2, .Name = "Sinta Maharani", .Phone = "081298765432", .Address = "Jakarta", .GuaranteeType = "Deposit tunai", .Deposit = 500000D, .IdentityVerified = True})
            customers.Add(New Customer With {.Id = 3, .Name = "Bima Saputra", .Phone = "082112223333", .Address = "Bogor", .GuaranteeType = "Kartu identitas lain ditunjukkan", .Deposit = 250000D, .IdentityVerified = True})
        End Sub

        Private Sub SeedRentals()
            Dim nowTime As DateTime = DateTime.Now
            AddRental(1, "RNT-001", 1, nowTime.AddHours(-8), nowTime.AddHours(16), RentalStatus.Active, 170000D, 300000D, 470000D, 2, "Tenda Dome 4P", 85000D, 8, "Matras Camping", 25000D)
            AddRental(2, "RNT-002", 2, nowTime.AddHours(-35), nowTime.AddHours(-4), RentalStatus.Late, 400000D, 500000D, 940000D, 13, "Carrier 60L", 80000D, 19, "Action Cam GoPro", 250000D)
            AddRental(3, "RNT-003", 3, nowTime.AddDays(-12), nowTime.AddDays(-10), RentalStatus.Completed, 65000D, 250000D, 390000D, 4, "Tenda Dome 2P", 65000D)
            AddRental(4, "RNT-004", 1, nowTime.AddDays(-8), nowTime.AddDays(-7), RentalStatus.Completed, 300000D, 300000D, 600000D, 20, "Kamera Vlog Compact", 300000D)
            AddRental(5, "RNT-005", 2, nowTime.AddDays(-5), nowTime.AddDays(-4), RentalStatus.Completed, 150000D, 500000D, 650000D, 21, "Lensa 50mm f/1.8", 150000D)
            AddRental(6, "RNT-006", 3, nowTime.AddDays(-3), nowTime.AddDays(-2), RentalStatus.Completed, 75000D, 250000D, 325000D, 24, "Tripod Kamera", 75000D)
        End Sub

        Private Sub AddRental(id As Integer, number As String, customerId As Integer, startAt As DateTime, dueAt As DateTime, status As RentalStatus, subtotal As Decimal, deposit As Decimal, total As Decimal, unit1 As Integer, name1 As String, rate1 As Decimal, Optional unit2 As Integer = 0, Optional name2 As String = "", Optional rate2 As Decimal = 0D)
            Dim rental As New Rental With {.Id = id, .RentalNumber = number, .CustomerId = customerId, .StartAt = startAt, .DueAt = dueAt, .Status = status, .Subtotal = subtotal, .Deposit = deposit, .Total = total, .CreatedBy = "front"}
            rental.Items.Add(New RentalItem With {.Id = id * 10, .RentalId = id, .UnitId = unit1, .ItemName = name1, .Quantity = 1, .DailyRate = rate1})
            If unit2 > 0 Then rental.Items.Add(New RentalItem With {.Id = id * 10 + 1, .RentalId = id, .UnitId = unit2, .ItemName = name2, .Quantity = 1, .DailyRate = rate2})
            If status = RentalStatus.Completed Then rental.ReturnedAt = dueAt
            rentals.Add(rental)
        End Sub

        Private Sub SeedHistory()
            inspections.Add(New Inspection With {.Id = 1, .RentalId = 3, .UnitId = 4, .InspectionType = "Pengembalian", .Condition = "Lecet ringan", .Notes = "Lecet pada tiang tenda.", .FineAmount = 75000D, .InspectionDate = DateTime.Now.AddDays(-10), .InspectorName = "Gudang QC"})
            inspections.Add(New Inspection With {.Id = 2, .RentalId = 4, .UnitId = 20, .InspectionType = "Pengembalian", .Condition = "Baik", .Notes = "Kamera lengkap.", .InspectionDate = DateTime.Now.AddDays(-7), .InspectorName = "Gudang QC"})
            inspections.Add(New Inspection With {.Id = 3, .RentalId = 6, .UnitId = 24, .InspectionType = "Pengembalian", .Condition = "Baik", .Notes = "Tripod normal.", .InspectionDate = DateTime.Now.AddDays(-2), .InspectorName = "Gudang QC"})
            fines.Add(New Fine With {.Id = 2, .RentalId = 3, .FineType = "Kerusakan", .Description = "Lecet ringan pada tenda.", .Amount = 75000D, .CreatedAt = DateTime.Now.AddDays(-10)})
            fines.Add(New Fine With {.Id = 3, .RentalId = 5, .FineType = "Keterlambatan", .Description = "Melewati waktu toleransi.", .Amount = 30000D, .CreatedAt = DateTime.Now.AddDays(-4)})
            GetRentalById(5).ReturnedAt = GetRentalById(5).DueAt.AddHours(2)
            For Each rental In rentals
                rental.LateFine = fines.Where(Function(f) f.RentalId = rental.Id AndAlso f.FineType = "Keterlambatan").Sum(Function(f) f.Amount)
                rental.DamageFine = fines.Where(Function(f) f.RentalId = rental.Id AndAlso f.FineType = "Kerusakan").Sum(Function(f) f.Amount)
                rental.Total = rental.Subtotal + rental.Deposit + rental.LateFine + rental.DamageFine
            Next
        End Sub
    End Class
End Namespace
