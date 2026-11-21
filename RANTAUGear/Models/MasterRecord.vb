Option Strict On
Option Explicit On
Option Infer On

Namespace Models
    Public Enum MasterDataKind
        Category
        EquipmentType
        EquipmentUnit
        Package
        Customer
    End Enum

    Public NotInheritable Class MasterPackageItem
        Public Property TypeId As Long
        Public Property Quantity As Integer
    End Class

    Public NotInheritable Class MasterRecord
        Public Property Kind As MasterDataKind
        Public Property Id As Long?
        Public Property RowVersion As Long
        Public Property Name As String = String.Empty
        Public Property ParentId As Long?
        Public Property ParentName As String = String.Empty
        Public Property Code As String = String.Empty
        Public Property DailyRate As Decimal
        Public Property ReplacementValue As Decimal
        Public Property Description As String = String.Empty
        Public Property IsActive As Boolean = True
        Public Property State As String = "ready"
        Public Property Notes As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property QcConfirmed As Boolean
        Public Property Components As New List(Of MasterPackageItem)()

        Public Function Copy() As MasterRecord
            Dim result = DirectCast(MemberwiseClone(), MasterRecord)
            result.Components = Components.Select(Function(item) New MasterPackageItem With {.TypeId = item.TypeId, .Quantity = item.Quantity}).ToList()
            Return result
        End Function

        Public Sub Validate()
            If Kind <> MasterDataKind.EquipmentUnit AndAlso String.IsNullOrWhiteSpace(Name) Then Throw New ArgumentException("Nama wajib diisi.")
            If Kind = MasterDataKind.EquipmentType Then
                If Not ParentId.HasValue OrElse ParentId.Value <= 0 Then Throw New ArgumentException("Pilih kategori alat.")
                If DailyRate <= 0 OrElse DailyRate <> Decimal.Truncate(DailyRate) OrElse ReplacementValue < 0 OrElse ReplacementValue <> Decimal.Truncate(ReplacementValue) Then Throw New ArgumentException("Tarif harus positif dan nilai penggantian tidak negatif, dalam rupiah bulat.")
            ElseIf Kind = MasterDataKind.EquipmentUnit Then
                If Not ParentId.HasValue OrElse ParentId.Value <= 0 OrElse String.IsNullOrWhiteSpace(Code) Then Throw New ArgumentException("Jenis alat dan kode unit wajib diisi.")
                If Not {"ready", "maintenance", "retired", "rented", "lost"}.Contains(State) Then Throw New ArgumentException("Kondisi unit tidak valid.")
            ElseIf Kind = MasterDataKind.Package Then
                If DailyRate <= 0 OrElse DailyRate <> Decimal.Truncate(DailyRate) Then Throw New ArgumentException("Harga paket harus positif dalam rupiah bulat.")
                If (IsActive AndAlso Components.Count = 0) OrElse Components.Any(Function(item) item.TypeId <= 0 OrElse item.Quantity <= 0) OrElse Components.Select(Function(item) item.TypeId).Distinct().Count() <> Components.Count Then Throw New ArgumentException("Paket aktif memerlukan isi alat tanpa duplikasi, dengan jumlah positif.")
            ElseIf Kind = MasterDataKind.Customer AndAlso String.IsNullOrWhiteSpace(Phone) Then
                Throw New ArgumentException("Nomor telepon pelanggan wajib diisi.")
            End If
            If Id.HasValue AndAlso RowVersion < 1 Then Throw New ArgumentException("Versi data tidak valid. Muat ulang daftar.")
        End Sub

        Public Overrides Function ToString() As String
            Return Name & If(IsActive, String.Empty, " (nonaktif)")
        End Function
    End Class
End Namespace
