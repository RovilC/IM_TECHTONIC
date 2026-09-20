Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' Clinical workspace for one encounter (Figures 21-29): Triage vitals,
    ''' then a Consultation tab set (Chief Complaint, Medical History,
    ''' Physical Exam, Assessment/Diagnoses, Treatment Plan, Medications).
    ''' </summary>
    Public Class EncounterWorkspaceForm
        Inherits Form

        Private ReadOnly encounterId As Integer
        Private patientId As Integer
        Private consultationId As Integer? = Nothing

        Private lblBanner, lblBannerMeta, lblTags As Label
        Private innerNav As TabControl

        ' Triage controls
        Private txtTemp, txtBpSys, txtBpDia, txtHr, txtRr, txtSpo2, txtWeight, txtHeight As TextBox
        Private lblBmi As Label
        Private numPain As NumericUpDown
        Private txtGeneralAppearance As TextBox
        Private cboTriageClass As ComboBox

        ' Consultation controls
        Private txtChiefComplaint, txtHpi As TextBox
        Private txtPastHistory, txtFamilyHistory, txtSocialHistory, txtCurrentMeds As TextBox
        Private txtPeGeneral, txtPeHeent, txtPeCardio, txtPeRespi, txtPeAbdomen, txtPeExtrem, txtPeNeuro As TextBox
        Private gridDiagnoses As DataGridView
        Private txtTreatmentPlan, txtFollowUpInstructions As TextBox
        Private dtpFollowUp As DateTimePicker
        Private chkNoFollowUp As CheckBox
        Private gridMedications As DataGridView

        Private lblStatus As Label

        Public Sub New(encounterId As Integer)
            Me.encounterId = encounterId
            InitializeComponent()
            LoadEncounter()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = UiTheme.BaseFont

            Dim banner As New Panel With {.Dock = DockStyle.Top, .Height = 90, .BackColor = Color.White, .Padding = New Padding(20, 10, 20, 10)}
            Me.Controls.Add(banner)
            lblBanner = New Label With {.Font = New Font("Segoe UI", 13.0F, FontStyle.Bold), .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(20, 8)}
            banner.Controls.Add(lblBanner)
            lblBannerMeta = New Label With {.ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 34)}
            banner.Controls.Add(lblBannerMeta)
            lblTags = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 56)}
            banner.Controls.Add(lblTags)
            lblStatus = New Label With {.ForeColor = UiTheme.AccentGreen, .Font = UiTheme.BoldFont, .AutoSize = True}
            banner.Controls.Add(lblStatus)
            UiTheme.PinToRight(lblStatus, banner, 20, 20)

            innerNav = New TabControl With {.Dock = DockStyle.Fill}
            Me.Controls.Add(innerNav)
            innerNav.BringToFront()

            Dim tabTriage As New TabPage("Triage")
            innerNav.TabPages.Add(tabTriage)
            BuildTriageTab(tabTriage)

            Dim tabConsult As New TabPage("Consultation")
            innerNav.TabPages.Add(tabConsult)
            BuildConsultationTab(tabConsult)

            Dim actionBar As New Panel With {.Dock = DockStyle.Bottom, .Height = 56, .BackColor = Color.White}
            Me.Controls.Add(actionBar)

            Dim btnComplete As New Button With {.Text = "Complete Encounter", .Size = New Size(180, 36)}
            UiTheme.StylePrimaryButton(btnComplete)
            AddHandler btnComplete.Click, AddressOf BtnCompleteEncounter_Click
            actionBar.Controls.Add(btnComplete)

            Dim btnRequestLab As New Button With {.Text = "Request Lab", .Size = New Size(130, 36)}
            UiTheme.StyleSecondaryButton(btnRequestLab)
            AddHandler btnRequestLab.Click, AddressOf BtnRequestLab_Click
            actionBar.Controls.Add(btnRequestLab)

            Dim btnCertificate As New Button With {.Text = "Medical Certificate", .Size = New Size(160, 36)}
            UiTheme.StyleSecondaryButton(btnCertificate)
            AddHandler btnCertificate.Click, AddressOf BtnMedicalCertificate_Click
            actionBar.Controls.Add(btnCertificate)

            UiTheme.PinRowToRight(actionBar, 10, 0, 10, btnComplete, btnRequestLab, btnCertificate)
        End Sub

        ' ---------------- Triage tab (Figure 23) ----------------
        Private Sub BuildTriageTab(tab As TabPage)
            Dim p As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(20)}
            tab.Controls.Add(p)

            p.Controls.Add(New Label With {.Text = "Vital Signs", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            txtTemp = LabeledBox(p, "Temperature (C) *", 0, 40)
            txtBpSys = LabeledBox(p, "BP Systolic", 180, 40, 80)
            txtBpDia = LabeledBox(p, "BP Diastolic", 270, 40, 80)
            txtHr = LabeledBox(p, "Heart Rate (bpm)", 380, 40, 110)
            txtRr = LabeledBox(p, "Respiratory Rate", 500, 40, 110)
            txtSpo2 = LabeledBox(p, "O2 Saturation (%)", 620, 40, 110)

            txtWeight = LabeledBox(p, "Weight (kg)", 0, 96, 120)
            txtHeight = LabeledBox(p, "Height (cm)", 140, 96, 120)
            AddHandler txtWeight.TextChanged, Sub() RecalculateBmi()
            AddHandler txtHeight.TextChanged, Sub() RecalculateBmi()
            p.Controls.Add(New Label With {.Text = "BMI (auto)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(280, 96)})
            lblBmi = New Label With {.ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 116)}
            p.Controls.Add(lblBmi)

            p.Controls.Add(New Label With {.Text = "Pain Scale (0-10)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(420, 96)})
            numPain = New NumericUpDown With {.Minimum = 0, .Maximum = 10, .Location = New Point(420, 116), .Width = 80}
            p.Controls.Add(numPain)

            p.Controls.Add(New Label With {.Text = "Triage Classification", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 156)})
            cboTriageClass = New ComboBox With {.Location = New Point(0, 176), .Width = 240, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboTriageClass.Items.AddRange({"Non-urgent", "Priority", "Emergency"})
            p.Controls.Add(cboTriageClass)

            p.Controls.Add(New Label With {.Text = "General Appearance", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 216)})
            txtGeneralAppearance = New TextBox With {.Location = New Point(0, 236), .Width = 700, .Height = 60, .Multiline = True}
            p.Controls.Add(txtGeneralAppearance)

            Dim btnSaveTriage As New Button With {.Text = "Save Triage", .Size = New Size(150, 36), .Location = New Point(0, 312)}
            UiTheme.StylePrimaryButton(btnSaveTriage)
            AddHandler btnSaveTriage.Click, AddressOf BtnSaveTriage_Click
            p.Controls.Add(btnSaveTriage)
        End Sub

        Private Function LabeledBox(container As Panel, label As String, x As Integer, y As Integer, Optional width As Integer = 150) As TextBox
            container.Controls.Add(New Label With {.Text = label, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(x, y)})
            Dim tb As New TextBox With {.Location = New Point(x, y + 20), .Width = width}
            container.Controls.Add(tb)
            Return tb
        End Function

        Private Sub RecalculateBmi()
            Dim w, h As Double
            If Double.TryParse(txtWeight.Text, w) AndAlso Double.TryParse(txtHeight.Text, h) AndAlso h > 0 Then
                Dim heightM = h / 100.0
                Dim bmi = w / (heightM * heightM)
                Dim category = If(bmi < 18.5, "Underweight", If(bmi < 25, "Normal", If(bmi < 30, "Overweight", "Obese")))
                lblBmi.Text = $"{bmi:0.0} kg/m2 - {category}"
            Else
                lblBmi.Text = ""
            End If
        End Sub

        ' ---------------- Consultation tab (Figures 24-29) ----------------
        Private Sub BuildConsultationTab(tab As TabPage)
            Dim consultTabs As New TabControl With {.Dock = DockStyle.Fill}
            tab.Controls.Add(consultTabs)

            ' -- Chief Complaint --
            Dim tCC As New TabPage("Chief Complaint")
            consultTabs.TabPages.Add(tCC)
            Dim pCC As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(20)}
            tCC.Controls.Add(pCC)
            pCC.Controls.Add(New Label With {.Text = "Chief Complaint *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 0)})
            txtChiefComplaint = New TextBox With {.Location = New Point(0, 20), .Width = 700, .Height = 50, .Multiline = True}
            pCC.Controls.Add(txtChiefComplaint)
            pCC.Controls.Add(New Label With {.Text = "History of Present Illness", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 84)})
            txtHpi = New TextBox With {.Location = New Point(0, 104), .Width = 700, .Height = 100, .Multiline = True}
            pCC.Controls.Add(txtHpi)

            ' -- Medical History --
            Dim tHist As New TabPage("Medical History")
            consultTabs.TabPages.Add(tHist)
            Dim pHist As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(20)}
            tHist.Controls.Add(pHist)
            pHist.Controls.Add(New Label With {.Text = "Past Medical History", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 0)})
            txtPastHistory = New TextBox With {.Location = New Point(0, 20), .Width = 340, .Height = 70, .Multiline = True}
            pHist.Controls.Add(txtPastHistory)
            pHist.Controls.Add(New Label With {.Text = "Family History", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(360, 0)})
            txtFamilyHistory = New TextBox With {.Location = New Point(360, 20), .Width = 340, .Height = 70, .Multiline = True}
            pHist.Controls.Add(txtFamilyHistory)
            pHist.Controls.Add(New Label With {.Text = "Social History", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 104)})
            txtSocialHistory = New TextBox With {.Location = New Point(0, 124), .Width = 340, .Height = 70, .Multiline = True}
            pHist.Controls.Add(txtSocialHistory)
            pHist.Controls.Add(New Label With {.Text = "Current Medications", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(360, 104)})
            txtCurrentMeds = New TextBox With {.Location = New Point(360, 124), .Width = 340, .Height = 70, .Multiline = True}
            pHist.Controls.Add(txtCurrentMeds)

            ' -- Physical Exam --
            Dim tExam As New TabPage("Physical Exam")
            consultTabs.TabPages.Add(tExam)
            Dim pExam As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(20)}
            tExam.Controls.Add(pExam)
            txtPeGeneral = LabeledMultiline(pExam, "General", 0, 0)
            txtPeHeent = LabeledMultiline(pExam, "HEENT", 360, 0)
            txtPeCardio = LabeledMultiline(pExam, "Cardiovascular", 0, 110)
            txtPeRespi = LabeledMultiline(pExam, "Respiratory", 360, 110)
            txtPeAbdomen = LabeledMultiline(pExam, "Abdomen", 0, 220)
            txtPeExtrem = LabeledMultiline(pExam, "Extremities", 360, 220)
            txtPeNeuro = LabeledMultiline(pExam, "Neurological", 0, 330)

            ' -- Assessment / Diagnoses (Figure 27) --
            Dim tAssess As New TabPage("Assessment")
            consultTabs.TabPages.Add(tAssess)
            Dim pAssess As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
            tAssess.Controls.Add(pAssess)
            pAssess.Controls.Add(New Label With {.Text = "Current Diagnoses", .Font = UiTheme.SubHeaderFont, .Dock = DockStyle.Top, .Height = 30})
            gridDiagnoses = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridDiagnoses)
            gridDiagnoses.ReadOnly = False
            gridDiagnoses.AllowUserToAddRows = True
            gridDiagnoses.AllowUserToDeleteRows = True
            gridDiagnoses.Columns.Add("icd_code", "ICD Code")
            gridDiagnoses.Columns.Add("diagnosis_desc", "Diagnosis")
            Dim colClass As New DataGridViewComboBoxColumn With {.Name = "classification", .HeaderText = "Classification"}
            colClass.Items.AddRange("Acute", "Chronic")
            gridDiagnoses.Columns.Add(colClass)
            Dim colStat As New DataGridViewComboBoxColumn With {.Name = "status", .HeaderText = "Status"}
            colStat.Items.AddRange("Active", "Resolved")
            gridDiagnoses.Columns.Add(colStat)
            gridDiagnoses.Columns("diagnosis_desc").FillWeight = 200
            Dim pnlAssessBottom As New Panel With {.Dock = DockStyle.Bottom, .Height = 44}
            pAssess.Controls.Add(gridDiagnoses)
            pAssess.Controls.Add(pnlAssessBottom)

            ' -- Treatment Plan --
            Dim tPlan As New TabPage("Treatment Plan")
            consultTabs.TabPages.Add(tPlan)
            Dim pPlan As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(20)}
            tPlan.Controls.Add(pPlan)
            pPlan.Controls.Add(New Label With {.Text = "Treatment Plan", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 0)})
            txtTreatmentPlan = New TextBox With {.Location = New Point(0, 20), .Width = 700, .Height = 100, .Multiline = True}
            pPlan.Controls.Add(txtTreatmentPlan)

            chkNoFollowUp = New CheckBox With {.Text = "No follow-up needed", .Location = New Point(0, 130), .AutoSize = True}
            pPlan.Controls.Add(chkNoFollowUp)
            pPlan.Controls.Add(New Label With {.Text = "Follow-up Date", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 158)})
            dtpFollowUp = New DateTimePicker With {.Location = New Point(0, 178), .Width = 200, .Format = DateTimePickerFormat.Short}
            pPlan.Controls.Add(dtpFollowUp)
            AddHandler chkNoFollowUp.CheckedChanged, Sub() dtpFollowUp.Enabled = Not chkNoFollowUp.Checked

            pPlan.Controls.Add(New Label With {.Text = "Follow-up Instructions", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(240, 158)})
            txtFollowUpInstructions = New TextBox With {.Location = New Point(240, 178), .Width = 460, .Height = 60, .Multiline = True}
            pPlan.Controls.Add(txtFollowUpInstructions)

            ' -- Medications (Figure 29) --
            Dim tMeds As New TabPage("Medications")
            consultTabs.TabPages.Add(tMeds)
            Dim pMeds As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(20)}
            tMeds.Controls.Add(pMeds)
            pMeds.Controls.Add(New Label With {.Text = "Prescribed Medications", .Font = UiTheme.SubHeaderFont, .Dock = DockStyle.Top, .Height = 30})
            gridMedications = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridMedications)
            gridMedications.ReadOnly = False
            gridMedications.AllowUserToAddRows = True
            gridMedications.AllowUserToDeleteRows = True
            gridMedications.Columns.Add("medicine_name", "Medicine")
            gridMedications.Columns.Add("dosage", "Dosage")
            gridMedications.Columns.Add("frequency", "Frequency")
            Dim colQty As New DataGridViewTextBoxColumn With {.Name = "quantity", .HeaderText = "Quantity"}
            gridMedications.Columns.Add(colQty)
            gridMedications.Columns.Add("instructions", "Instructions")
            gridMedications.Columns.Add("status", "Status")
            gridMedications.Columns("status").ReadOnly = True
            gridMedications.Columns("status").DefaultCellStyle.ForeColor = UiTheme.TextMuted
            Dim colRxId As New DataGridViewTextBoxColumn With {.Name = "prescription_id", .Visible = False}
            gridMedications.Columns.Add(colRxId)
            gridMedications.Columns("medicine_name").FillWeight = 160
            gridMedications.Columns("instructions").FillWeight = 200
            AddHandler gridMedications.CellBeginEdit, AddressOf GridMedications_CellBeginEdit
            AddHandler gridMedications.UserDeletingRow, AddressOf GridMedications_UserDeletingRow
            pMeds.Controls.Add(gridMedications)
            gridMedications.BringToFront()

            ' shared Save Draft button, docked under the inner consult tab control
            Dim pnlSave As New Panel With {.Dock = DockStyle.Bottom, .Height = 50}
            tab.Controls.Add(pnlSave)
            tab.Controls.SetChildIndex(pnlSave, 0)
            Dim btnSaveDraft As New Button With {.Text = "Save Draft", .Size = New Size(140, 36), .Location = New Point(0, 8)}
            UiTheme.StylePrimaryButton(btnSaveDraft)
            AddHandler btnSaveDraft.Click, AddressOf BtnSaveConsultation_Click
            pnlSave.Controls.Add(btnSaveDraft)
        End Sub

        Private Function LabeledMultiline(container As Panel, label As String, x As Integer, y As Integer) As TextBox
            container.Controls.Add(New Label With {.Text = label, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(x, y)})
            Dim tb As New TextBox With {.Location = New Point(x, y + 20), .Width = 340, .Height = 80, .Multiline = True}
            container.Controls.Add(tb)
            Return tb
        End Function

        ' ---------------- Data load/save ----------------

        Private Sub LoadEncounter()
            Try
                Dim table = Db.Query(
                    "SELECT e.*, p.first_name, p.middle_name, p.last_name, p.patient_no, p.date_of_birth, p.sex, p.known_allergies, p.existing_conditions
                     FROM encounters e JOIN patients p ON p.patient_id = e.patient_id
                     WHERE e.encounter_id = @Id", New With {.Id = encounterId})
                If table.Rows.Count = 0 Then
                    lblBanner.Text = "Encounter not found"
                    ' patientId would otherwise silently stay 0, and a later Save Triage
                    ' would issue a harmless-looking but wrong "UPDATE patients ... WHERE
                    ' patient_id=0" -- lock the whole workspace instead of allowing that.
                    ' (Me.Enabled, not just innerNav, since the action bar's Save/Complete
                    ' buttons live outside innerNav.)
                    Me.Enabled = False
                    Return
                End If
                Dim r = table.Rows(0)
                patientId = Convert.ToInt32(r("patient_id"))
                Dim age = DateTime.Today.Year - Convert.ToDateTime(r("date_of_birth")).Year
                lblBanner.Text = $"{r("last_name")}, {r("first_name")} {r("middle_name")}   ({r("patient_no")})"
                lblBannerMeta.Text = $"{age} yrs - {r("sex")}   |   Encounter {r("encounter_no")}   |   {Convert.ToDateTime(r("encounter_date")):MMM dd, yyyy hh:mm tt}   |   {r("chief_complaint")}"
                lblStatus.Text = r("status").ToString()

                Dim tags As New List(Of String)
                If Not IsDBNull(r("existing_conditions")) AndAlso r("existing_conditions").ToString().Trim().Length > 0 Then tags.Add(r("existing_conditions").ToString())
                If Not IsDBNull(r("known_allergies")) AndAlso r("known_allergies").ToString().Trim().Length > 0 Then tags.Add("Allergy: " & r("known_allergies").ToString())
                lblTags.Text = String.Join("   |   ", tags)

                If Not IsDBNull(r("triage_temp_c")) Then txtTemp.Text = r("triage_temp_c").ToString()
                If Not IsDBNull(r("triage_bp_systolic")) Then txtBpSys.Text = r("triage_bp_systolic").ToString()
                If Not IsDBNull(r("triage_bp_diastolic")) Then txtBpDia.Text = r("triage_bp_diastolic").ToString()
                If Not IsDBNull(r("triage_heart_rate")) Then txtHr.Text = r("triage_heart_rate").ToString()
                If Not IsDBNull(r("triage_respiratory_rate")) Then txtRr.Text = r("triage_respiratory_rate").ToString()
                If Not IsDBNull(r("triage_spo2")) Then txtSpo2.Text = r("triage_spo2").ToString()
                If Not IsDBNull(r("triage_weight_kg")) Then txtWeight.Text = r("triage_weight_kg").ToString()
                If Not IsDBNull(r("triage_height_cm")) Then txtHeight.Text = r("triage_height_cm").ToString()
                If Not IsDBNull(r("triage_pain_scale")) Then numPain.Value = Convert.ToInt32(r("triage_pain_scale"))
                If Not IsDBNull(r("triage_general_appearance")) Then txtGeneralAppearance.Text = r("triage_general_appearance").ToString()
                If Not IsDBNull(r("triage_classification")) Then cboTriageClass.SelectedItem = r("triage_classification").ToString()
                If Not IsDBNull(r("chief_complaint")) Then txtChiefComplaint.Text = r("chief_complaint").ToString()

                LoadConsultation()
                LoadMedications()
            Catch ex As Exception
                lblBanner.Text = "Error loading encounter: " & ex.Message
            End Try
        End Sub

        Private Sub LoadConsultation()
            Dim table = Db.Query("SELECT * FROM consultations WHERE encounter_id = @Id ORDER BY consultation_id DESC LIMIT 1", New With {.Id = encounterId})
            If table.Rows.Count = 0 Then Return
            Dim r = table.Rows(0)
            consultationId = Convert.ToInt32(r("consultation_id"))
            txtHpi.Text = SafeStr(r("history_of_present_illness"))
            txtPastHistory.Text = SafeStr(r("past_medical_history"))
            txtFamilyHistory.Text = SafeStr(r("family_history"))
            txtSocialHistory.Text = SafeStr(r("social_history"))
            txtCurrentMeds.Text = SafeStr(r("current_medications"))
            txtPeGeneral.Text = SafeStr(r("pe_general"))
            txtPeHeent.Text = SafeStr(r("pe_heent"))
            txtPeCardio.Text = SafeStr(r("pe_cardiovascular"))
            txtPeRespi.Text = SafeStr(r("pe_respiratory"))
            txtPeAbdomen.Text = SafeStr(r("pe_abdomen"))
            txtPeExtrem.Text = SafeStr(r("pe_extremities"))
            txtPeNeuro.Text = SafeStr(r("pe_neurological"))
            txtTreatmentPlan.Text = SafeStr(r("treatment_plan"))
            txtFollowUpInstructions.Text = SafeStr(r("follow_up_instructions"))
            If Not IsDBNull(r("follow_up_date")) Then
                dtpFollowUp.Value = Convert.ToDateTime(r("follow_up_date"))
            Else
                chkNoFollowUp.Checked = True
            End If

            Dim diag = Db.Query("SELECT icd_code, diagnosis_desc, classification, status FROM diagnoses WHERE consultation_id = @Id", New With {.Id = consultationId.Value})
            For Each dr As DataRow In diag.Rows
                gridDiagnoses.Rows.Add(dr("icd_code"), dr("diagnosis_desc"), dr("classification"), dr("status"))
            Next
        End Sub

        Private Sub LoadMedications()
            Dim rx = Db.Query("SELECT prescription_id, medicine_name, dosage, frequency, quantity, instructions, status FROM prescriptions_medication_orders WHERE encounter_id = @Id", New With {.Id = encounterId})
            For Each dr As DataRow In rx.Rows
                gridMedications.Rows.Add(dr("medicine_name"), dr("dosage"), dr("frequency"), dr("quantity"), dr("instructions"), dr("status"), dr("prescription_id"))
            Next
        End Sub

        ''' <summary>Once a medication has been dispensed (or partially dispensed), its row can no longer be edited or deleted here -- changing it would desync from what the patient actually received. Adjust dosage/instructions for a NEW prescription instead.</summary>
        Private Function IsMedicationRowLocked(row As DataGridViewRow) As Boolean
            Dim status = TryCellString(row, "status")
            Return status.Length > 0 AndAlso status <> "Pending"
        End Function

        Private Sub GridMedications_CellBeginEdit(sender As Object, e As DataGridViewCellCancelEventArgs)
            If e.RowIndex < 0 OrElse e.RowIndex >= gridMedications.Rows.Count Then Return
            If IsMedicationRowLocked(gridMedications.Rows(e.RowIndex)) Then e.Cancel = True
        End Sub

        Private Sub GridMedications_UserDeletingRow(sender As Object, e As DataGridViewRowCancelEventArgs)
            If IsMedicationRowLocked(e.Row) Then
                MessageBox.Show("This medication has already been dispensed and can't be removed here.", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                e.Cancel = True
            End If
        End Sub

        Private Function SafeStr(v As Object) As String
            Return If(IsDBNull(v), "", v.ToString())
        End Function

        Private Sub BtnSaveTriage_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtTemp.Text) Then
                MessageBox.Show("Temperature is required to save triage.", "Missing Vitals", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Try
                ' Both writes represent the same triage event -- if the connection drops between
                ' them, the encounter would show new triage vitals while the patient's "latest
                ' vitals" cache silently keeps the old ones (or vice versa).
                Using tx As New DbTransaction()
                    tx.Execute(
                        "UPDATE encounters SET
                            triage_temp_c = @Temp, triage_bp_systolic = @BpSys, triage_bp_diastolic = @BpDia,
                            triage_heart_rate = @Hr, triage_respiratory_rate = @Rr, triage_spo2 = @Spo2,
                            triage_weight_kg = @Weight, triage_height_cm = @Height, triage_bmi = @Bmi,
                            triage_pain_scale = @Pain, triage_general_appearance = @General, triage_classification = @Class,
                            triage_by = @TriageBy, triage_at = NOW(), status = IF(status = 'Open', 'In Progress', status), updated_at = NOW()
                         WHERE encounter_id = @Id",
                        New With {
                            .Temp = ParseOrNull(txtTemp.Text), .BpSys = ParseIntOrNull(txtBpSys.Text), .BpDia = ParseIntOrNull(txtBpDia.Text),
                            .Hr = ParseIntOrNull(txtHr.Text), .Rr = ParseIntOrNull(txtRr.Text), .Spo2 = ParseOrNull(txtSpo2.Text),
                            .Weight = ParseOrNull(txtWeight.Text), .Height = ParseOrNull(txtHeight.Text), .Bmi = ParseBmiOrNull(),
                            .Pain = CInt(numPain.Value), .General = EmptyToNull(txtGeneralAppearance.Text), .Class = EmptyToNull(cboTriageClass.Text),
                            .TriageBy = Session.Current.UserId, .Id = encounterId
                        })

                    tx.Execute(
                        "UPDATE patients SET height_cm=@Height, weight_kg=@Weight, respiratory_rate=@Rr, heart_rate=@Hr, temp_celsius=@Temp
                         WHERE patient_id = @PatientId",
                        New With {.Height = ParseOrNull(txtHeight.Text), .Weight = ParseOrNull(txtWeight.Text), .Rr = ParseIntOrNull(txtRr.Text), .Hr = ParseIntOrNull(txtHr.Text), .Temp = ParseOrNull(txtTemp.Text), .PatientId = patientId})

                    tx.Commit()
                End Using

                AuditLogger.Log("UPDATE", "triage", $"Encounter #{encounterId}", "Saved triage vitals")
                MessageBox.Show("Triage vitals saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                lblStatus.Text = "In Progress"
                innerNav.SelectedIndex = 1
            Catch ex As Exception
                MessageBox.Show("Could not save triage: " & ex.Message)
            End Try
        End Sub

        Private Function ParseOrNull(s As String) As Object
            Dim v As Double
            If Double.TryParse(s, v) Then Return v
            Return DBNull.Value
        End Function

        Private Function ParseIntOrNull(s As String) As Object
            Dim v As Integer
            If Integer.TryParse(s, v) Then Return v
            Return DBNull.Value
        End Function

        Private Function ParseBmiOrNull() As Object
            Dim w, h As Double
            If Double.TryParse(txtWeight.Text, w) AndAlso Double.TryParse(txtHeight.Text, h) AndAlso h > 0 Then
                Dim hm = h / 100.0
                Return Math.Round(w / (hm * hm), 2)
            End If
            Return DBNull.Value
        End Function

        Private Function EmptyToNull(s As String) As Object
            Return If(String.IsNullOrWhiteSpace(s), CType(DBNull.Value, Object), s.Trim())
        End Function

        Private Sub BtnSaveConsultation_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtChiefComplaint.Text) Then
                MessageBox.Show("Chief complaint is required.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Try
                SaveConsultationCore(status:="Draft")
                MessageBox.Show("Consultation draft saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Could not save consultation: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' The consultation upsert, diagnoses reconcile, medications
        ''' reconcile, and encounter update all happen in one transaction --
        ''' otherwise a crash or dropped connection partway through (e.g.
        ''' after diagnoses save but before medications save) leaves the
        ''' encounter in an inconsistent half-saved state.
        ''' </summary>
        Private Sub SaveConsultationCore(status As String)
            Dim followUpDate As Object = If(chkNoFollowUp.Checked, CType(DBNull.Value, Object), dtpFollowUp.Value.Date)

            Using tx As New DbTransaction()
                If Not consultationId.HasValue Then
                    Dim newId = tx.ExecuteInsertReturnId(
                        "INSERT INTO consultations (encounter_id, doctor_id, consultation_date, chief_complaint, history_of_present_illness,
                            past_medical_history, family_history, social_history, current_medications,
                            pe_general, pe_heent, pe_cardiovascular, pe_respiratory, pe_abdomen, pe_extremities, pe_neurological,
                            treatment_plan, follow_up_date, follow_up_instructions, status)
                         VALUES (@EncounterId, @DoctorId, NOW(), @ChiefComplaint, @Hpi, @Past, @Family, @Social, @CurrentMeds,
                            @PeGeneral, @PeHeent, @PeCardio, @PeRespi, @PeAbdomen, @PeExtrem, @PeNeuro,
                            @TreatmentPlan, @FollowUpDate, @FollowUpInstructions, @Status)",
                        ConsultationParams(followUpDate, status))
                    consultationId = CInt(newId)
                Else
                    tx.Execute(
                        "UPDATE consultations SET chief_complaint=@ChiefComplaint, history_of_present_illness=@Hpi,
                            past_medical_history=@Past, family_history=@Family, social_history=@Social, current_medications=@CurrentMeds,
                            pe_general=@PeGeneral, pe_heent=@PeHeent, pe_cardiovascular=@PeCardio, pe_respiratory=@PeRespi,
                            pe_abdomen=@PeAbdomen, pe_extremities=@PeExtrem, pe_neurological=@PeNeuro,
                            treatment_plan=@TreatmentPlan, follow_up_date=@FollowUpDate, follow_up_instructions=@FollowUpInstructions, status=@Status
                         WHERE consultation_id=@ConsultationId",
                        ConsultationParamsForUpdate(followUpDate, status))
                End If

                SaveDiagnoses(tx)
                SaveMedications(tx)

                tx.Execute("UPDATE encounters SET chief_complaint=@ChiefComplaint, updated_at=NOW() WHERE encounter_id=@Id",
                    New With {.ChiefComplaint = txtChiefComplaint.Text.Trim(), .Id = encounterId})

                tx.Commit()
            End Using

            AuditLogger.Log("UPDATE", "consultation", $"Encounter #{encounterId}", $"Saved consultation ({status})")
        End Sub

        Private Function ConsultationParams(followUpDate As Object, status As String) As Object
            Return New With {
                .EncounterId = encounterId, .DoctorId = Session.Current.UserId, .ChiefComplaint = txtChiefComplaint.Text.Trim(),
                .Hpi = EmptyToNull(txtHpi.Text), .Past = EmptyToNull(txtPastHistory.Text), .Family = EmptyToNull(txtFamilyHistory.Text),
                .Social = EmptyToNull(txtSocialHistory.Text), .CurrentMeds = EmptyToNull(txtCurrentMeds.Text),
                .PeGeneral = EmptyToNull(txtPeGeneral.Text), .PeHeent = EmptyToNull(txtPeHeent.Text), .PeCardio = EmptyToNull(txtPeCardio.Text),
                .PeRespi = EmptyToNull(txtPeRespi.Text), .PeAbdomen = EmptyToNull(txtPeAbdomen.Text), .PeExtrem = EmptyToNull(txtPeExtrem.Text),
                .PeNeuro = EmptyToNull(txtPeNeuro.Text), .TreatmentPlan = EmptyToNull(txtTreatmentPlan.Text),
                .FollowUpDate = followUpDate, .FollowUpInstructions = EmptyToNull(txtFollowUpInstructions.Text), .Status = status
            }
        End Function

        Private Function ConsultationParamsForUpdate(followUpDate As Object, status As String) As Object
            Return New With {
                .ChiefComplaint = txtChiefComplaint.Text.Trim(),
                .Hpi = EmptyToNull(txtHpi.Text), .Past = EmptyToNull(txtPastHistory.Text), .Family = EmptyToNull(txtFamilyHistory.Text),
                .Social = EmptyToNull(txtSocialHistory.Text), .CurrentMeds = EmptyToNull(txtCurrentMeds.Text),
                .PeGeneral = EmptyToNull(txtPeGeneral.Text), .PeHeent = EmptyToNull(txtPeHeent.Text), .PeCardio = EmptyToNull(txtPeCardio.Text),
                .PeRespi = EmptyToNull(txtPeRespi.Text), .PeAbdomen = EmptyToNull(txtPeAbdomen.Text), .PeExtrem = EmptyToNull(txtPeExtrem.Text),
                .PeNeuro = EmptyToNull(txtPeNeuro.Text), .TreatmentPlan = EmptyToNull(txtTreatmentPlan.Text),
                .FollowUpDate = followUpDate, .FollowUpInstructions = EmptyToNull(txtFollowUpInstructions.Text), .Status = status,
                .ConsultationId = consultationId.Value
            }
        End Function

        Private Sub SaveDiagnoses(tx As DbTransaction)
            If Not consultationId.HasValue Then Return
            tx.Execute("DELETE FROM diagnoses WHERE consultation_id = @Id", New With {.Id = consultationId.Value})
            For Each row As DataGridViewRow In gridDiagnoses.Rows
                If row.IsNewRow Then Continue For
                Dim desc = TryCellString(row, "diagnosis_desc")
                If String.IsNullOrWhiteSpace(desc) Then Continue For
                tx.Execute(
                    "INSERT INTO diagnoses (consultation_id, icd_code, diagnosis_desc, classification, status)
                     VALUES (@ConsultationId, @IcdCode, @Desc, @Classification, @Status)",
                    New With {
                        .ConsultationId = consultationId.Value,
                        .IcdCode = EmptyToNull(TryCellString(row, "icd_code")),
                        .Desc = desc.Trim(),
                        .Classification = EmptyToNull(TryCellString(row, "classification")),
                        .Status = If(String.IsNullOrWhiteSpace(TryCellString(row, "status")), "Active", TryCellString(row, "status"))
                    })
            Next
        End Sub

        ''' <summary>
        ''' Reconciles the grid against the DB instead of blindly deleting and
        ''' re-inserting every row: a prescription that's already been
        ''' dispensed has a pharmacy_dispensing row pointing at it
        ''' (ON DELETE RESTRICT), so a blanket DELETE would throw the moment
        ''' any medication from this encounter had been picked up at the
        ''' pharmacy, blocking every future save of this encounter. Locked
        ''' (already-dispensed) rows are left untouched entirely; only
        ''' still-Pending rows are removed/updated/inserted based on the grid.
        ''' </summary>
        Private Sub SaveMedications(tx As DbTransaction)
            Dim keepIds As New List(Of Integer)
            For Each row As DataGridViewRow In gridMedications.Rows
                If row.IsNewRow Then Continue For
                Dim rxIdStr = TryCellString(row, "prescription_id")
                Dim rxId As Integer
                If Integer.TryParse(rxIdStr, rxId) Then keepIds.Add(rxId)
            Next

            If keepIds.Count > 0 Then
                tx.Execute(
                    $"DELETE FROM prescriptions_medication_orders
                      WHERE encounter_id = @Id AND status = 'Pending'
                        AND prescription_id NOT IN ({String.Join(",", keepIds)})",
                    New With {.Id = encounterId})
            Else
                tx.Execute("DELETE FROM prescriptions_medication_orders WHERE encounter_id = @Id AND status = 'Pending'", New With {.Id = encounterId})
            End If

            For Each row As DataGridViewRow In gridMedications.Rows
                If row.IsNewRow Then Continue For
                If IsMedicationRowLocked(row) Then Continue For ' already dispensed -- never touch

                Dim name = TryCellString(row, "medicine_name")
                If String.IsNullOrWhiteSpace(name) Then Continue For
                Dim qty As Integer
                Integer.TryParse(TryCellString(row, "quantity"), qty)
                If qty <= 0 Then qty = 1

                Dim rxIdStr = TryCellString(row, "prescription_id")
                Dim rxId As Integer
                Dim isExisting = Integer.TryParse(rxIdStr, rxId)

                If isExisting Then
                    tx.Execute(
                        "UPDATE prescriptions_medication_orders SET medicine_name=@MedicineName, dosage=@Dosage, frequency=@Frequency, quantity=@Quantity, instructions=@Instructions
                         WHERE prescription_id = @Id AND status = 'Pending'",
                        New With {
                            .MedicineName = name.Trim(), .Dosage = EmptyToNull(TryCellString(row, "dosage")), .Frequency = EmptyToNull(TryCellString(row, "frequency")),
                            .Quantity = qty, .Instructions = EmptyToNull(TryCellString(row, "instructions")), .Id = rxId
                        })
                Else
                    Dim newId = tx.ExecuteInsertReturnId(
                        "INSERT INTO prescriptions_medication_orders (prescription_number, encounter_id, doctor_id, medication_date, medicine_name, dosage, frequency, quantity, instructions, status)
                         VALUES (@RxNo, @EncounterId, @DoctorId, NOW(), @MedicineName, @Dosage, @Frequency, @Quantity, @Instructions, 'Pending')",
                        New With {
                            .RxNo = IdGenerator.NextPrescriptionNo(), .EncounterId = encounterId, .DoctorId = Session.Current.UserId,
                            .MedicineName = name.Trim(), .Dosage = EmptyToNull(TryCellString(row, "dosage")), .Frequency = EmptyToNull(TryCellString(row, "frequency")),
                            .Quantity = qty, .Instructions = EmptyToNull(TryCellString(row, "instructions"))
                        })
                    row.Cells("prescription_id").Value = CInt(newId)
                    row.Cells("status").Value = "Pending"
                End If
            Next
        End Sub

        Private Function TryCellString(row As DataGridViewRow, columnName As String) As String
            If row.Cells(columnName).Value Is Nothing Then Return ""
            Return row.Cells(columnName).Value.ToString()
        End Function

        Private Sub BtnRequestLab_Click(sender As Object, e As EventArgs)
            Using dlg As New LabRequestDialog(encounterId, lblBanner.Text)
                dlg.ShowDialog(Me)
                If dlg.RequestsSubmitted Then
                    MessageBox.Show("Laboratory request submitted.", "Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End Using
        End Sub

        Private Sub BtnMedicalCertificate_Click(sender As Object, e As EventArgs)
            Using dlg As New IssueCertificateDialog(encounterId, lblBanner.Text)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.IssuedDocumentId.HasValue Then
                    Using view As New CertificateViewDialog(dlg.IssuedDocumentId.Value)
                        view.ShowDialog(Me)
                    End Using
                End If
            End Using
        End Sub

        Private Sub BtnCompleteEncounter_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtChiefComplaint.Text) Then
                MessageBox.Show("Chief complaint is required before completing the encounter.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                innerNav.SelectedIndex = 1
                Return
            End If
            Dim confirm = MessageBox.Show("Complete this encounter? It will be marked Closed and consultation notes finalized.", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If confirm <> DialogResult.Yes Then Return

            Try
                SaveConsultationCore(status:="Completed")
                Db.Execute("UPDATE encounters SET status='Closed', updated_at=NOW() WHERE encounter_id=@Id", New With {.Id = encounterId})
                AuditLogger.Log("UPDATE", "encounters", $"Encounter #{encounterId}", "Encounter completed/closed")
                lblStatus.Text = "Closed"
                MessageBox.Show("Encounter completed.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Could not complete encounter: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
