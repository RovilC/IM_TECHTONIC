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

        Private Shared ReadOnly Green As Color = ColorTranslator.FromHtml("#1F5F3F")
        Private Shared ReadOnly GreenHover As Color = ColorTranslator.FromHtml("#174A31")
        Private Shared ReadOnly PageBg As Color = ColorTranslator.FromHtml("#F3F7F4")
        Private Shared ReadOnly CardBorder As Color = ColorTranslator.FromHtml("#E3E8E5")
        Private Shared ReadOnly InputBorder As Color = ColorTranslator.FromHtml("#D9DEDB")
        Private Shared ReadOnly TextMain As Color = ColorTranslator.FromHtml("#1A1F1C")
        Private Shared ReadOnly TextSoft As Color = ColorTranslator.FromHtml("#6B7280")
        Private Shared ReadOnly RequiredRed As Color = ColorTranslator.FromHtml("#DC2626")

        Private Shared ReadOnly LabelFont As New Font("Segoe UI Semibold", 8.75F)
        Private Shared ReadOnly InputFont As New Font("Segoe UI", 10.5F)

        ''' <summary>Label above a field; a trailing " *" becomes a separate red asterisk.</summary>
        Private Sub AddFieldLabel(parent As Control, text As String, x As Integer, y As Integer)
            Dim required = text.EndsWith("*")
            Dim caption = text.TrimEnd("*"c).TrimEnd()
            parent.Controls.Add(New Label With {.Text = caption, .Font = LabelFont, .ForeColor = TextMain, .AutoSize = True, .Location = New Point(x, y)})
            If required Then
                Dim w = TextRenderer.MeasureText(caption, LabelFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                parent.Controls.Add(New Label With {.Text = "*", .Font = LabelFont, .ForeColor = RequiredRed, .AutoSize = True, .Location = New Point(x + w + 2, y)})
            End If
        End Sub

        Public Sub New(patientId As Integer, patientDisplayName As String)
            Me.patientId = patientId
            Me.Text = "New Encounter"
            Me.Size = New Size(560, 480)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont
            Me.BackColor = PageBg

            ' White bordered card holding the whole form (2-column grid: x = 24 and x = 271, 233px wide, 14px gap).
            Dim card As New Panel With {.BackColor = Color.White, .Location = New Point(16, 16), .Width = 528}
            AddHandler card.Paint,
                Sub(s, e)
                    Using pen As New Pen(CardBorder)
                        e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
                    End Using
                End Sub
            Me.Controls.Add(card)
            Const col1 As Integer = 24
            Const col2 As Integer = 271
            Const colW As Integer = 233
            Const fullW As Integer = 480

            card.Controls.Add(New Label With {.Text = "Create a New Patient Encounter", .Font = New Font("Segoe UI Semibold", 11.5F), .ForeColor = TextMain, .AutoSize = True, .Location = New Point(col1, 20)})
            card.Controls.Add(New Label With {.Text = patientDisplayName, .ForeColor = TextSoft, .AutoSize = True, .Location = New Point(col1, 50)})

            Dim y = 90
            AddFieldLabel(card, "Visit Type *", col1, y)
            cboVisitType = New ComboBox With {.Location = New Point(col1, y + 22), .Width = colW, .Font = InputFont, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboVisitType.Items.AddRange({"Outpatient Consultation", "Follow-up", "Immunization", "Prenatal/Postpartum", "Emergency", "Referral Follow-up"})
            cboVisitType.SelectedIndex = 0
            card.Controls.Add(cboVisitType)

            y += 74
            AddFieldLabel(card, "Referral Source", col1, y)
            cboReferralSource = New ComboBox With {.Location = New Point(col1, y + 22), .Width = colW, .Font = InputFont, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboReferralSource.Items.AddRange({"Walk-in", "Referred", "Scheduled"})
            cboReferralSource.SelectedIndex = 0
            card.Controls.Add(cboReferralSource)

            AddFieldLabel(card, "Priority Level", col2, y)
            cboPriority = New ComboBox With {.Location = New Point(col2, y + 22), .Width = colW, .Font = InputFont, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboPriority.Items.AddRange({"Routine", "Urgent", "Emergency"})
            cboPriority.SelectedIndex = 0
            card.Controls.Add(cboPriority)

            y += 74
            AddFieldLabel(card, "Chief Complaint / Reason for Visit *", col1, y)
            txtChiefComplaint = New TextBox With {.Location = New Point(col1, y + 22), .Width = fullW, .Height = 64, .Multiline = True, .Font = InputFont, .BorderStyle = BorderStyle.FixedSingle, .ScrollBars = ScrollBars.Vertical}
            card.Controls.Add(txtChiefComplaint)

            y += 104
            AddFieldLabel(card, "Additional Notes", col1, y)
            txtNotes = New TextBox With {.Location = New Point(col1, y + 22), .Width = fullW, .Height = 52, .Multiline = True, .Font = InputFont, .BorderStyle = BorderStyle.FixedSingle, .ScrollBars = ScrollBars.Vertical}
            card.Controls.Add(txtNotes)

            y += 92
            lblError = New Label With {.ForeColor = RequiredRed, .AutoSize = True, .MaximumSize = New Size(fullW, 0), .Location = New Point(col1, y)}
            card.Controls.Add(lblError)

            ' Footer: divider, then Cancel (left, small outline) and the primary action (right).
            y += 36
            card.Controls.Add(New Panel With {.Location = New Point(col1, y), .Size = New Size(fullW, 1), .BackColor = CardBorder})
            y += 18

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(84, 30), .Location = New Point(col1, y + 3)}
            btnCancel.FlatStyle = FlatStyle.Flat
            btnCancel.BackColor = Color.White
            btnCancel.ForeColor = TextSoft
            btnCancel.Font = New Font("Segoe UI", 9.0F)
            btnCancel.UseVisualStyleBackColor = False
            btnCancel.FlatAppearance.BorderColor = InputBorder
            btnCancel.FlatAppearance.BorderSize = 1
            btnCancel.FlatAppearance.MouseOverBackColor = PageBg
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            card.Controls.Add(btnCancel)

            Dim btnStart As New Button With {.Text = "Save && Start Triage ->", .Size = New Size(200, 36), .Location = New Point(col1 + fullW - 200, y)}
            btnStart.FlatStyle = FlatStyle.Flat
            btnStart.BackColor = Green
            btnStart.ForeColor = Color.White
            btnStart.Font = New Font("Segoe UI Semibold", 10.0F)
            btnStart.UseVisualStyleBackColor = False
            btnStart.FlatAppearance.BorderSize = 0
            btnStart.FlatAppearance.MouseOverBackColor = GreenHover
            btnStart.FlatAppearance.MouseDownBackColor = GreenHover
            AddHandler btnStart.Click, AddressOf BtnStart_Click
            card.Controls.Add(btnStart)

            ' Size the card and window to the content actually built (the old fixed height clipped the buttons).
            card.Height = y + 36 + 24
            Me.ClientSize = New Size(560, card.Bottom + 16)
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
