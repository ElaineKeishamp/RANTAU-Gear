Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports RantauGear.Models

Namespace UI
    Public Class StatusBadge
        Inherits Label
        Public Sub New(status As UnitStatus)
            AutoSize = False
            TextAlign = ContentAlignment.MiddleCenter
            Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
            ForeColor = GetTextColor(status)
            Text = GetText(status)
            BackColor = GetColor(status)
            Width = 100
            Height = 28
            Margin = New Padding(0)
        End Sub

        Private Shared Function GetText(status As UnitStatus) As String
            If status = UnitStatus.Ready Then Return "Ready"
            If status = UnitStatus.Rented Then Return "Disewa"
            Return "Maintenance"
        End Function

        Private Shared Function GetColor(status As UnitStatus) As Color
            ' Warna latar (light/pastel)
            If status = UnitStatus.Ready Then Return Theme.ReadyLight
            If status = UnitStatus.Rented Then Return ColorTranslator.FromHtml("#FEE2E4")
            Return ColorTranslator.FromHtml("#FEE2E2")
        End Function

        Private Shared Function GetTextColor(status As UnitStatus) As Color
            ' Warna teks (dark/saturated)
            If status = UnitStatus.Ready Then Return Theme.ReadyText
            If status = UnitStatus.Rented Then Return ColorTranslator.FromHtml("#BE123C")
            Return ColorTranslator.FromHtml("#7F1D1D")
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias

            ' Draw rounded rectangle background
            Using path As GraphicsPath = UiHelpers.CreateRoundPath(ClientRectangle, 14)
                Using brush As New System.Drawing.SolidBrush(BackColor)
                    e.Graphics.FillPath(brush, path)
                End Using
            End Using

            ' Draw text
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        End Sub
    End Class
End Namespace
