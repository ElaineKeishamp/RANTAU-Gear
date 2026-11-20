Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.ComponentModel
Imports System.Windows.Forms

Namespace UI
    Public Class RoundedButton
        Inherits Button
        Private hovered As Boolean
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property CornerRadius As Integer = 6
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property NormalColor As Color = Theme.Primary
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property HoverColor As Color = Color.FromArgb(23, 71, 53)
        Public Sub New()
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            ForeColor = Color.White
            BackColor = NormalColor
            Font = Theme.UiFont
            Cursor = Cursors.Hand
            DoubleBuffered = True
            AddHandler MouseEnter, AddressOf ButtonMouseEnter
            AddHandler MouseLeave, AddressOf ButtonMouseLeave
        End Sub
        Private Sub ButtonMouseEnter(sender As Object, e As EventArgs)
            hovered = True
            Invalidate()
        End Sub
        Private Sub ButtonMouseLeave(sender As Object, e As EventArgs)
            hovered = False
            Invalidate()
        End Sub
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Using path As GraphicsPath = UiHelpers.CreateRoundPath(ClientRectangle, CornerRadius)
                Using brush As New SolidBrush(If(Not Enabled, Theme.Border, If(hovered OrElse (MouseButtons And MouseButtons.Left) <> 0, HoverColor, NormalColor)))
                    e.Graphics.FillPath(brush, path)
                End Using
            End Using
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, If(Enabled, ForeColor, Theme.Muted), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            If Focused AndAlso ShowFocusCues Then ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ForeColor, NormalColor)
        End Sub
    End Class
End Namespace
