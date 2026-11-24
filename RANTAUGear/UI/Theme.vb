Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Drawing.Drawing2D

Namespace UI
    Public NotInheritable Class Theme
        Private Sub New()
        End Sub
        ' Warna utama (hijau emerald modern)
        Public Shared ReadOnly Primary As Color = ColorTranslator.FromHtml("#227C51")
        Public Shared ReadOnly PrimaryDark As Color = ColorTranslator.FromHtml("#0E261C")
        Public Shared ReadOnly PrimaryLight As Color = ColorTranslator.FromHtml("#163829")

        ' Warna aksen dan latar belakang
        Public Shared ReadOnly Accent As Color = ColorTranslator.FromHtml("#925315")
        Public Shared ReadOnly Sidebar As Color = ColorTranslator.FromHtml("#0E261C")
        Public Shared ReadOnly Background As Color = ColorTranslator.FromHtml("#F1F5F3")
        Public Shared ReadOnly Card As Color = Color.White

        ' Warna teks dan border
        Public Shared ReadOnly Text As Color = ColorTranslator.FromHtml("#1E2A26")
        Public Shared ReadOnly Muted As Color = ColorTranslator.FromHtml("#58665F")
        Public Shared ReadOnly Border As Color = ColorTranslator.FromHtml("#E3E8E6")
        Public Shared ReadOnly BorderLight As Color = ColorTranslator.FromHtml("#EDF2EF")

        ' Warna status
        Public Shared ReadOnly Ready As Color = ColorTranslator.FromHtml("#237044")
        Public Shared ReadOnly ReadyLight As Color = ColorTranslator.FromHtml("#DCFCE7")
        Public Shared ReadOnly ReadyText As Color = ColorTranslator.FromHtml("#166534")
        Public Shared ReadOnly Rented As Color = ColorTranslator.FromHtml("#865209")
        Public Shared ReadOnly Maintenance As Color = ColorTranslator.FromHtml("#AD3030")

        ' Warna notifikasi
        Public Shared ReadOnly SuccessLight As Color = ColorTranslator.FromHtml("#ECFDF5")
        Public Shared ReadOnly SuccessAccent As Color = ColorTranslator.FromHtml("#10B981")

        ' Font
        Public Shared ReadOnly UiFont As New Font("Segoe UI", 10.0F)
        Public Shared ReadOnly TitleFont As New Font("Segoe UI", 20.0F, FontStyle.Bold)

        ''' <summary>
        ''' Gambar logo segitiga RANTAU Gear (tiga segitiga bersarang emerald).
        ''' </summary>
        Public Shared Function DrawLogo(width As Integer, height As Integer) As Bitmap
            Dim bitmap As New Bitmap(width, height)
            Using g As Graphics = Graphics.FromImage(bitmap)
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

                Dim color As Color = ColorTranslator.FromHtml("#50C878") ' Bright emerald
                g.Clear(Color.Transparent)

                Dim penWidth As Single = CSng(Math.Max(1.5F, width / 32))
                Dim pen As New Pen(color, penWidth) With {
                    .LineJoin = Drawing2D.LineJoin.Round,
                    .StartCap = Drawing2D.LineCap.Round,
                    .EndCap = Drawing2D.LineCap.Round
                }

                Dim centerX As Single = CSng(width / 2)
                Dim centerY As Single = CSng(height / 2)

                ' Segitiga luar (paling besar)
                Dim outerSize As Single = CSng(Math.Min(width, height) * 0.85F)
                DrawTrianglePath(g, pen, centerX, centerY, outerSize, 0)

                ' Segitiga tengah
                Dim midSize As Single = CSng(outerSize * 0.6F)
                DrawTrianglePath(g, pen, centerX, centerY, midSize, CSng(-outerSize * 0.15F))

                ' Segitiga dalam (paling kecil)
                Dim innerSize As Single = CSng(outerSize * 0.35F)
                DrawTrianglePath(g, pen, centerX, centerY, innerSize, CSng(-outerSize * 0.3F))

                pen.Dispose()
            End Using
            Return bitmap
        End Function

        Private Shared Sub DrawTrianglePath(g As Graphics, pen As Pen, centerX As Single, centerY As Single, size As Single, offsetY As Single)
            ' Segitiga pointing up (gunung)
            ' Top point
            Dim pt1 As New PointF(centerX, centerY - size / 2 + offsetY)
            ' Bottom-left
            Dim pt2 As New PointF(centerX - size * 0.6F, centerY + size / 2 + offsetY)
            ' Bottom-right
            Dim pt3 As New PointF(centerX + size * 0.6F, centerY + size / 2 + offsetY)

            Using path As New Drawing2D.GraphicsPath()
                path.AddLine(pt1, pt2)
                path.AddLine(pt2, pt3)
                path.AddLine(pt3, pt1)
                path.CloseFigure()
                g.DrawPath(pen, path)
            End Using
        End Sub
    End Class
End Namespace
