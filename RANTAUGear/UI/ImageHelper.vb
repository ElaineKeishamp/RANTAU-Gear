Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

Namespace UI
    Public NotInheritable Class ImageHelper
        Private Sub New()
        End Sub

        Private Shared ReadOnly s_assetsDir As String = Path.Combine(AppContext.BaseDirectory, "Assets")
        Private Shared ReadOnly s_supportedExts As String() = {".png", ".jpg", ".jpeg", ".bmp", ".gif"}

        Public Shared Function LoadFromAssets(fileName As String) As Image
            If String.IsNullOrWhiteSpace(fileName) Then Return Nothing
            Dim candidates As New List(Of String)() From {
                Path.Combine(s_assetsDir, fileName),
                Path.Combine(s_assetsDir, Path.GetFileNameWithoutExtension(fileName) & ".png"),
                Path.Combine(s_assetsDir, Path.GetFileNameWithoutExtension(fileName) & ".jpg"),
                Path.Combine(s_assetsDir, Path.GetFileNameWithoutExtension(fileName) & ".jpeg")
            }
            For Each path In candidates
                If File.Exists(path) Then
                    Try
                        Return LoadImageFromFile(path)
                    Catch
                    End Try
                End If
            Next
            Return Nothing
        End Function

        Public Shared Function FindImageForName(displayName As String) As String
            If String.IsNullOrWhiteSpace(displayName) Then Return Nothing
            If Not Directory.Exists(s_assetsDir) Then Return Nothing
            Dim normalizedTarget As String = NormalizeName(displayName)
            Dim allFiles As New List(Of String)()
            For Each ext In s_supportedExts
                allFiles.AddRange(Directory.GetFiles(s_assetsDir, "*" & ext, SearchOption.AllDirectories))
            Next
            For Each file In allFiles
                Dim fileNameOnly As String = Path.GetFileNameWithoutExtension(file)
                Dim normalizedFile As String = NormalizeName(fileNameOnly)
                If normalizedFile.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) Then
                    Return Path.GetRelativePath(s_assetsDir, file)
                End If
            Next
            For Each file In allFiles
                Dim fileNameOnly As String = Path.GetFileNameWithoutExtension(file)
                Dim normalizedFile As String = NormalizeName(fileNameOnly)
                If normalizedFile.Contains(normalizedTarget) OrElse normalizedTarget.Contains(normalizedFile) Then
                    Return Path.GetRelativePath(s_assetsDir, file)
                End If
            Next
            Return Nothing
        End Function

        Public Shared Function LoadImageForName(displayName As String, Optional size As Size? = Nothing) As Image
            Dim relPath As String = FindImageForName(displayName)
            If relPath IsNot Nothing Then
                Dim img As Image = LoadFromAssets(relPath)
                If img IsNot Nothing Then
                    If size.HasValue Then
                        Dim sz As Size = size.Value
                        Return ResizeImage(img, sz.Width, sz.Height)
                    End If
                    Return img
                End If
            End If
            Return CreatePlaceholder(size)
        End Function

        Public Shared Function CreatePlaceholder(Optional size As Size? = Nothing) As Image
            Dim w As Integer = If(size.HasValue AndAlso size.Value.Width > 0, size.Value.Width, 120)
            Dim h As Integer = If(size.HasValue AndAlso size.Value.Height > 0, size.Value.Height, 90)
            Dim bmp As New Bitmap(w, h)
            Using g As Graphics = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.Clear(Color.FromArgb(230, 230, 230))
                Using pen As New Pen(Color.FromArgb(180, 180, 180), 2)
                    g.DrawRectangle(pen, 0, 0, w - 1, h - 1)
                End Using
                Using brush As New SolidBrush(Color.FromArgb(120, 120, 120))
                    Using font As New Font("Segoe UI", 10, FontStyle.Bold)
                        Dim text As String = "No Image"
                        Dim textSize As SizeF = g.MeasureString(text, font)
                        g.DrawString(text, font, brush, CSng((w - textSize.Width) / 2), CSng((h - textSize.Height) / 2))
                    End Using
                End Using
            End Using
            Return bmp
        End Function

        Public Shared Function LoadBrandLogo(Optional width As Integer = 40, Optional height As Integer = 40) As Image
            Dim logoFile As String = FindImageForName("Logo Rantaugear")
            If logoFile IsNot Nothing Then
                Dim img As Image = LoadFromAssets(logoFile)
                If img IsNot Nothing Then
                    Return ResizeImage(img, width, height)
                End If
            End If
            Return Theme.DrawLogo(width, height)
        End Function

        Private Shared Function NormalizeName(name As String) As String
            If String.IsNullOrWhiteSpace(name) Then Return String.Empty
            Dim result As String = name.ToLowerInvariant()
            result = result.Replace(" ", String.Empty)
            result = result.Replace("-", String.Empty)
            result = result.Replace("_", String.Empty)
            result = result.Replace("'", String.Empty)
            Return result
        End Function

        Public Shared Function LoadImageFromFile(filePath As String) As Image
            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                Using ms As New MemoryStream(CType(fs.Length, Integer))
                    fs.CopyTo(ms)
                    ms.Position = 0
                    Using img As Image = Image.FromStream(ms)
                        Return CType(img.Clone(), Image)
                    End Using
                End Using
            End Using
        End Function

        Private Shared Function ResizeImage(source As Image, targetWidth As Integer, targetHeight As Integer) As Bitmap
            Dim destRect As New Rectangle(0, 0, targetWidth, targetHeight)
            Dim destImage As New Bitmap(targetWidth, targetHeight)
            destImage.SetResolution(source.HorizontalResolution, source.VerticalResolution)
            Using g As Graphics = Graphics.FromImage(destImage)
                g.CompositingMode = CompositingMode.SourceCopy
                g.CompositingQuality = CompositingQuality.HighQuality
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.SmoothingMode = SmoothingMode.HighQuality
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.DrawImage(source, destRect, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel)
            End Using
            Return destImage
        End Function
    End Class
End Namespace