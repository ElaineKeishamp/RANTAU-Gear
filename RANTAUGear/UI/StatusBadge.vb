Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Windows.Forms
Imports RantauGear.Models

Namespace UI
    Public Class StatusBadge
        Inherits Label
        Public Sub New(status As UnitStatus)
            AutoSize = False
            TextAlign = ContentAlignment.MiddleCenter
            Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
            ForeColor = Color.White
            Text = GetText(status)
            BackColor = GetColor(status)
            Width = 105
            Height = 26
        End Sub
        Private Shared Function GetText(status As UnitStatus) As String
            If status = UnitStatus.Ready Then Return "Ready"
            If status = UnitStatus.Rented Then Return "Disewa"
            Return "Maintenance"
        End Function
        Private Shared Function GetColor(status As UnitStatus) As Color
            If status = UnitStatus.Ready Then Return Theme.Ready
            If status = UnitStatus.Rented Then Return Theme.Rented
            Return Theme.Maintenance
        End Function
    End Class
End Namespace
