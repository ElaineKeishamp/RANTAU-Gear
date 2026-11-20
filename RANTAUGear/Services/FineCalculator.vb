Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports RantauGear.Models

Namespace Services
    Public Class FineCalculator
        Public Const GracePeriodMinutes As Integer = 30
        Public Function CalculateLateFine(rental As Rental, Optional currentTime As Nullable(Of DateTime) = Nothing) As Decimal
            Dim current As DateTime = If(currentTime.HasValue, currentTime.Value, If(rental.ReturnedAt.HasValue, rental.ReturnedAt.Value, DateTime.Now))
            If current <= rental.DueAt.AddMinutes(GracePeriodMinutes) Then Return 0D
            Dim lateHours As Integer = Math.Max(1, CInt(Math.Ceiling((current - rental.DueAt.AddMinutes(GracePeriodMinutes)).TotalHours)))
            Dim dailyTotal As Decimal = rental.Items.Sum(Function(item As RentalItem) item.DailyRate * item.Quantity)
            Return Math.Ceiling(dailyTotal * 0.1D * lateHours)
        End Function
        Public Function CalculateDamageFine(items As IEnumerable(Of RentalItem)) As Decimal
            Return items.Sum(Function(item As RentalItem) item.DamageFine)
        End Function
        Public Function CalculateTotalFine(rental As Rental, Optional currentTime As Nullable(Of DateTime) = Nothing) As Decimal
            Return CalculateLateFine(rental, currentTime) + CalculateDamageFine(rental.Items)
        End Function
        Public Function IsLate(rental As Rental, Optional currentTime As Nullable(Of DateTime) = Nothing) As Boolean
            Dim current As DateTime = If(currentTime.HasValue, currentTime.Value, DateTime.Now)
            Return current > rental.DueAt.AddMinutes(GracePeriodMinutes)
        End Function
    End Class
End Namespace
