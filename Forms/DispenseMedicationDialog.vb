Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Dispense one prescription line against a stock batch (FEFO), or without stock tracking if the medicine isn't in the inventory catalog.</summary>
    Public Class DispenseMedicationDialog
        Inherits Form

        Private ReadOnly prescriptionId As Integer
        Private ReadOnly patientId As Integer
        Private ReadOnly medicineName As String
        Private ReadOnly remainingToDispense As Integer

        Private cboBatch As ComboBox
        Private numQuantity As NumericUpDown
        Private txtDosage, txtInstructions As TextBox
        Private lblStockWarning, lblError As Label
        Public Dispensed As Boolean = False

        Private Class BatchItem
            Public ReadOnly BatchId As Integer
            Public ReadOnly Remaining As Integer
            Public ReadOnly Label As String
            Public Sub New(batchId As Integer, remaining As Integer, label As String)
                Me.BatchId = batchId : Me.Remaining = remaining : Me.Label = label
            End Sub
            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

        Public Sub New(prescriptionId As Integer, patientId As Integer, patientName As String, medicineName As String, dosage As String, frequency As String, instructions As String, prescribedQty As Integer, alreadyDispensedQty As Integer)
            Me.prescriptionId = prescriptionId
            Me.patientId = patientId
            Me.medicineName = medicineName
            Me.remainingToDispense = Math.Max(prescribedQty - alreadyDispensedQty, 0)

            Me.Text = "Dispense Medication"
            Me.Size = New Size(520, 480)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = medicineName, .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})
            Me.Controls.Add(New Label With {.Text = $"{patientName}   |   Prescribed: {prescribedQty}   |   Already dispensed: {alreadyDispensedQty}", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Me.Controls.Add(New Label With {.Text = "Stock Batch (earliest-expiring first)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 80)})
            cboBatch = New ComboBox With {.Location = New Point(20, 100), .Width = 460, .DropDownStyle = ComboBoxStyle.DropDownList}
            AddHandler cboBatch.SelectedIndexChanged, AddressOf CboBatch_SelectedIndexChanged
            Me.Controls.Add(cboBatch)

            lblStockWarning = New Label With {.ForeColor = UiTheme.WarningAmber, .AutoSize = True, .Location = New Point(20, 130), .MaximumSize = New Size(460, 0)}
            Me.Controls.Add(lblStockWarning)

            Me.Controls.Add(New Label With {.Text = "Quantity to Dispense *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 170)})
            numQuantity = New NumericUpDown With {.Location = New Point(20, 190), .Width = 120, .Minimum = 0, .Maximum = 100000, .Value = Math.Max(remainingToDispense, 0)}
            Me.Controls.Add(numQuantity)

            Me.Controls.Add(New Label With {.Text = "Dosage", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 230)})
            txtDosage = New TextBox With {.Location = New Point(20, 250), .Width = 200, .Text = dosage}
            Me.Controls.Add(txtDosage)

            Me.Controls.Add(New Label With {.Text = "Instructions", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 288)})
            txtInstructions = New TextBox With {.Location = New Point(20, 308), .Width = 460, .Height = 60, .Multiline = True, .Text = instructions}
            Me.Controls.Add(txtInstructions)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 376), .MaximumSize = New Size(460, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(270, 408)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnDispense As New Button With {.Text = "Confirm Dispense", .Size = New Size(160, 36), .Location = New Point(380, 408)}
            UiTheme.StylePrimaryButton(btnDispense)
            AddHandler btnDispense.Click, AddressOf BtnDispense_Click
            Me.Controls.Add(btnDispense)

            LoadBatches()
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub LoadBatches()
            Try
                Dim table = Db.Query(
                    "SELECT b.batch_id, b.batch_lot_no, b.quantity_remaining, b.expiration_date
                     FROM medicine_stock_batches b
                     JOIN medicines m ON m.medicine_id = b.medicine_id
                     WHERE LOWER(m.medicine_name) = LOWER(@Name) AND b.quantity_remaining > 0
                     ORDER BY b.expiration_date ASC",
                    New With {.Name = medicineName})

                cboBatch.Items.Clear()
                For Each row As DataRow In table.Rows
                    Dim expText = If(IsDBNull(row("expiration_date")), "no expiry", Convert.ToDateTime(row("expiration_date")).ToString("MMM dd, yyyy"))
                    Dim label = $"Lot {row("batch_lot_no")} - {row("quantity_remaining")} left - exp. {expText}"
                    cboBatch.Items.Add(New BatchItem(Convert.ToInt32(row("batch_id")), Convert.ToInt32(row("quantity_remaining")), label))
                Next

                If cboBatch.Items.Count = 0 Then
                    lblStockWarning.Text = "This medicine has no tracked stock batches (not in the inventory catalog, or out of stock). Dispensing will be recorded without a stock deduction."
                    cboBatch.Enabled = False
                Else
                    cboBatch.SelectedIndex = 0
                End If
            Catch ex As Exception
                lblStockWarning.Text = "Could not load stock batches: " & ex.Message
            End Try
        End Sub

        Private Sub CboBatch_SelectedIndexChanged(sender As Object, e As EventArgs)
            If cboBatch.SelectedItem Is Nothing Then Return
            Dim batch = CType(cboBatch.SelectedItem, BatchItem)
            If numQuantity.Value > batch.Remaining Then numQuantity.Value = batch.Remaining
            numQuantity.Maximum = Math.Max(batch.Remaining, 1)
        End Sub

        Private Sub BtnDispense_Click(sender As Object, e As EventArgs)
            If numQuantity.Value <= 0 Then
                lblError.Text = "Enter a quantity greater than zero."
                Return
            End If

            Dim batch As BatchItem = Nothing
            If cboBatch.Enabled AndAlso cboBatch.SelectedItem IsNot Nothing Then
                batch = CType(cboBatch.SelectedItem, BatchItem)
                If numQuantity.Value > batch.Remaining Then
                    lblError.Text = $"Only {batch.Remaining} unit(s) remain in this batch."
                    Return
                End If
            End If
            If Not UiTheme.Confirm(Me, $"Dispense {numQuantity.Value} unit(s)? Stock will be deducted.", "Dispense Medication") Then Return

            Try
                Dim batchId As Object = If(batch IsNot Nothing, CType(batch.BatchId, Object), DBNull.Value)

                ' One transaction: logging the dispensing, decrementing stock, and flipping the
                ' prescription's status must all happen together or not at all -- otherwise a
                ' dropped connection mid-sequence could decrement stock without ever marking the
                ' prescription dispensed (or vice versa).
                Using tx As New DbTransaction()
                    tx.Execute(
                        "INSERT INTO pharmacy_dispensing (prescription_id, patient_id, date_dispensed, medicine_name, quantity_dispensed, dosage, instruction, dispensed_by, stock_deduction_id)
                         VALUES (@PrescriptionId, @PatientId, NOW(), @MedicineName, @Quantity, @Dosage, @Instruction, @DispensedBy, @BatchId)",
                        New With {
                            .PrescriptionId = prescriptionId, .PatientId = patientId, .MedicineName = medicineName, .Quantity = CInt(numQuantity.Value),
                            .Dosage = If(String.IsNullOrWhiteSpace(txtDosage.Text), CType(DBNull.Value, Object), txtDosage.Text.Trim()),
                            .Instruction = If(String.IsNullOrWhiteSpace(txtInstructions.Text), CType(DBNull.Value, Object), txtInstructions.Text.Trim()),
                            .DispensedBy = Session.Current.UserId, .BatchId = batchId
                        })

                    If batch IsNot Nothing Then
                        tx.Execute("UPDATE medicine_stock_batches SET quantity_remaining = quantity_remaining - @Qty WHERE batch_id = @BatchId",
                            New With {.Qty = CInt(numQuantity.Value), .BatchId = batch.BatchId})
                    End If

                    Dim totalDispensed = Convert.ToInt32(tx.QueryScalar("SELECT COALESCE(SUM(quantity_dispensed),0) FROM pharmacy_dispensing WHERE prescription_id = @Id", New With {.Id = prescriptionId}))
                    Dim prescribedQty = Convert.ToInt32(tx.QueryScalar("SELECT quantity FROM prescriptions_medication_orders WHERE prescription_id = @Id", New With {.Id = prescriptionId}))
                    Dim newStatus = If(totalDispensed >= prescribedQty, "Dispensed", "Partially Dispensed")
                    tx.Execute("UPDATE prescriptions_medication_orders SET status = @Status WHERE prescription_id = @Id", New With {.Status = newStatus, .Id = prescriptionId})

                    tx.Commit()
                End Using

                AuditLogger.Log("CREATE", "pharmacy", $"Prescription #{prescriptionId}", $"Dispensed {CInt(numQuantity.Value)} of {medicineName}")
                Dispensed = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not dispense: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
