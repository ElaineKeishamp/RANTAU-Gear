Option Strict On
Option Explicit On
Option Infer On

Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Models
Imports RantauGear.Services

Namespace Data.Supabase
    Partial Public NotInheritable Class SupabaseRentalRepository
        Implements IRentalRepository

        Private ReadOnly gateway As SupabaseGateway
        Private ReadOnly sessions As SupabaseSessionManager
        Private expectedRole As UserRole?

        Public Sub New(gateway As SupabaseGateway, sessions As SupabaseSessionManager)
            Me.gateway = gateway
            Me.sessions = sessions
        End Sub

        Public ReadOnly Property IsOnline As Boolean Implements IRentalRepository.IsOnline
            Get
                Return True
            End Get
        End Property

        Public Async Function GetCurrentUserAsync(cancellationToken As CancellationToken) As Task(Of AppUser) Implements IRentalRepository.GetCurrentUserAsync
            Dim rows = Await ReadAsync("staff_profiles", "id,display_name,role,is_active", "id.asc", 0, 2, cancellationToken, sessions.UserId).ConfigureAwait(False)
            If rows.Records.Count <> 1 Then Throw New UnauthorizedAccessException("Akun belum terdaftar sebagai petugas. Hubungi Super Admin.")
            Dim row = rows.Records(0)
            If Guid.Parse(row.GetProperty("id").GetString()) <> sessions.UserId OrElse Not row.GetProperty("is_active").GetBoolean() Then
                Throw New UnauthorizedAccessException("Akun petugas tidak aktif. Hubungi Super Admin.")
            End If
            Dim role As UserRole
            Select Case row.GetProperty("role").GetString()
                Case "super_admin" : role = UserRole.SuperAdmin
                Case "front_office" : role = UserRole.FrontOffice
                Case "warehouse_qc" : role = UserRole.GudangQc
                Case Else : Throw New UnauthorizedAccessException("Peran akun tidak dikenali. Hubungi Super Admin.")
            End Select
            If expectedRole.HasValue AndAlso expectedRole.Value <> role Then Throw New UnauthorizedAccessException("Hak akses akun berubah. Silakan masuk kembali.")
            expectedRole = role
            Return New AppUser With {.AuthUserId = sessions.UserId, .DisplayName = row.GetProperty("display_name").GetString(), .Role = role, .IsActive = True}
        End Function

        Public Async Function ReadPageAsync(section As String, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of RepositoryPage) Implements IRentalRepository.ReadPageAsync
            If offset < 0 OrElse pageSize < 1 OrElse pageSize > 100 Then Throw New ArgumentOutOfRangeException(NameOf(pageSize))
            Dim user = Await GetCurrentUserAsync(cancellationToken).ConfigureAwait(False)
            If Not New AuthService(Me).CanAccess(user, section) Then Throw New UnauthorizedAccessException("Akun tidak dapat membuka halaman ini.")
            Dim resource As String
            Dim selection As String
            Dim ordering As String = "id.asc"
            Dim description As String
            Dim columns As ColumnSpec()
            Select Case section
                Case "dashboard"
                    resource = "rental_overview"
                    selection = "id,rental_number,pickup_at,due_at,display_state"
                    ordering = "pickup_at.desc,id.desc"
                    description = "Transaksi terbaru dari Supabase. Halaman ini menampilkan data tersimpan."
                    columns = {New ColumnSpec("rental_number", "Nomor sewa"), New ColumnSpec("pickup_at", "Jadwal ambil", GetType(DateTime)), New ColumnSpec("due_at", "Jadwal kembali", GetType(DateTime)), New ColumnSpec("display_state", "Status")}
                Case "catalog"
                    resource = "equipment_units"
                    selection = "id,code,state,equipment_types(name,daily_rate)"
                    description = "Unit fisik dan tarif dari database. Perubahan inventaris belum diaktifkan."
                    columns = {New ColumnSpec("code", "Kode unit"), New ColumnSpec("equipment_types.name", "Jenis alat"), New ColumnSpec("equipment_types.daily_rate", "Tarif per hari", GetType(Decimal)), New ColumnSpec("state", "Kondisi")}
                Case "packages"
                    resource = "packages"
                    selection = "id,name,daily_price,is_active"
                    description = "Daftar paket online. Pengelolaan isi paket akan dihubungkan pada tahap berikutnya."
                    columns = {New ColumnSpec("name", "Nama paket"), New ColumnSpec("daily_price", "Tarif per hari", GetType(Decimal)), New ColumnSpec("is_active", "Status")}
                Case "booking"
                    resource = "reservation_register"
                    selection = "id,rental_number,customer_name,pickup_at,due_at,display_state,rent_amount,deposit_due,dp_received"
                    ordering = "pickup_at.desc,id.desc"
                    description = "Reservasi dan DP diterima dari database. Pengambilan serta deposit dicatat pada tahap berikutnya."
                    columns = {New ColumnSpec("rental_number", "Nomor sewa"), New ColumnSpec("customer_name", "Pelanggan"), New ColumnSpec("pickup_at", "Jadwal ambil", GetType(DateTime)), New ColumnSpec("due_at", "Jadwal kembali", GetType(DateTime)), New ColumnSpec("display_state", "Status"), New ColumnSpec("rent_amount", "Nilai sewa", GetType(Decimal)), New ColumnSpec("dp_received", "DP diterima", GetType(Decimal)), New ColumnSpec("deposit_due", "Deposit saat ambil", GetType(Decimal))}
                Case "return-qc", "reports"
                    resource = "rental_overview"
                    selection = "id,rental_number,pickup_at,due_at,display_state,rent_amount,deposit_due"
                    ordering = "pickup_at.desc,id.desc"
                    description = If(section = "reports", "Nilai sewa dan deposit yang ditetapkan pada transaksi. Ini belum merupakan laporan penerimaan kas.", "Daftar sewa online. Reservasi dan DP dapat dicatat melalui Reservasi baru; pengambilan dan QC belum diaktifkan.")
                    columns = {New ColumnSpec("rental_number", "Nomor sewa"), New ColumnSpec("pickup_at", "Jadwal ambil", GetType(DateTime)), New ColumnSpec("due_at", "Jadwal kembali", GetType(DateTime)), New ColumnSpec("display_state", "Status"), New ColumnSpec("rent_amount", "Nilai sewa", GetType(Decimal)), New ColumnSpec("deposit_due", "Deposit ditetapkan", GetType(Decimal))}
                Case "users"
                    resource = "staff_profiles"
                    selection = "id,display_name,role,is_active"
                    description = "Profil petugas dari Supabase. Perubahan akun belum diaktifkan."
                    columns = {New ColumnSpec("display_name", "Nama petugas"), New ColumnSpec("role", "Peran"), New ColumnSpec("is_active", "Status")}
                Case Else
                    Throw New ArgumentException("Halaman tidak dikenali.", NameOf(section))
            End Select
            Dim result = Await ReadAsync(resource, selection, ordering, offset, pageSize, cancellationToken).ConfigureAwait(False)
            If Not result.Total.HasValue Then Throw New InvalidDataException("Layanan tidak mengembalikan jumlah data. Muat ulang halaman.")
            Dim table As New DataTable With {.Locale = CultureInfo.GetCultureInfo("id-ID")}
            For Each column In columns
                table.Columns.Add(column.Caption, column.ValueType)
            Next
            For Each record In result.Records
                Dim values(columns.Length - 1) As Object
                For i = 0 To columns.Length - 1
                    Dim value = ReadValue(record, columns(i).Path)
                    values(i) = ConvertValue(value, columns(i).ValueType, columns(i).Path)
                Next
                table.Rows.Add(values)
            Next
            Return New RepositoryPage(table, result.Total.Value, offset, pageSize, description)
        End Function

        Private Async Function ReadAsync(resource As String, selection As String, ordering As String, offset As Integer, size As Integer,
            cancellationToken As CancellationToken, Optional userId As Guid? = Nothing) As Task(Of SupabaseRows)
            Dim token = Await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(False)
            Dim retry As Boolean
            Try
                Return Await gateway.ReadRowsAsync(resource, selection, ordering, offset, size, token, cancellationToken, userId).ConfigureAwait(False)
            Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.Unauthorized
                retry = True
            End Try
            cancellationToken.ThrowIfCancellationRequested()
            If retry Then
                token = Await sessions.GetAccessTokenAsync(cancellationToken, token).ConfigureAwait(False)
                Try
                    Return Await gateway.ReadRowsAsync(resource, selection, ordering, offset, size, token, cancellationToken, userId).ConfigureAwait(False)
                Catch ex As SupabaseRequestException When ex.StatusCode = HttpStatusCode.Unauthorized
                    Throw New UnauthorizedAccessException("Sesi ditolak oleh layanan. Silakan masuk kembali.")
                End Try
            End If
            Throw New InvalidOperationException("Pembacaan data tidak selesai.")
        End Function

        Private Shared Function ReadValue(record As JsonElement, path As String) As JsonElement
            Dim result = record
            For Each segment In path.Split("."c)
                If result.ValueKind = JsonValueKind.Null Then Return result
                result = result.GetProperty(segment)
            Next
            Return result
        End Function

        Private Shared Function ConvertValue(value As JsonElement, targetType As Type, path As String) As Object
            If value.ValueKind = JsonValueKind.Null Then Return DBNull.Value
            If targetType Is GetType(Decimal) Then Return value.GetDecimal()
            If targetType Is GetType(DateTime) Then Return DateTimeOffset.Parse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToOffset(TimeSpan.FromHours(7)).DateTime
            If value.ValueKind = JsonValueKind.True OrElse value.ValueKind = JsonValueKind.False Then Return If(value.GetBoolean(), "Aktif", "Nonaktif")
            Dim text = value.GetString()
            If path <> "state" AndAlso path <> "display_state" AndAlso path <> "role" Then Return text
            Select Case text
                Case "super_admin" : Return "Super Admin"
                Case "front_office" : Return "Front office"
                Case "warehouse_qc" : Return "Gudang / QC"
                Case "reserved" : Return "Dipesan"
                Case "active", "rented" : Return "Disewa"
                Case "returned" : Return "Dikembalikan"
                Case "cancelled" : Return "Dibatalkan"
                Case "expired" : Return "Hangus"
                Case "late" : Return "Terlambat"
                Case "ready" : Return "Siap"
                Case "maintenance" : Return "Perawatan"
                Case "lost" : Return "Hilang"
                Case "retired" : Return "Dinonaktifkan"
                Case Else : Return text
            End Select
        End Function

        Private NotInheritable Class ColumnSpec
            Public ReadOnly Property Path As String
            Public ReadOnly Property Caption As String
            Public ReadOnly Property ValueType As Type
            Public Sub New(path As String, caption As String, Optional valueType As Type = Nothing)
                Me.Path = path
                Me.Caption = caption
                Me.ValueType = If(valueType, GetType(String))
            End Sub
        End Class

        Public Function SignOutAsync(cancellationToken As CancellationToken) As Task Implements IRentalRepository.SignOutAsync
            Return sessions.SignOutAsync(cancellationToken)
        End Function

        ' Legacy synchronous members serve the explicit test/demo repository only.
        ' Online callers must use the async boundary; writes await the next integration stage.
        Private Shared Function LegacyNotSupported() As Exception
            Return New NotSupportedException("Repository online memakai akses async. Perubahan data belum diaktifkan.")
        End Function
        Public Function GetCategories() As List(Of Category) Implements IRentalRepository.GetCategories
            Throw LegacyNotSupported()
        End Function
        Public Function GetUnits() As List(Of Unit) Implements IRentalRepository.GetUnits
            Throw LegacyNotSupported()
        End Function
        Public Function GetPackages() As List(Of Package) Implements IRentalRepository.GetPackages
            Throw LegacyNotSupported()
        End Function
        Public Function GetCustomers() As List(Of Customer) Implements IRentalRepository.GetCustomers
            Throw LegacyNotSupported()
        End Function
        Public Function GetRentals() As List(Of Rental) Implements IRentalRepository.GetRentals
            Throw LegacyNotSupported()
        End Function
        Public Function GetInspections() As List(Of Inspection) Implements IRentalRepository.GetInspections
            Throw LegacyNotSupported()
        End Function
        Public Function GetFines() As List(Of Fine) Implements IRentalRepository.GetFines
            Throw LegacyNotSupported()
        End Function
        Public Function GetUsers() As List(Of AppUser) Implements IRentalRepository.GetUsers
            Throw LegacyNotSupported()
        End Function
        Public Function GetUnitById(id As Integer) As Unit Implements IRentalRepository.GetUnitById
            Throw LegacyNotSupported()
        End Function
        Public Function GetCategoryById(id As Integer) As Category Implements IRentalRepository.GetCategoryById
            Throw LegacyNotSupported()
        End Function
        Public Function GetPackageById(id As Integer) As Package Implements IRentalRepository.GetPackageById
            Throw LegacyNotSupported()
        End Function
        Public Function GetCustomerById(id As Integer) As Customer Implements IRentalRepository.GetCustomerById
            Throw LegacyNotSupported()
        End Function
        Public Function GetRentalById(id As Integer) As Rental Implements IRentalRepository.GetRentalById
            Throw LegacyNotSupported()
        End Function
        Public Sub AddUnit(item As Unit) Implements IRentalRepository.AddUnit
            Throw LegacyNotSupported()
        End Sub
        Public Sub UpdateUnit(item As Unit) Implements IRentalRepository.UpdateUnit
            Throw LegacyNotSupported()
        End Sub
        Public Sub DeleteUnit(id As Integer) Implements IRentalRepository.DeleteUnit
            Throw LegacyNotSupported()
        End Sub
        Public Sub AddCustomer(item As Customer) Implements IRentalRepository.AddCustomer
            Throw LegacyNotSupported()
        End Sub
        Public Sub AddRental(item As Rental) Implements IRentalRepository.AddRental
            Throw LegacyNotSupported()
        End Sub
        Public Sub UpdateRental(item As Rental) Implements IRentalRepository.UpdateRental
            Throw LegacyNotSupported()
        End Sub
        Public Sub AddInspection(item As Inspection) Implements IRentalRepository.AddInspection
            Throw LegacyNotSupported()
        End Sub
        Public Sub AddFine(item As Fine) Implements IRentalRepository.AddFine
            Throw LegacyNotSupported()
        End Sub
        Public Sub AddUser(item As AppUser) Implements IRentalRepository.AddUser
            Throw LegacyNotSupported()
        End Sub
        Public Sub UpdateUser(item As AppUser) Implements IRentalRepository.UpdateUser
            Throw LegacyNotSupported()
        End Sub
        Public Function NextUnitId() As Integer Implements IRentalRepository.NextUnitId
            Throw LegacyNotSupported()
        End Function
        Public Function NextCustomerId() As Integer Implements IRentalRepository.NextCustomerId
            Throw LegacyNotSupported()
        End Function
        Public Function NextRentalId() As Integer Implements IRentalRepository.NextRentalId
            Throw LegacyNotSupported()
        End Function
        Public Function NextInspectionId() As Integer Implements IRentalRepository.NextInspectionId
            Throw LegacyNotSupported()
        End Function
        Public Function NextFineId() As Integer Implements IRentalRepository.NextFineId
            Throw LegacyNotSupported()
        End Function
        Public Function NextUserId() As Integer Implements IRentalRepository.NextUserId
            Throw LegacyNotSupported()
        End Function
    End Class
End Namespace
