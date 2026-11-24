Option Strict On
Option Explicit On
Option Infer On
Imports System.Globalization
Imports System.IO
Imports PdfSharp.Drawing
Imports PdfSharp.Pdf
Imports RantauGear.Models
Namespace Services
    ''' <summary>Membuat PDF nota/bukti sewa A4 dari data RentalReceipt (PDFsharp GDI, hanya Windows).</summary>
    Public NotInheritable Class ReceiptPdfService
        Private Sub New()
        End Sub
        Public Shared Function SuggestedFileName(receipt As RentalReceipt) As String
            Dim name = "Nota-" & receipt.Number
            For Each invalid In Path.GetInvalidFileNameChars()
                name = name.Replace(invalid, "_"c)
            Next
            Return name & ".pdf"
        End Function
        Public Shared Function FromPickup(details As PickupDetails, method As String, unitIds As IEnumerable(Of Long)) As RentalReceipt
            If details Is Nothing Then Throw New ArgumentNullException(NameOf(details))
            Dim chosen As New HashSet(Of Long)(If(unitIds, Enumerable.Empty(Of Long)()))
            Dim receipt As New RentalReceipt With {
                .Number = details.Number, .CustomerName = details.CustomerName, .PickupAt = details.PickupAt, .DueAt = details.DueAt,
                .RentAmount = details.RentAmount, .RentPaidBefore = details.RentPaid, .RentPaidNow = details.RentBalance,
                .DepositTotal = details.DepositPaid + details.DepositBalance, .Method = method}
            For Each line In details.Lines
                receipt.Lines.Add(New ReceiptLine With {.Name = line.Name, .Quantity = line.Quantity})
            Next
            For Each unit In details.Units
                If chosen.Contains(unit.Id) Then receipt.UnitCodes.Add(unit.Code)
            Next
            Return receipt
        End Function
        Public Shared Sub Save(receipt As RentalReceipt, path As String)
            If receipt Is Nothing Then Throw New ArgumentNullException(NameOf(receipt))
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("Lokasi file PDF belum dipilih.", NameOf(path))
            Using document As New PdfDocument()
                document.Info.Title = "Nota " & receipt.Number
                document.Info.Creator = "RANTAU Gear"
                Dim builder As New Builder(document, receipt)
                builder.Build()
                document.Save(path)
            End Using
        End Sub
        Private NotInheritable Class Builder
            Private Const PageHeight As Double = 842.0
            Private Const Margin As Double = 40.0
            Private Const ContentWidth As Double = 515.0
            Private Shared ReadOnly IdCulture As CultureInfo = CultureInfo.GetCultureInfo("id-ID")
            Private ReadOnly document As PdfDocument
            Private ReadOnly receipt As RentalReceipt
            Private ReadOnly regular As New XFont("Arial", 9.5, XFontStyleEx.Regular)
            Private ReadOnly bold As New XFont("Arial", 9.5, XFontStyleEx.Bold)
            Private ReadOnly small As New XFont("Arial", 8, XFontStyleEx.Regular)
            Private ReadOnly heading As New XFont("Arial", 11, XFontStyleEx.Bold)
            Private ReadOnly title As New XFont("Arial", 20, XFontStyleEx.Bold)
            Private ReadOnly green As XColor = XColor.FromArgb(31, 92, 69)
            Private ReadOnly grey As XColor = XColor.FromArgb(88, 102, 95)
            Private ReadOnly rule As XColor = XColor.FromArgb(200, 208, 204)
            Private gfx As XGraphics
            Private y As Double
            Public Sub New(document As PdfDocument, receipt As RentalReceipt)
                Me.document = document : Me.receipt = receipt
            End Sub
            Public Sub Build()
                Try
                    NewPage()
                    DrawHeader()
                    DrawInfo()
                    DrawDueBox()
                    DrawEquipment()
                    DrawCosts()
                    DrawNotes()
                    DrawSignatures()
                Finally
                    gfx?.Dispose()
                End Try
            End Sub
            Private Shared Function Rp(value As Decimal) As String
                Return "Rp " & value.ToString("N0", IdCulture)
            End Function
            Private Shared Function Wib(value As DateTimeOffset) As String
                Return value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) & " WIB"
            End Function
            Private Sub NewPage()
                gfx?.Dispose()
                Dim page = document.AddPage()
                page.Size = PdfSharp.PageSize.A4
                gfx = XGraphics.FromPdfPage(page)
                y = Margin
            End Sub
            Private Sub Ensure(height As Double)
                If y + height > PageHeight - Margin Then NewPage()
            End Sub
            Private Sub Write(text As String, font As XFont, x As Double, width As Double, color As XColor, Optional right As Boolean = False)
                Dim format = If(right, XStringFormats.TopRight, XStringFormats.TopLeft)
                gfx.DrawString(If(text, String.Empty), font, New XSolidBrush(color), New XRect(x, y, width, 16), format)
            End Sub
            Private Sub Line(color As XColor, Optional gap As Double = 4)
                gfx.DrawLine(New XPen(color, 0.75), Margin, y, Margin + ContentWidth, y)
                y += gap
            End Sub
            Private Function Wrap(text As String, font As XFont, width As Double) As List(Of String)
                Dim result As New List(Of String)()
                Dim current As String = String.Empty
                For Each word In If(text, String.Empty).Split(" "c)
                    Dim candidate = If(current.Length = 0, word, current & " " & word)
                    If current.Length = 0 OrElse gfx.MeasureString(candidate, font).Width <= width Then
                        current = candidate
                    Else
                        result.Add(current) : current = word
                    End If
                Next
                result.Add(current)
                Return result
            End Function
            Private Sub Heading1(text As String)
                Ensure(40)
                y += 10
                Write(text, heading, Margin, ContentWidth, green)
                y += 17
                Line(green, 6)
            End Sub
            Private Sub Pair(label As String, value As String, Optional emphasised As Boolean = False)
                Ensure(18)
                Write(label, regular, Margin, 140, grey)
                Write(value, If(emphasised, bold, regular), Margin + 150, ContentWidth - 150, XColors.Black)
                y += 16
            End Sub
            Private Sub DrawHeader()
                Write("RANTAU Gear", title, Margin, 300, green)
                Write("BUKTI SEWA", heading, Margin + 300, ContentWidth - 300, XColors.Black, True)
                y += 26
                Write("Penyewaan perlengkapan outdoor", regular, Margin, 300, grey)
                Write(receipt.Number, bold, Margin + 300, ContentWidth - 300, XColors.Black, True)
                y += 20
                Line(green, 10)
            End Sub
            Private Sub DrawInfo()
                Pair("Pelanggan", receipt.CustomerName)
                Pair("Jadwal ambil", Wib(receipt.PickupAt))
                y += 4
            End Sub
            Private Sub DrawDueBox()
                Ensure(46)
                gfx.DrawRectangle(New XPen(green, 1.25), New XSolidBrush(XColor.FromArgb(238, 245, 241)), Margin, y, ContentWidth, 38)
                y += 6
                Write("Batas pengembalian", regular, Margin + 12, 200, grey)
                y += 14
                Write(Wib(receipt.DueAt), heading, Margin + 12, ContentWidth - 24, green)
                y += 28
            End Sub
            Private Sub DrawEquipment()
                Heading1("Rincian alat")
                Ensure(20)
                Write("Alat", bold, Margin, 400, XColors.Black)
                Write("Jumlah", bold, Margin + 415, 100, XColors.Black, True)
                y += 15
                Line(rule)
                For Each item In receipt.Lines
                    Dim names = Wrap(item.Name, regular, 390)
                    Ensure(16 * names.Count + 4)
                    Write(names(0), regular, Margin, 400, XColors.Black)
                    Write(item.Quantity.ToString(CultureInfo.InvariantCulture), regular, Margin + 415, 100, XColors.Black, True)
                    y += 15
                    For index = 1 To names.Count - 1
                        Write(names(index), regular, Margin, 400, XColors.Black)
                        y += 15
                    Next
                Next
                Line(rule, 6)
                If receipt.UnitCodes.Count > 0 Then
                    For Each row In Wrap("Unit diserahkan: " & String.Join(", ", receipt.UnitCodes), small, ContentWidth)
                        Ensure(13)
                        Write(row, small, Margin, ContentWidth, grey)
                        y += 12
                    Next
                End If
            End Sub
            Private Sub DrawCosts()
                Heading1("Biaya dan deposit")
                Pair("Nilai sewa", Rp(receipt.RentAmount), True)
                If receipt.RentPaidBefore > 0 Then Pair("DP / dibayar sebelumnya", Rp(receipt.RentPaidBefore))
                If receipt.RentPaidNow > 0 Then Pair("Pelunasan saat ambil", Rp(receipt.RentPaidNow))
                Pair("Deposit diterima", Rp(receipt.DepositTotal), True)
                If Not String.IsNullOrWhiteSpace(receipt.Method) Then Pair("Metode (saat ambil)", ReportFiles.Caption(receipt.Method))
            End Sub
            Private Sub DrawNotes()
                y += 6
                For Each row In Wrap("Deposit bukan biaya sewa. Deposit dikembalikan setelah alat diperiksa; denda (bila ada) dipotong dari deposit.", small, ContentWidth)
                    Ensure(13)
                    Write(row, small, Margin, ContentWidth, grey)
                    y += 12
                Next
            End Sub
            Private Sub DrawSignatures()
                Ensure(130)
                y += 30
                Dim top = y + 50
                gfx.DrawLine(New XPen(grey, 0.75), Margin, top, Margin + 190, top)
                gfx.DrawLine(New XPen(grey, 0.75), Margin + ContentWidth - 190, top, Margin + ContentWidth, top)
                y = top + 4
                Write("Penyewa", regular, Margin, 190, grey)
                Write("Petugas", regular, Margin + ContentWidth - 190, 190, grey)
                y += 24
                Write("Dicetak " & Wib(DateTimeOffset.UtcNow), small, Margin, ContentWidth, grey)
            End Sub
        End Class
    End Class
End Namespace
