Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports RantauGear.Models

Namespace Data.Supabase
    Public NotInheritable Class MasterSaveUncertainException
        Inherits IOException
        Public Sub New(inner As Exception)
            MyBase.New("Hasil penyimpanan belum dapat dikonfirmasi. Data dikunci agar aman; klik Simpan ulang untuk memeriksa permintaan yang sama.", inner)
        End Sub
    End Class

    Partial Public NotInheritable Class SupabaseRentalRepository
        Public Shared Function MasterResource(kind As MasterDataKind) As String
            Select Case kind
                Case MasterDataKind.Category : Return "categories"
                Case MasterDataKind.EquipmentType : Return "equipment_types"
                Case MasterDataKind.EquipmentUnit : Return "equipment_units"
                Case MasterDataKind.Package : Return "packages"
                Case MasterDataKind.Customer : Return "customers"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(kind))
            End Select
        End Function

        Private Async Function RequireMasterAsync(kind As MasterDataKind, cancellationToken As CancellationToken) As Task
            Dim user = Await GetCurrentUserAsync(cancellationToken).ConfigureAwait(False)
            If user.Role <> UserRole.SuperAdmin AndAlso Not (kind = MasterDataKind.Customer AndAlso user.Role = UserRole.FrontOffice) Then Throw New UnauthorizedAccessException("Akun tidak dapat mengelola data ini.")
        End Function

        Public Async Function ReadMasterAsync(kind As MasterDataKind, offset As Integer, pageSize As Integer, cancellationToken As CancellationToken) As Task(Of MasterRecordPage) Implements IRentalRepository.ReadMasterAsync
            Await RequireMasterAsync(kind, cancellationToken).ConfigureAwait(False)
            Dim resource = MasterResource(kind)
            Dim selection As String
            Select Case kind
                Case MasterDataKind.Category : selection = "id,row_version,name,is_active"
                Case MasterDataKind.EquipmentType : selection = "id,row_version,category_id,name,daily_rate,replacement_value,description,is_active,categories(name)"
                Case MasterDataKind.EquipmentUnit : selection = "id,row_version,equipment_type_id,code,state,notes,equipment_types(name)"
                Case MasterDataKind.Package : selection = "id,row_version,name,daily_price,is_active,package_components(equipment_type_id,quantity,is_active)"
                Case MasterDataKind.Customer : selection = "id,row_version,name,phone,address,is_active"
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(kind))
            End Select
            Dim response = Await ReadAsync(resource, selection, "id.asc", offset, pageSize, cancellationToken).ConfigureAwait(False)
            If Not response.Total.HasValue Then Throw New InvalidDataException("Jumlah data tidak tersedia dari layanan.")
            Dim records As New List(Of MasterRecord)()
            For Each row In response.Records
                Dim record As New MasterRecord With {.Kind = kind, .Id = row.GetProperty("id").GetInt64(), .RowVersion = row.GetProperty("row_version").GetInt64()}
                If kind <> MasterDataKind.EquipmentUnit Then
                    record.Name = row.GetProperty("name").GetString()
                    record.IsActive = row.GetProperty("is_active").GetBoolean()
                End If
                Select Case kind
                    Case MasterDataKind.EquipmentType
                        record.ParentId = row.GetProperty("category_id").GetInt64()
                        record.ParentName = row.GetProperty("categories").GetProperty("name").GetString()
                        record.DailyRate = row.GetProperty("daily_rate").GetDecimal()
                        record.ReplacementValue = row.GetProperty("replacement_value").GetDecimal()
                        record.Description = row.GetProperty("description").GetString()
                    Case MasterDataKind.EquipmentUnit
                        record.ParentId = row.GetProperty("equipment_type_id").GetInt64()
                        record.ParentName = row.GetProperty("equipment_types").GetProperty("name").GetString()
                        record.Code = row.GetProperty("code").GetString()
                        record.State = row.GetProperty("state").GetString()
                        record.Notes = row.GetProperty("notes").GetString()
                    Case MasterDataKind.Package
                        record.DailyRate = row.GetProperty("daily_price").GetDecimal()
                        For Each item In row.GetProperty("package_components").EnumerateArray()
                            If item.GetProperty("is_active").GetBoolean() Then record.Components.Add(New MasterPackageItem With {.TypeId = item.GetProperty("equipment_type_id").GetInt64(), .Quantity = item.GetProperty("quantity").GetInt32()})
                        Next
                    Case MasterDataKind.Customer
                        record.Phone = row.GetProperty("phone").GetString()
                        record.Address = row.GetProperty("address").GetString()
                End Select
                records.Add(record)
            Next
            Return New MasterRecordPage(records, response.Total.Value, offset)
        End Function

        Public Async Function SaveMasterAsync(requestId As Guid, record As MasterRecord, cancellationToken As CancellationToken) As Task(Of Long) Implements IRentalRepository.SaveMasterAsync
            If record Is Nothing Then Throw New ArgumentNullException(NameOf(record))
            If requestId = Guid.Empty Then Throw New ArgumentException("ID permintaan tidak valid.")
            record.Validate()
            Await RequireMasterAsync(record.Kind, cancellationToken).ConfigureAwait(False)
            Dim values As Object
            Select Case record.Kind
                Case MasterDataKind.Category : values = New With {.name = record.Name.Trim(), .is_active = record.IsActive}
                Case MasterDataKind.EquipmentType : values = New With {.name = record.Name.Trim(), .category_id = record.ParentId, .daily_rate = record.DailyRate, .replacement_value = record.ReplacementValue, .description = record.Description, .is_active = record.IsActive}
                Case MasterDataKind.EquipmentUnit : values = New With {.code = record.Code.Trim(), .equipment_type_id = record.ParentId, .state = record.State, .notes = record.Notes, .qc_confirmed = record.QcConfirmed}
                Case MasterDataKind.Package : values = New With {.name = record.Name.Trim(), .daily_price = record.DailyRate, .is_active = record.IsActive, .components = record.Components.Select(Function(item) New With {.type_id = item.TypeId, .quantity = item.Quantity}).ToArray()}
                Case MasterDataKind.Customer : values = New With {.name = record.Name.Trim(), .phone = record.Phone.Trim(), .address = record.Address, .is_active = record.IsActive}
                Case Else : Throw New ArgumentOutOfRangeException(NameOf(record.Kind))
            End Select
            Dim token = Await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Return Await gateway.RpcAsync(Of Long)("save_master_data", New With {.p_request_id = requestId, .p_entity = MasterResource(record.Kind), .p_id = record.Id, .p_version = record.RowVersion, .p_values = values}, token, cancellationToken).ConfigureAwait(False)
            Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is JsonException
                Throw New MasterSaveUncertainException(ex)
            Catch ex As SupabaseRequestException When CInt(ex.StatusCode) >= 500 OrElse ex.StatusCode = HttpStatusCode.RequestTimeout
                Throw New MasterSaveUncertainException(ex)
            End Try
        End Function
    End Class
End Namespace
