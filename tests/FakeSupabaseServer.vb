Option Strict On
Option Explicit On
Option Infer On

Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Friend NotInheritable Class FakeSupabaseServer
    Inherits HttpMessageHandler

    Public Shared ReadOnly StaffId As Guid = Guid.Parse("11111111-1111-1111-1111-111111111111")
    Public Property StaffRole As String = "super_admin"
    Public Property StaffName As String = "Administrator uji"
    Public Property Active As Boolean = True
    Public Property HasProfile As Boolean = True
    Public Property Offline As Boolean
    Public Property DelayReads As Boolean
    Public Property InvalidRefresh As Boolean
    Public Property RejectReadOnce As Boolean
    Public Property RejectEveryRead As Boolean
    Public Property EmptyData As Boolean
    Public Property LastReadPath As String = String.Empty
    Public Property RefreshCount As Integer
    Public Property LogoutCount As Integer
    Private access As String = "ACCESS-1"
    Public Property DropSaveReplyOnce As Boolean
    Public Property ConflictNextSave As Boolean
    Public Property SaveCount As Integer
    Public ReadOnly SaveRequestIds As New List(Of Guid)()
    Private ReadOnly masters As New Dictionary(Of String, List(Of Dictionary(Of String, Object)))()
    Private ReadOnly savedRequests As New Dictionary(Of Guid, (Body As String, Id As Long))()
    Private nextId As Long = 100
    Public Sub New()
        masters("categories") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 10L}, {"row_version", 1L}, {"name", "Kategori uji"}, {"is_active", True}}}
        masters("equipment_types") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 20L}, {"row_version", 1L}, {"category_id", 10L}, {"name", "reserved"}, {"daily_rate", 100000D}, {"replacement_value", 1000000D}, {"description", ""}, {"is_active", True}, {"categories", New With {.name = "Kategori uji"}}}}
        masters("equipment_units") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 30L}, {"row_version", 1L}, {"equipment_type_id", 20L}, {"code", "TND-TEST"}, {"state", "ready"}, {"notes", ""}, {"equipment_types", New With {.name = "reserved", .daily_rate = 100000D}}}}
        masters("packages") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 40L}, {"row_version", 1L}, {"name", "Paket uji"}, {"daily_price", 80000D}, {"is_active", True}, {"package_components", New Object() {New With {.equipment_type_id = 20L, .quantity = 1, .is_active = True}}}}}
        masters("customers") = New List(Of Dictionary(Of String, Object)) From {New Dictionary(Of String, Object) From {{"id", 50L}, {"row_version", 1L}, {"name", "Pelanggan uji"}, {"phone", "080000"}, {"address", "Alamat uji"}, {"is_active", True}}}
    End Sub
    Public Function MasterCount(entity As String) As Integer
        Return masters(entity).Count
    End Function
    Private Async Function SaveMaster(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        SaveCount += 1
        Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
            Dim body = document.RootElement
            Dim entity = body.GetProperty("p_entity").GetString()
            If StaffRole <> "super_admin" AndAlso Not (StaffRole = "front_office" AndAlso entity = "customers") Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
            Dim requestId = body.GetProperty("p_request_id").GetGuid()
            SaveRequestIds.Add(requestId)
            If savedRequests.ContainsKey(requestId) Then
                If savedRequests(requestId).Body <> body.GetRawText() Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_REQUEST"})
                Return Reply(HttpStatusCode.OK, savedRequests(requestId).Id)
            End If
            If ConflictNextSave Then
                ConflictNextSave = False
                Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_VERSION"})
            End If
            Dim row As Dictionary(Of String, Object)
            If body.GetProperty("p_id").ValueKind = JsonValueKind.Null Then
                nextId += 1
                row = New Dictionary(Of String, Object) From {{"id", nextId}, {"row_version", 1L}}
                masters(entity).Add(row)
            Else
                Dim id = body.GetProperty("p_id").GetInt64()
                row = masters(entity).Single(Function(item) CLng(item("id")) = id)
                If CLng(row("row_version")) <> body.GetProperty("p_version").GetInt64() Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_MASTER_VERSION"})
                row("row_version") = CLng(row("row_version")) + 1
            End If
            For Each field In body.GetProperty("p_values").EnumerateObject()
                row(field.Name) = field.Value.Clone()
            Next
            If entity = "equipment_types" Then row("categories") = New With {.name = "Kategori uji"}
            If entity = "equipment_units" Then row("equipment_types") = New With {.name = "reserved", .daily_rate = 100000D}
            If entity = "packages" Then row("package_components") = body.GetProperty("p_values").GetProperty("components").EnumerateArray().Select(Function(item) New With {.equipment_type_id = item.GetProperty("type_id").GetInt64(), .quantity = item.GetProperty("quantity").GetInt32(), .is_active = True}).ToArray()
            Dim result = CLng(row("id"))
            savedRequests(requestId) = (body.GetRawText(), result)
            If DropSaveReplyOnce Then
                DropSaveReplyOnce = False
                Throw New HttpRequestException("Simulated lost reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, result)
        End Using
    End Function

    Private Shared Function Reply(status As HttpStatusCode, value As Object, Optional total As Long? = Nothing, Optional offset As Integer = 0, Optional count As Integer = 0) As HttpResponseMessage
        Dim response As New HttpResponseMessage(status) With {.Content = New StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")}
        If total.HasValue Then response.Content.Headers.TryAddWithoutValidation("Content-Range", If(count = 0, "*", offset.ToString() & "-" & (offset + count - 1).ToString()) & "/" & total.Value.ToString())
        Return response
    End Function

    Private Function SessionReply() As HttpResponseMessage
        Return Reply(HttpStatusCode.OK, New With {.access_token = access, .refresh_token = "REFRESH-" & (RefreshCount + 1).ToString(), .expires_in = 3600})
    End Function


    Public Property ObjectReservationReply As Boolean
    Public Property ReservationCount As Integer
    Public Property DpCount As Integer
    Public Property DropReservationReplyOnce As Boolean
    Public Property RejectBookingStock As Boolean
    Public Property ChangeBookingPrice As Boolean
    Public ReadOnly ReservationIds As New List(Of String)()
    Private ReadOnly reservationReplies As New Dictionary(Of String, (Body As String, Receipt As Object))()
    Private Async Function BookingRpc(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
        If StaffRole <> "super_admin" AndAlso StaffRole <> "front_office" Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
        Using document = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(ct).ConfigureAwait(False))
            Dim body = document.RootElement
            If request.RequestUri.AbsolutePath.EndsWith("preview_reservation", StringComparison.Ordinal) Then
                If RejectBookingStock Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_STOCK"})
                Dim days = Math.Max(1, CInt(Math.Ceiling((DateTimeOffset.Parse(body.GetProperty("p_due_at").GetString()) - DateTimeOffset.Parse(body.GetProperty("p_pickup_at").GetString())).TotalDays)))
                Dim daily = If(body.GetProperty("p_package_id").ValueKind = JsonValueKind.Null, body.GetProperty("p_lines").EnumerateArray().Sum(Function(x) x.GetProperty("quantity").GetInt32()) * 100000D, 50000D)
                Return Reply(HttpStatusCode.OK, New With {.daily_price = daily, .rental_days = days, .rent_amount = daily * days, .down_payment_due = Math.Ceiling(daily * days / 4D), .deposit_due = daily * days, .pickup_deadline_at = DateTimeOffset.Parse(body.GetProperty("p_pickup_at").GetString()).AddHours(12)})
            End If
            Dim id = body.GetProperty("p_request_id").GetString()
            ReservationIds.Add(id)
            If reservationReplies.ContainsKey(id) Then
                If reservationReplies(id).Body <> body.GetRawText() Then Throw New InvalidOperationException("Retry changed payment payload")
                Return Reply(HttpStatusCode.OK, reservationReplies(id).Receipt)
            End If
            If RejectBookingStock Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_STOCK"})
            If ChangeBookingPrice Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_BOOKING_CHANGED"})
            ReservationCount += 1 : DpCount += 1
            Dim receipt = New With {.id = 900L + ReservationCount, .rental_number = "UJI-RESERVASI-" & ReservationCount.ToString(), .rent_amount = body.GetProperty("p_expected_rent").GetDecimal(), .down_payment_due = body.GetProperty("p_expected_dp").GetDecimal(), .deposit_due = body.GetProperty("p_expected_deposit").GetDecimal()}
            Dim payload As Object = If(ObjectReservationReply, DirectCast(receipt, Object), New Object() {receipt})
            reservationReplies(id) = (body.GetRawText(), payload)
            If DropReservationReplyOnce Then
                DropReservationReplyOnce = False
                Throw New HttpRequestException("Lost reservation reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, payload)
        End Using
    End Function


    Public Property PickedUpCount As Integer
    Public Property PickupRentReceived As Decimal
    Public Property PickupDepositReceived As Decimal
    Public Property LastPickupRentalId As Long
    Public Property DropPickupReplyOnce As Boolean
    Public Property RejectPickupUnit As Boolean
    Public Property PickupWindowClosed As Boolean
    Public ReadOnly PickupIds As New List(Of String)()
    Private ReadOnly pickupReplies As New Dictionary(Of String, (Body As String, Reply As Object))()
    Private ReadOnly activePickups As New HashSet(Of Long)()
    Private Async Function PickupRpc(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
        If StaffRole <> "super_admin" AndAlso StaffRole <> "front_office" Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
        Using doc = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(ct).ConfigureAwait(False))
            Dim body = doc.RootElement
            Dim rentalId = body.GetProperty("p_rental_id").GetInt64()
            If request.RequestUri.AbsolutePath.EndsWith("get_pickup_details", StringComparison.Ordinal) Then
                If PickupWindowClosed Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_PICKUP_WINDOW"})
                If activePickups.Contains(rentalId) Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_PICKUP_STATE"})
                Return Reply(HttpStatusCode.OK, New With {.id = rentalId, .rental_number = "UJI-PICKUP-" & rentalId.ToString(), .customer_name = "Pelanggan uji", .pickup_at = "2026-11-21T01:00:00Z", .due_at = "2026-11-22T01:00:00Z", .deadline = "2026-11-21T13:00:00Z", .rent_amount = 100000D, .rent_paid = 25000D, .deposit_paid = 0D, .rent_balance = 75000D, .deposit_balance = 100000D, .lines = New Object() {New With {.type_id = 20L, .name = "Tenda uji", .quantity = 1}}, .units = New Object() {New With {.id = 30L, .type_id = 20L, .code = "TND-UJI-01"}, New With {.id = 31L, .type_id = 20L, .code = "TND-UJI-02"}}})
            End If
            Dim id = body.GetProperty("p_request_id").GetString()
            PickupIds.Add(id)
            If pickupReplies.ContainsKey(id) Then
                If pickupReplies(id).Body <> body.GetRawText() Then Throw New InvalidOperationException("Pickup retry changed payload")
                Return Reply(HttpStatusCode.OK, pickupReplies(id).Reply)
            End If
            If RejectPickupUnit Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "Semua unit harus siap, lolos QC, dan belum dipinjam."})
            If PickupWindowClosed Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_PICKUP_WINDOW"})
            If activePickups.Contains(rentalId) Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_PICKUP_STATE"})
            If body.GetProperty("p_unit_ids").GetArrayLength() <> 1 Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "Jenis dan jumlah unit harus sama dengan reservasi."})
            PickedUpCount += 1 : PickupRentReceived += 75000D : PickupDepositReceived += 100000D
            LastPickupRentalId = rentalId : activePickups.Add(rentalId)
            Dim result As Object = New Object() {New With {.id = rentalId, .rental_number = "UJI-PICKUP-" & rentalId.ToString(), .state = "active", .rent_amount = 100000D, .down_payment_due = 25000D, .deposit_due = 100000D}}
            pickupReplies(id) = (body.GetRawText(), result)
            If DropPickupReplyOnce Then
                DropPickupReplyOnce = False : Throw New HttpRequestException("Lost pickup reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, result)
        End Using
    End Function


    Public Property ReturnFinancialIncomplete As Boolean
    Public Property ReturnFinancialUnbalanced As Boolean
    Public Property ReturnState As String = "active"
    Public Property ReturnApproved As Boolean
    Public Property ReturnSettled As Boolean
    Public Property ReturnLateCharge As Decimal = 25000D
    Public Property ReturnProposedCharge As Decimal
    Public Property ReturnDeposit As Decimal = 100000D
    Public Property InspectionsCount As Integer
    Public Property ApprovalsCount As Integer
    Public Property SettlementsCount As Integer
    Public Property RefundPaid As Decimal
    Public Property AdditionalReceived As Decimal
    Public Property DepositDeducted As Decimal
    Public Property DropReturnReplyOnce As Boolean
    Public Property RejectReturnAction As Boolean
    Public ReadOnly ReturnRequestIds As New List(Of String)()
    Private ReadOnly returnReplies As New Dictionary(Of String, (Body As String, Rpc As String, Reply As Object))()
    Private returnUnitChecks As JsonElement?
    Private Async Function ReturnRpc(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
        Using doc = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(ct).ConfigureAwait(False))
            Dim body = doc.RootElement
            Dim id = body.GetProperty("p_rental_id").GetInt64()
            Dim rpc = request.RequestUri.AbsolutePath.Split("/"c).Last()
            If rpc = "get_return_details" Then
                Dim units As New List(Of Object)()
                For index = 0 To 1
                    Dim check As JsonElement? = Nothing
                    If returnUnitChecks.HasValue Then
                        check = returnUnitChecks.Value.EnumerateArray().FirstOrDefault(Function(x) x.GetProperty("unit_id").GetInt64() = 30L + index)
                    End If
                    units.Add(New With {.unit_id = 30L + index, .code = "TND-QA-" & index.ToString(), .name = "Tenda uji", .replacement_value = 200000D, .condition = If(check.HasValue, check.GetValueOrDefault().GetProperty("condition").GetString(), Nothing), .charge = If(check.HasValue, check.GetValueOrDefault().GetProperty("charge").GetDecimal(), 0D), .notes = If(check.HasValue, check.GetValueOrDefault().GetProperty("notes").GetString(), "")})
                Next
                Dim result As New Dictionary(Of String, Object) From {{"id", id}, {"rental_number", "UJI-RETURN-" & id.ToString()}, {"state", ReturnState}, {"due_at", "2026-11-21T00:00:00Z"}, {"returned_at", If(ReturnState = "returned", "2026-11-21T02:00:00Z", Nothing)}, {"approved", ReturnApproved}, {"settled", ReturnSettled}, {"units", units}}
                If StaffRole <> "warehouse_qc" Then
                    Dim total = ReturnLateCharge + ReturnProposedCharge
                    result("financial") = New With {.deposit = ReturnDeposit, .proposed_charge = ReturnProposedCharge, .late_charge = ReturnLateCharge, .total_charges = total, .deposit_applied = Math.Min(ReturnDeposit, total), .refund = Math.Max(0, ReturnDeposit - total) + If(ReturnFinancialUnbalanced, 1D, 0D), .additional = Math.Max(0, total - ReturnDeposit)}
                End If
                If ReturnFinancialIncomplete Then result("financial") = New With {.deposit = 100000D}
                Return Reply(HttpStatusCode.OK, result)
            End If
            Dim allowed = StaffRole = "super_admin" OrElse (rpc = "inspect_return" AndAlso StaffRole = "warehouse_qc") OrElse (rpc = "settle_return" AndAlso StaffRole = "front_office")
            If Not allowed Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
            Dim uuid = body.GetProperty("p_request_id").GetString()
            ReturnRequestIds.Add(uuid)
            If returnReplies.ContainsKey(uuid) Then
                If returnReplies(uuid).Rpc <> rpc OrElse returnReplies(uuid).Body <> body.GetRawText() Then Throw New InvalidOperationException("Return retry changed payload")
                Return Reply(HttpStatusCode.OK, returnReplies(uuid).Reply)
            End If
            If RejectReturnAction Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_RETURN_STATE"})
            Select Case rpc
                Case "inspect_return"
                    If ReturnState <> "active" Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_RETURN_STATE"})
                    Dim checks = body.GetProperty("p_checks")
                    If checks.GetArrayLength() <> 2 Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_RETURN_CHECKS"})
                    returnUnitChecks = checks.Clone() : ReturnProposedCharge = checks.EnumerateArray().Sum(Function(x) x.GetProperty("charge").GetDecimal())
                    ReturnState = "returned" : InspectionsCount += 1
                Case "approve_return_charges"
                    If ReturnState <> "returned" OrElse ReturnApproved Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_RETURN_STATE"})
                    ReturnApproved = True : ApprovalsCount += 1
                Case "settle_return"
                    If ReturnState <> "returned" OrElse Not ReturnApproved OrElse ReturnSettled Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_RETURN_STATE"})
                    ReturnSettled = True : SettlementsCount += 1
                    Dim total = ReturnLateCharge + ReturnProposedCharge
                    DepositDeducted += Math.Min(ReturnDeposit, total) : RefundPaid += Math.Max(0, ReturnDeposit - total) : AdditionalReceived += Math.Max(0, total - ReturnDeposit)
                Case Else : Throw New InvalidOperationException("Unknown return RPC")
            End Select
            Dim receipt As Object = New Object() {New With {.id = id, .rental_number = "UJI-RETURN-" & id.ToString(), .state = "returned"}}
            returnReplies(uuid) = (body.GetRawText(), rpc, receipt)
            If DropReturnReplyOnce Then
                DropReturnReplyOnce = False : Throw New HttpRequestException("Lost return action reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, receipt)
        End Using
    End Function


    Public Property ChangeState As String = "active"
    Public Property ChangeDeadlinePassed As Boolean
    Public Property ChangeLate As Boolean
    Public Property ChangeConflict As Boolean
    Public Property DropChangeReplyOnce As Boolean
    Public Property RejectChangeCode As String
    Public Property ChangePaymentCount As Integer
    Public Property ChangePaid As Decimal
    Public Property ChangeCancelCount As Integer
    Public Property ChangeExpireCount As Integer
    Public Property ChangeDue As DateTimeOffset = DateTimeOffset.Parse("2027-01-02T10:00:00+07:00")
    Private ReadOnly changePickup As DateTimeOffset = DateTimeOffset.Parse("2027-01-01T10:00:00+07:00")
    Private changeRent As Decimal = 100000D
    Public ReadOnly ChangeRequestIds As New List(Of String)()
    Private ReadOnly changeReplies As New Dictionary(Of String, (Body As String, Reply As Object))()
    Private ReadOnly changeStates As New Dictionary(Of Long, String)()
    Private Async Function ChangeRpc(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
        If StaffRole <> "super_admin" AndAlso StaffRole <> "front_office" Then Return Reply(HttpStatusCode.Forbidden, New With {.code = "42501"})
        Using doc = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(ct).ConfigureAwait(False))
            Dim body = doc.RootElement
            Dim id = body.GetProperty("p_rental_id").GetInt64()
            Dim rpc = request.RequestUri.AbsolutePath.Split("/"c).Last()
            If rpc = "get_booking_change_details" Then
                Dim deadline = changePickup.AddHours(12)
                Return Reply(HttpStatusCode.OK, New With {.id = id, .rental_number = "UJI-CHANGE-" & id.ToString(), .customer_name = "Pelanggan uji", .state = ChangeState, .pickup_at = changePickup, .due_at = ChangeDue, .deadline = deadline, .server_now = If(ChangeState = "reserved", If(ChangeDeadlinePassed, deadline.AddMinutes(1), deadline.AddHours(-1)), If(ChangeLate, ChangeDue.AddMinutes(1), ChangeDue.AddHours(-1))), .rent_amount = changeRent, .daily_price = 100000D, .dp_received = 25000D, .deposit_received = If(ChangeState = "active", 100000D, 0D)})
            End If
            If rpc = "preview_extension" Then
                If ChangeConflict Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_STOCK"})
                If ChangeLate Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_LATE"})
                Dim newDue = DateTimeOffset.Parse(body.GetProperty("p_due_at").GetString())
                If newDue <= ChangeDue Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_DUE"})
                Dim days = CInt(Math.Ceiling((newDue - changePickup).TotalDays))
                Return Reply(HttpStatusCode.OK, New With {.old_due = ChangeDue, .new_due = newDue, .days = days, .rent_amount = days * 100000D, .additional = days * 100000D - changeRent, .deposit = 100000D})
            End If
            Dim uuid = body.GetProperty("p_request_id").GetString()
            ChangeRequestIds.Add(uuid)
            If changeReplies.ContainsKey(uuid) Then
                If changeReplies(uuid).Body <> body.GetRawText() Then Throw New InvalidOperationException("Change retry altered payload")
                Return Reply(HttpStatusCode.OK, changeReplies(uuid).Reply)
            End If
            If RejectChangeCode IsNot Nothing OrElse ChangeConflict Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = If(RejectChangeCode, "RG_CHANGE_STOCK")})
            Select Case body.GetProperty("p_action").GetString()
                Case "extend"
                    If ChangeState <> "active" Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_STATE"})
                    If DateTimeOffset.Parse(body.GetProperty("p_expected_due").GetString()) <> ChangeDue Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_QUOTE"})
                    Dim extra = body.GetProperty("p_expected_additional").GetDecimal()
                    If extra > 0 Then ChangePaymentCount += 1
                    ChangePaid += extra : changeRent += extra
                    ChangeDue = DateTimeOffset.Parse(body.GetProperty("p_due_at").GetString())
                Case "cancel"
                    If ChangeState <> "reserved" Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_STATE"})
                    If String.IsNullOrWhiteSpace(body.GetProperty("p_reason").GetString()) Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_REASON"})
                    ChangeState = "cancelled" : ChangeCancelCount += 1
                Case "expire"
                    If ChangeState <> "reserved" OrElse Not ChangeDeadlinePassed Then Return Reply(HttpStatusCode.BadRequest, New With {.code = "P0001", .message = "RG_CHANGE_NOT_EXPIRED"})
                    ChangeState = "expired" : ChangeExpireCount += 1
            End Select
            changeStates(id) = ChangeState
            Dim result As Object = New Object() {New With {.id = id, .rental_number = "UJI-CHANGE-" & id.ToString(), .state = ChangeState}}
            changeReplies(uuid) = (body.GetRawText(), result)
            If DropChangeReplyOnce Then
                DropChangeReplyOnce = False : Throw New HttpRequestException("Lost change reply after commit")
            End If
            Return Reply(HttpStatusCode.OK, result)
        End Using
    End Function

    Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        If Offline Then Throw New HttpRequestException("Simulated offline fixture")
        Dim path = request.RequestUri.AbsolutePath
        If path = "/auth/v1/token" Then
            If request.RequestUri.Query.Contains("refresh_token") Then
                RefreshCount += 1
                Await Task.Delay(40, cancellationToken).ConfigureAwait(False)
                If InvalidRefresh Then Return Reply(HttpStatusCode.BadRequest, New With {.error = "refresh_expired"})
                access = "ACCESS-" & (RefreshCount + 1).ToString()
                Return SessionReply()
            End If
            Using body = JsonDocument.Parse(Await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False))
                If body.RootElement.GetProperty("email").GetString() <> "staff@example.test" OrElse body.RootElement.GetProperty("password").GetString() <> "TEST_PASSWORD" Then Return Reply(HttpStatusCode.BadRequest, New With {.error = "invalid_credentials"})
            End Using
            Return SessionReply()
        End If
        If request.Headers.Authorization Is Nothing OrElse request.Headers.Authorization.Parameter <> access Then Return Reply(HttpStatusCode.Unauthorized, New With {.error = "expired"})
        If path = "/auth/v1/user" Then Return Reply(HttpStatusCode.OK, New With {.id = StaffId})
        If path = "/auth/v1/logout" Then
            LogoutCount += 1
            access = String.Empty
            Return New HttpResponseMessage(HttpStatusCode.NoContent)
        End If
        If {"/rest/v1/rpc/get_booking_change_details", "/rest/v1/rpc/preview_extension", "/rest/v1/rpc/change_booking"}.Contains(path) Then Return Await ChangeRpc(request, cancellationToken).ConfigureAwait(False)
        If {"/rest/v1/rpc/get_return_details", "/rest/v1/rpc/inspect_return", "/rest/v1/rpc/approve_return_charges", "/rest/v1/rpc/settle_return"}.Contains(path) Then Return Await ReturnRpc(request, cancellationToken).ConfigureAwait(False)
        If path = "/rest/v1/rpc/get_pickup_details" OrElse path = "/rest/v1/rpc/pickup_reservation" Then Return Await PickupRpc(request, cancellationToken).ConfigureAwait(False)
        If path = "/rest/v1/rpc/preview_reservation" OrElse path = "/rest/v1/rpc/reserve_with_dp" Then Return Await BookingRpc(request, cancellationToken).ConfigureAwait(False)
        If path = "/rest/v1/rpc/save_master_data" Then Return Await SaveMaster(request, cancellationToken).ConfigureAwait(False)
        If path.StartsWith("/rest/v1/", StringComparison.Ordinal) Then
            If DelayReads Then Await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(False)
            LastReadPath = request.RequestUri.PathAndQuery
            If RejectReadOnce OrElse RejectEveryRead Then
                RejectReadOnce = False
                Return Reply(HttpStatusCode.Unauthorized, New With {.error = "expired"})
            End If
            Dim query = request.RequestUri.Query.TrimStart("?"c).Split("&"c).Select(Function(part) part.Split("="c, 2)).ToDictionary(Function(parts) parts(0), Function(parts) Uri.UnescapeDataString(parts(1)))
            Dim offset = Integer.Parse(query("offset"), Globalization.CultureInfo.InvariantCulture)
            Dim limit = Integer.Parse(query("limit"), Globalization.CultureInfo.InvariantCulture)
            Dim rows As New List(Of Object)()
            Select Case path
                Case "/rest/v1/staff_profiles"
                    If HasProfile Then rows.Add(New With {.id = StaffId, .display_name = StaffName, .role = StaffRole, .is_active = Active})
                Case "/rest/v1/categories", "/rest/v1/equipment_types", "/rest/v1/equipment_units", "/rest/v1/packages", "/rest/v1/customers"
                    rows.AddRange(masters(path.Substring("/rest/v1/".Length)).Cast(Of Object)())
                Case "/rest/v1/return_register"
                    For id = 1 To 61
                        rows.Add(New With {.id = id, .rental_number = "UJI-RETURN-" & id.ToString("D3"), .due_at = "2026-11-21T00:00:00Z", .returned_at = If(ReturnState = "returned", "2026-11-21T02:00:00Z", Nothing), .display_state = ReturnState, .return_phase = If(ReturnSettled, "done", If(ReturnApproved, "payment", If(ReturnState = "returned", "approval", "qc")))})
                    Next
                Case "/rest/v1/rental_overview", "/rest/v1/reservation_register"
                    For id = 1 To 61
                        rows.Add(New With {.id = id, .rental_number = "UJI-" & id.ToString("D3"), .pickup_at = "2026-11-21T03:00:00Z", .due_at = "2026-11-22T03:00:00Z", .display_state = If(changeStates.ContainsKey(CLng(id)), changeStates.GetValueOrDefault(CLng(id)), If(activePickups.Contains(CLng(id)), "active", "reserved")), .rent_amount = 100000D, .deposit_due = 100000D, .customer_name = "Pelanggan uji", .dp_received = 25000D, .rent_paid = If(activePickups.Contains(CLng(id)), 100000D, 25000D), .deposit_received = If(activePickups.Contains(CLng(id)), 100000D, 0D)})
                    Next
                Case Else
                    Throw New InvalidOperationException("Unexpected fake resource: " & path)
            End Select
            If EmptyData AndAlso path <> "/rest/v1/staff_profiles" Then rows.Clear()
            Dim page = rows.Skip(offset).Take(limit).ToArray()
            Return Reply(HttpStatusCode.OK, page, rows.Count, offset, page.Length)
        End If
        Throw New InvalidOperationException("Unexpected fake endpoint: " & path)
    End Function
End Class
