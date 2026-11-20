Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports RantauGear.Data
Imports RantauGear.Models

Namespace Services
    Public Class AvailabilityService
        Private ReadOnly repository As IRentalRepository
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
        End Sub
        Public Function IsUnitAvailable(unitId As Integer, startAt As DateTime, endAt As DateTime, Optional ignoredRentalId As Nullable(Of Integer) = Nothing) As Boolean
            If endAt <= startAt Then Return False
            Dim unit As Unit = repository.GetUnitById(unitId)
            If unit Is Nothing OrElse unit.Status = UnitStatus.Maintenance Then Return False
            Dim ignored As Integer = If(ignoredRentalId.HasValue, ignoredRentalId.Value, -1)
            Return Not repository.GetRentals().Any(Function(rental As Rental) rental.Id <> ignored AndAlso (rental.Status = RentalStatus.Active OrElse rental.Status = RentalStatus.Late) AndAlso rental.Items.Any(Function(item As RentalItem) item.UnitId = unitId) AndAlso (rental.DueAt < DateTime.Now OrElse (startAt < rental.DueAt AndAlso endAt > rental.StartAt)))
        End Function
        Public Function IsPackageAvailable(package As Package, startAt As DateTime, endAt As DateTime) As Boolean
            For Each item As PackageItem In package.Items
                If Not IsUnitAvailable(item.UnitId, startAt, endAt) Then Return False
            Next
            Return True
        End Function
        Public Function GetAvailableUnits(startAt As DateTime, endAt As DateTime) As List(Of Unit)
            Return repository.GetUnits().Where(Function(unit As Unit) IsUnitAvailable(unit.Id, startAt, endAt)).ToList()
        End Function
        Public Function GetAvailablePackages(startAt As DateTime, endAt As DateTime) As List(Of Package)
            Return repository.GetPackages().Where(Function(package As Package) IsPackageAvailable(package, startAt, endAt)).ToList()
        End Function
    End Class
End Namespace
