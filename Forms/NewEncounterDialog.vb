Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Modal "New Encounter" form (Figure 22): visit metadata captured before triage/consultation begins.</summary>
    Public Class NewEncounterDialog
        Inherits Form

        Private ReadOnly patientId As Integer
        Public NewEncounterId As Integer? = Nothing

        Private cboVisitType, cboReferralSource, cboPriority As ComboBox
        Private txtChiefComplaint, txtNotes As TextBox
        Private lblError As Label

        Public Sub New(patientId As Integer, patientDisplayName As String)
            Me.patientId = patientId
            Me.Text = "New Encounter"
            Me.Size = New Size(560, 480)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont
            Me.Padding = New Padding(20)

            Dim lblTitle As New Label With {.Text = "Create a New Patient Encounter", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)}
            Me.Controls.Add(lblTitle)
            Me.Controls.Add(New Label With {.Text = patientDisplayName, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Dim y = 84
            Me.Controls.Add(New Label With {.Text = "Visit Type *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            cboVisitType = New ComboBox With {.Location = New Point(20, y + 20), .Width = 240, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboVisitType.Items.AddRange({"Outpatient Consultation", "Follow-up", "Immunization", "Prenatal/Postpartum", "Emergency", "Referral Follow-up"})
            cboVisitType.SelectedIndex = 0
            Me.Controls.Add(cboVisitType)

            Me.Controls.Add(New Label With {.Text = "Priority Level", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(280, y)})
            cboPriority = New ComboBox With {.Location = New Point(280, y + 20), .Width = 220, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboPriority.Items.AddRange({"Routine", "Urgent", "Emergency"})
            cboPriority.SelectedIndex = 0
            Me.Controls.Add(cboPriority)

            y += 60
            Me.Controls.Add(New Label With {.Text = "Referral Source", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            cboReferralSource = New ComboBox With {.Location = New Point(20, y + 20), .Width = 240, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboReferralSource.Items.AddRange({"Walk-in", "Referred", "Scheduled"})
            cboReferralSource.SelectedIndex = 0
            Me.Controls.Add(cboReferralSource)

            y += 60
            Me.Controls.Add(New Label With {.Text = "Chief Complaint / Reason for Visit *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtChiefComplaint = New TextBox With {.Location = New Point(20, y + 20), .Width = 480, .Height = 60, .Multiline = True}
            Me.Controls.Add(txtChiefComplaint)

            y += 100
            Me.Controls.Add(New Label With {.Text = "Additional Notes", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtNotes = New TextBox With {.Location = New Point(20, y + 20), .Width = 480, .Height = 50, .Multiline = True}
            Me.Controls.Add(txtNotes)

            y += 82
            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .MaximumSize = New Size(480, 0), .Location = New Point(20, y)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(300, y + 30)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnStart As New Button With {.Text = "Save && Start Triage ->", .Size = New Size(200, 36), .Location = New Point(300, y + 30)}
            btnStart.Location = New Point(300, y + 30)
            UiTheme.StylePrimaryButton(btnStart)
            AddHandler btnStart.Click, AddressOf BtnStart_Click
            Me.Controls.Add(btnStart)

            ' Buttons landed at y+30+36=452 against a hardcoded 480 window height, which nets out
            ' to only ~35px of client area below them once the title bar/borders are subtracted --
            ' in practice they rendered clipped at the bottom edge. Size to the content actually built.
            Me.ClientSize = New Size(Me.ClientSize.Width, y + 30 + 36 + 24)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub BtnStart_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtChiefComplaint.Text) Then
                lblError.Text = "Chief complaint is required."
                Return
            End If
            If Not UiTheme.Confirm(Me, "Start a new encounter for this patient?", "New Encounter") Then Return
            Try
                Dim encounterNo = IdGenerator.NextEncounterNo()
                Dim newId = Db.ExecuteInsertReturnId(
                    "INSERT INTO encounters (patient_id, encounter_no, encounter_date, visit_type, referral_source, priority_level, chief_complaint, additional_notes, status, created_by)
                     VALUES (@PatientId, @EncounterNo, NOW(), @VisitType, @ReferralSource, @Priority, @ChiefComplaint, @Notes, 'Open', @CreatedBy)",
                    New With {
                        .PatientId = patientId, .EncounterNo = encounterNo, .VisitType = cboVisitType.Text,
                        .ReferralSource = cboReferralSource.Text, .Priority = cboPriority.Text,
                        .ChiefComplaint = txtChiefComplaint.Text.Trim(),
                        .Notes = If(String.IsNullOrWhiteSpace(txtNotes.Text), CType(DBNull.Value, Object), txtNotes.Text.Trim()),
                        .CreatedBy = Session.Current.UserId
                    })
                AuditLogger.Log("CREATE", "encounters", encounterNo, $"Started encounter for patient #{patientId}")
                NewEncounterId = CInt(newId)
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not create encounter: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
