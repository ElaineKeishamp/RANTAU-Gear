Option Strict On
Option Explicit On
Option Infer On

Imports System.Drawing

Namespace UI
    Public NotInheritable Class Theme
        Private Sub New()
        End Sub
        Public Shared ReadOnly Primary As Color = ColorTranslator.FromHtml("#1F5C45")
        Public Shared ReadOnly Accent As Color = ColorTranslator.FromHtml("#925315")
        Public Shared ReadOnly Sidebar As Color = ColorTranslator.FromHtml("#16251F")
        Public Shared ReadOnly Background As Color = ColorTranslator.FromHtml("#F5F4EF")
        Public Shared ReadOnly Card As Color = Color.White
        Public Shared ReadOnly Text As Color = ColorTranslator.FromHtml("#1E2A26")
        Public Shared ReadOnly Muted As Color = ColorTranslator.FromHtml("#58665F")
        Public Shared ReadOnly Border As Color = ColorTranslator.FromHtml("#E1E7E4")
        Public Shared ReadOnly Ready As Color = ColorTranslator.FromHtml("#237044")
        Public Shared ReadOnly Rented As Color = ColorTranslator.FromHtml("#865209")
        Public Shared ReadOnly Maintenance As Color = ColorTranslator.FromHtml("#AD3030")
        Public Shared ReadOnly UiFont As New Font("Segoe UI", 10.0F)
        Public Shared ReadOnly TitleFont As New Font("Segoe UI", 20.0F, FontStyle.Bold)
    End Class
End Namespace
