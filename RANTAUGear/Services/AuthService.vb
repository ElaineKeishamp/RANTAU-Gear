Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Linq
Imports RantauGear.Data
Imports RantauGear.Models

Namespace Services
    Public Class AuthService
        Private ReadOnly repository As IRentalRepository
        Public Sub New(repository As IRentalRepository)
            Me.repository = repository
        End Sub
        Public Function Login(username As String, password As String) As AppUser
            Return repository.GetUsers().FirstOrDefault(Function(user As AppUser) user.IsActive AndAlso String.Equals(user.Username, username.Trim(), StringComparison.OrdinalIgnoreCase) AndAlso user.Password = password)
        End Function
        Public Function CanAccess(user As AppUser, menuKey As String) As Boolean
            If user Is Nothing OrElse Not user.IsActive Then Return False
            If user.Role = UserRole.SuperAdmin Then Return True
            If user.Role = UserRole.FrontOffice Then Return menuKey = "dashboard" OrElse menuKey = "booking" OrElse menuKey = "reports"
            If user.Role = UserRole.GudangQc Then Return menuKey = "dashboard" OrElse menuKey = "return-qc"
            Return False
        End Function
    End Class
End Namespace
