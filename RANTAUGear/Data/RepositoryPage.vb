Option Strict On
Option Explicit On
Option Infer On

Imports System.Data

Namespace Data
    Public NotInheritable Class RepositoryPage
        Public ReadOnly Property Rows As DataTable
        Public ReadOnly Property TotalRows As Long
        Public ReadOnly Property Offset As Integer
        Public ReadOnly Property PageSize As Integer
        Public ReadOnly Property LoadedAt As DateTimeOffset
        Public ReadOnly Property Description As String

        Public Sub New(rows As DataTable, totalRows As Long, offset As Integer, pageSize As Integer, description As String)
            Me.Rows = rows
            Me.TotalRows = totalRows
            Me.Offset = offset
            Me.PageSize = pageSize
            Me.Description = description
            LoadedAt = DateTimeOffset.UtcNow
        End Sub
    End Class
End Namespace
