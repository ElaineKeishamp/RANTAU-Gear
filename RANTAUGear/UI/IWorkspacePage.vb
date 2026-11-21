Option Strict On
Option Explicit On
Option Infer On
Imports System.Threading.Tasks
Namespace UI
    Public Interface IWorkspacePage
        ReadOnly Property PendingLoad As Task
        Event SessionExpired As EventHandler
    End Interface
End Namespace
