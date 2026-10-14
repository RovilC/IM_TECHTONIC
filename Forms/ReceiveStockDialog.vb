Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Receives a new stock batch (batch/lot no, quantity, source, expiration) for a medicine already in the catalog.</summary>
    Public Class ReceiveStockDialog
        Inherits Form

        Private ReadOnly medicineId As Integer
        Private txtBatchLot, txtSource As TextBox
        Private numQuantity As NumericUpDown
        Private dtpExpiration As DateTimePicker
        Private chkNoExpiration As CheckBox
        Private lblError As Label
        Public Saved As Boolean = False

        Public Sub New(medicineId As Integer, medicineName As String)
            Me.medicineId = medicineId
            Me.Text = "Receive Stock"
            Me.Size = New Size(460, 420)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = "Receive Stock", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})
            Me.Controls.Add(New Label With {.Text = medicineName, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Me.Controls.Add(New Label With {.Text = "Batch / Lot No.", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 80)})
            txtBatchLot = New TextBox With {.Location = New Point(20, 100), .Width = 200}
            Me.Controls.Add(txtBatchLot)

            Me.Controls.Add(New Label With {.Text = "Quantity Received *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(240, 80)})
            numQuantity = New NumericUpDown With {.Location = New Point(240, 100), .Width = 160, .Maximum = 1000000, .Minimum = 1, .Value = 100}
            Me.Controls.Add(numQuantity)

            Me.Controls.Add(New Label With {.Text = "Source", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 140)})
            txtSource = New TextBox With {.Location = New Point(20, 160), .Width = 380, .Text = "RHU supply"}
            Me.Controls.Add(txtSource)

            chkNoExpiration = New CheckBox With {.Text = "No expiration date", .Location = New Point(20, 196), .AutoSize = True}
            AddHandler chkNoExpiration.CheckedChanged, Sub() dtpExpiration.Enabled = Not chkNoExpiration.Checked
            Me.Controls.Add(chkNoExpiration)

            Me.Controls.Add(New Label With {.Text = "Expiration Date", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 224)})
            dtpExpiration = New DateTimePicker With {.Location = New Point(20, 244), .Width = 200, .Format = DateTimePickerFormat.Short, .Value = DateTime.Today.AddYears(1)}
            Me.Controls.Add(dtpExpiration)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 288), .MaximumSize = New Size(380, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(210, 330)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Save", .Size = New Size(110, 36), .Location = New Point(320, 330)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If numQuantity.Value <= 0 Then
                lblError.Text = "Quantity must be greater than zero."
                Return
            End If
            If Not UiTheme.Confirm(Me, $"Receive {numQuantity.Value} unit(s) into stock?", "Receive Stock") Then Return
            Try
                Dim expiration As Object = If(chkNoExpiration.Checked, CType(DBNull.Value, Object), dtpExpiration.Value.Date)
                Db.Execute(
                    "INSERT INTO medicine_stock_batches (medicine_id, batch_lot_no, quantity_received, quantity_remaining, source, expiration_date, date_received, received_by)
                     VALUES (@MedicineId, @BatchLot, @Qty, @Qty, @Source, @Expiration, NOW(), @ReceivedBy)",
                    New With {
                        .MedicineId = medicineId,
                        .BatchLot = If(String.IsNullOrWhiteSpace(txtBatchLot.Text), CType(DBNull.Value, Object), txtBatchLot.Text.Trim()),
                        .Qty = CInt(numQuantity.Value),
                        .Source = If(String.IsNullOrWhiteSpace(txtSource.Text), CType(DBNull.Value, Object), txtSource.Text.Trim()),
                        .Expiration = expiration, .ReceivedBy = Session.Current.UserId
                    })
                AuditLogger.Log("CREATE", "inventory", $"Medicine #{medicineId}", $"Received {CInt(numQuantity.Value)} units")
                Saved = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not save: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
