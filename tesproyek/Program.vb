Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Windows.Forms
Imports RantauGear.Forms

Friend Module Program
    <STAThread>
    Friend Sub Main()
        Application.EnableVisualStyles()
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.Run(New LoginForm())
    End Sub
End Module
