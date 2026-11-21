Option Strict On
Option Explicit On
Option Infer On
Imports System.Text.Json
Imports System.Text.Json.Serialization
Namespace Models
    Public Class ReportQuery
        Public Property Kind As String = "cash"
        Public Property FromDate As DateTime
        Public Property ToDate As DateTime
        Public Property Status As String = "all"
        Public Function Copy() As ReportQuery
            Return DirectCast(MemberwiseClone(), ReportQuery)
        End Function
        Public Sub Validate()
            If Not {"cash", "rentals"}.Contains(Kind) OrElse ToDate.Date < FromDate.Date OrElse Not {"all", "reserved", "active", "late", "returned", "cancelled", "expired"}.Contains(Status) Then Throw New ArgumentException("Pilih jenis laporan, periode dan status yang valid.")
        End Sub
    End Class
    Public Class ReportSnapshot
        <JsonRequired, JsonPropertyName("kind")> Public Property Kind As String
        <JsonRequired, JsonPropertyName("total")> Public Property Total As Long
        <JsonRequired, JsonPropertyName("summary")> Public Property Summary As Dictionary(Of String, Decimal)
        <JsonRequired, JsonPropertyName("rows")> Public Property Rows As List(Of JsonElement)
        <JsonRequired, JsonPropertyName("snapshot_at")> Public Property SnapshotAt As DateTimeOffset
    End Class
    Public Class StaffRecord
        <JsonPropertyName("id")> Public Property Id As Guid
        <JsonPropertyName("display_name")> Public Property DisplayName As String = String.Empty
        <JsonPropertyName("email")> Public Property Email As String = String.Empty
        <JsonPropertyName("role")> Public Property Role As String = "front_office"
        <JsonPropertyName("is_active")> Public Property IsActive As Boolean = True
        <JsonPropertyName("row_version")> Public Property RowVersion As Long
        Public Function Copy() As StaffRecord
            Return DirectCast(MemberwiseClone(), StaffRecord)
        End Function
        Public Sub Validate(operation As String)
            If Not {"create", "update", "password"}.Contains(operation) Then Throw New ArgumentException("Tindakan akun tidak valid.")
            If operation <> "create" AndAlso Id = Guid.Empty Then Throw New ArgumentException("Pilih akun staf terlebih dahulu.")
            If operation = "password" Then Return
            If String.IsNullOrWhiteSpace(DisplayName) OrElse DisplayName.Length > 150 OrElse Not {"super_admin", "front_office", "warehouse_qc"}.Contains(Role) Then Throw New ArgumentException("Isi nama tampilan dan pilih peran staf.")
            If operation = "create" Then
                Dim address As Net.Mail.MailAddress = Nothing
                If Not Net.Mail.MailAddress.TryCreate(Email, address) OrElse address.Address <> Email OrElse Email.Length > 254 Then Throw New ArgumentException("Isi alamat email yang valid.")
            ElseIf RowVersion < 1 Then
                Throw New ArgumentException("Versi akun tidak valid. Muat ulang daftar staf.")
            End If
        End Sub
    End Class
    Public Class StaffPageResult
        <JsonRequired, JsonPropertyName("rows")> Public Property Rows As List(Of StaffRecord)
        <JsonRequired, JsonPropertyName("total")> Public Property Total As Long
    End Class
End Namespace
