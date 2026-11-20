Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.ComponentModel
Imports System.Windows.Forms

Namespace UI
    Public Class RoundedPanel
        Inherits Panel
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property CornerRadius As Integer = 14
        Public Sub New()
            DoubleBuffered = True
            BackColor = Theme.Card
        End Sub
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Using path As GraphicsPath = UiHelpers.CreateRoundPath(ClientRectangle, CornerRadius)
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                Using brush As New SolidBrush(BackColor)
                    e.Graphics.FillPath(brush, path)
                End Using
                Using pen As New Pen(Theme.Border)
                    e.Graphics.DrawPath(pen, path)
                End Using
            End Using
            MyBase.OnPaint(e)
        End Sub
    End Class
End Namespace
