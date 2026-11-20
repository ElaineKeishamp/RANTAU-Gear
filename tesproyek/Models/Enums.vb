Option Strict On
Option Explicit On
Option Infer On

Namespace Models
    Public Enum UserRole
        SuperAdmin
        FrontOffice
        GudangQc
    End Enum

    Public Enum UnitStatus
        Ready
        Rented
        Maintenance
    End Enum

    Public Enum RentalStatus
        Draft
        Active
        Completed
        Late
        Cancelled
    End Enum
End Namespace
