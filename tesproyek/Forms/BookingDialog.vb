Option Strict On
Option Explicit On
Option Infer On
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RantauGear.Data
Imports RantauGear.Models
Imports RantauGear.Services
Imports RantauGear.UI
Namespace Forms
    Public Class BookingDialog
        Inherits Form
        Private ReadOnly repository As IRentalRepository
        Private ReadOnly actor As AppUser
        Private ReadOnly customerName As New TextBox With {.Dock = DockStyle.Fill}
        Private ReadOnly phone As New TextBox With {.Dock = DockStyle.Fill}
        Private ReadOnly identity As New CheckBox With {.Text = "Identitas pelanggan sudah diperiksa", .AutoSize = True}
        Private ReadOnly startAt As New DateTimePicker With {.Format = DateTimePickerFormat.Custom, .CustomFormat = "dd MMM yyyy HH:mm", .Dock = DockStyle.Fill, .Value = DateTime.Now}
        Private ReadOnly dueAt As New DateTimePicker With {.Format = DateTimePickerFormat.Custom, .CustomFormat = "dd MMM yyyy HH:mm", .Dock = DockStyle.Fill, .Value = DateTime.Now.AddDays(1)}
        Private ReadOnly deposit As New NumericUpDown With {.Maximum = 100000000, .ThousandsSeparator = True, .Increment = 10000, .Dock = DockStyle.Fill}
        Private ReadOnly packages As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Dock = DockStyle.Fill}
        Private ReadOnly units As New CheckedListBox With {.Dock = DockStyle.Fill, .CheckOnClick = True, .BorderStyle = BorderStyle.FixedSingle, .IntegralHeight = False}
        Private ReadOnly summary As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Primary, .Font = New Font("Segoe UI", 11, FontStyle.Bold)}
        Private ReadOnly errorText As New Label With {.Dock = DockStyle.Fill, .ForeColor = Theme.Maintenance}
        Private available As New List(Of Unit)
        Public Sub New(repository As IRentalRepository, actor As AppUser)
            Me.repository = repository
            Me.actor = actor
            dueAt.Value = startAt.Value.AddDays(1)
            Text = "Buat booking | RANTAU Gear"
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(720, 690)
            MinimumSize = New Size(650, 640)
            BackColor = Theme.Background
            Font = Theme.UiFont
            AutoScaleMode = AutoScaleMode.Dpi
            MinimizeBox = False
            MaximizeBox = False
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(24), .ColumnCount = 2, .RowCount = 11}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For i = 0 To 6
                layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
            Next
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            Controls.Add(layout)
            AddField(layout, 0, "Nama pelanggan", customerName)
            AddField(layout, 1, "Nomor telepon", phone)
            AddField(layout, 2, "Pengambilan", startAt)
            AddField(layout, 3, "Rencana kembali", dueAt)
            AddField(layout, 4, "Deposit (Rp)", deposit)
            AddField(layout, 5, "Pilih paket", packages)
            layout.Controls.Add(identity, 1, 6)
            AddField(layout, 7, "Unit tersedia", units)
            layout.Controls.Add(summary, 0, 8)
            layout.SetColumnSpan(summary, 2)
            layout.Controls.Add(errorText, 0, 9)
            layout.SetColumnSpan(errorText, 2)
            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
            Dim save As New RoundedButton With {.Text = "Simpan booking", .Width = 165, .Height = 38}
            Dim cancel As New Button With {.Text = "Batal", .DialogResult = DialogResult.Cancel, .Width = 90, .Height = 38, .Cursor = Cursors.Hand}
            actions.Controls.Add(save)
            actions.Controls.Add(cancel)
            layout.Controls.Add(actions, 0, 10)
            layout.SetColumnSpan(actions, 2)
            CancelButton = cancel
            packages.Items.Add("Pilih unit satuan")
            For Each package In repository.GetPackages()
                packages.Items.Add(package.Name & " / " & UiHelpers.Currency(package.DailyPrice))
            Next
            AddHandler startAt.ValueChanged, Sub() RefreshUnits()
            AddHandler dueAt.ValueChanged, Sub() RefreshUnits()
            AddHandler packages.SelectedIndexChanged, Sub() RefreshUnits()
            AddHandler units.ItemCheck, Sub(sender, e)
                                           If IsHandleCreated Then BeginInvoke(New Action(AddressOf RefreshTotal))
                                       End Sub
            AddHandler deposit.ValueChanged, Sub() RefreshTotal()
            AddHandler save.Click, AddressOf SaveBooking
            packages.SelectedIndex = 0
            AutoScaleDimensions = New SizeF(96, 96)
        End Sub
        Private Sub AddField(layout As TableLayoutPanel, row As Integer, caption As String, input As Control)
            Dim label = UiHelpers.LabelOf(caption, 10, Theme.Text, False)
            label.Dock = DockStyle.Fill
            input.AccessibleName = caption
            layout.Controls.Add(label, 0, row)
            layout.Controls.Add(input, 1, row)
        End Sub
        Private Sub RefreshUnits()
            units.Items.Clear()
            available = New AvailabilityService(repository).GetAvailableUnits(startAt.Value, dueAt.Value).Where(Function(u) u.Status = UnitStatus.Ready).ToList()
            Dim bundle = If(packages.SelectedIndex > 0, repository.GetPackages()(packages.SelectedIndex - 1), Nothing)
            For Each unit In available
                units.Items.Add(unit.Code & " | " & unit.Name & " | " & UiHelpers.Currency(unit.DailyRate), bundle IsNot Nothing AndAlso bundle.Items.Any(Function(i) i.UnitId = unit.Id))
            Next
            units.Enabled = bundle Is Nothing
            RefreshTotal()
        End Sub
        Private Sub RefreshTotal()
            If IsDisposed Then Return
            Dim days = CDec(Math.Max(1, Math.Ceiling((dueAt.Value - startAt.Value).TotalDays)))
            Dim daily As Decimal
            If packages.SelectedIndex > 0 Then
                daily = repository.GetPackages()(packages.SelectedIndex - 1).DailyPrice
            Else
                daily = units.CheckedIndices.Cast(Of Integer)().Sum(Function(i) available(i).DailyRate)
            End If
            summary.Text = days.ToString() & " hari x " & UiHelpers.Currency(daily) & " = " & UiHelpers.Currency(days * daily) & Environment.NewLine & "Dengan deposit: " & UiHelpers.Currency(days * daily + deposit.Value)
            errorText.Text = If(dueAt.Value <= startAt.Value, "Waktu kembali harus setelah pengambilan.", If(available.Count = 0, "Tidak ada unit tersedia untuk periode ini.", String.Empty))
        End Sub
        Private Sub SaveBooking(sender As Object, e As EventArgs)
            Try
                Dim bundle = If(packages.SelectedIndex > 0, repository.GetPackages()(packages.SelectedIndex - 1), Nothing)
                Dim ids = If(bundle Is Nothing, units.CheckedIndices.Cast(Of Integer)().Select(Function(i) available(i).Id).ToList(), bundle.Items.Select(Function(i) i.UnitId).ToList())
                Dim customer As New Customer With {.Name = customerName.Text.Trim(), .Phone = phone.Text.Trim(), .IdentityVerified = identity.Checked, .Deposit = deposit.Value, .GuaranteeType = "Identitas diperiksa dan deposit"}
                Dim rental = New RentalService(repository).Checkout(customer, ids, startAt.Value, dueAt.Value, deposit.Value, actor, If(bundle Is Nothing, 0, bundle.Id))
                MessageBox.Show("Booking " & rental.RentalNumber & " tersimpan dalam sesi demo ini.", "Booking tersimpan", MessageBoxButtons.OK, MessageBoxIcon.Information)
                DialogResult = DialogResult.OK
            Catch ex As InvalidOperationException
                errorText.Text = ex.Message
            End Try
        End Sub
    End Class
End Namespace
