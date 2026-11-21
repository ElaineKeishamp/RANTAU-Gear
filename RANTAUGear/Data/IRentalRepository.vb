Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Models

Namespace Data
    Public Interface IRentalRepository
        Function GetReservationOptionsAsync(ct As CancellationToken) As Task(Of ReservationOptions)
        Function PreviewReservationAsync(request As ReservationRequest, ct As CancellationToken) As Task(Of ReservationQuote)
        Function ReserveWithDpAsync(requestId As Guid, request As ReservationRequest, quote As ReservationQuote, ct As CancellationToken) As Task(Of ReservationReceipt)
        Function ReadMasterAsync(kind As MasterDataKind, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of MasterRecordPage)
        Function SaveMasterAsync(requestId As Guid, record As MasterRecord, cancellationToken As CancellationToken) As Task(Of Long)
        ReadOnly Property IsOnline As Boolean
        Function GetCurrentUserAsync(cancellationToken As CancellationToken) As Task(Of AppUser)
        Function ReadPageAsync(section As String, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of RepositoryPage)
        Function SignOutAsync(cancellationToken As CancellationToken) As Task
        Function GetCategories() As List(Of Category)
        Function GetUnits() As List(Of Unit)
        Function GetPackages() As List(Of Package)
        Function GetCustomers() As List(Of Customer)
        Function GetRentals() As List(Of Rental)
        Function GetInspections() As List(Of Inspection)
        Function GetFines() As List(Of Fine)
        Function GetUsers() As List(Of AppUser)
        Function GetUnitById(id As Integer) As Unit
        Function GetCategoryById(id As Integer) As Category
        Function GetPackageById(id As Integer) As Package
        Function GetCustomerById(id As Integer) As Customer
        Function GetRentalById(id As Integer) As Rental
        Sub AddUnit(item As Unit)
        Sub UpdateUnit(item As Unit)
        Sub DeleteUnit(id As Integer)
        Sub AddCustomer(item As Customer)
        Sub AddRental(item As Rental)
        Sub UpdateRental(item As Rental)
        Sub AddInspection(item As Inspection)
        Sub AddFine(item As Fine)
        Sub AddUser(item As AppUser)
        Sub UpdateUser(item As AppUser)
        Function NextUnitId() As Integer
        Function NextCustomerId() As Integer
        Function NextRentalId() As Integer
        Function NextInspectionId() As Integer
        Function NextFineId() As Integer
        Function NextUserId() As Integer
    End Interface
End Namespace
