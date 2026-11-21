Option Strict On
Option Explicit On
Option Infer On
Imports RantauGear.Models
Namespace Data
    Public NotInheritable Class MasterRecordPage
        Public ReadOnly Property Records As List(Of MasterRecord)
        Public ReadOnly Property Total As Long
        Public ReadOnly Property Offset As Integer
        Public Sub New(records As List(Of MasterRecord), total As Long, offset As Integer)
            Me.Records = records
            Me.Total = total
            Me.Offset = offset
        End Sub
    End Class
End Namespace
