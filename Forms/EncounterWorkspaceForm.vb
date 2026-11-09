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
        Implements IConfirmLeave

        Private triageChanges, consultChanges As ChangeTracker
        Private tabTriage, tabConsult As TabPage

        Private ReadOnly encounterId As Integer
        Private patientId As Integer
        Private consultationId As Integer? = Nothing

        Private lblBanner, lblBannerMeta, lblTags As Label
        Private innerNav As PillTabControl

        ' Triage controls
        Private txtTemp, txtBpSys, txtBpDia, txtHr, txtRr, txtSpo2, txtWeight, txtHeight As TextBox
        Private lblBmi As Label
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
        Private btnComplete As Button
        Private lblCompleteHint As Label
        Private triageDone As Boolean = False
        Private encounterClosed As Boolean = False

        Public Sub New(encounterId As Integer)
            Me.encounterId = encounterId
            InitializeComponent()
            LoadEncounter()
            ' Start watching for edits only after the saved data has been filled in.
            triageChanges = UiTheme.TrackChanges(tabTriage)
            consultChanges = UiTheme.TrackChanges(tabConsult)
        End Sub

        ''' <summary>Asked by the main window before Back / sidebar / log out: unsaved triage or consultation notes would be lost.</summary>
        Public Function ConfirmLeave() As Boolean Implements IConfirmLeave.ConfirmLeave
            If encounterClosed Then Return True
            Dim unsaved As New List(Of String)
            If triageChanges IsNot Nothing AndAlso triageChanges.IsDirty Then unsaved.Add("triage vitals")
            If consultChanges IsNot Nothing AndAlso consultChanges.IsDirty Then unsaved.Add("consultation notes (saved only when the encounter is completed)")
            If unsaved.Count = 0 Then Return True
            Return UiTheme.ConfirmDiscard(Me, "the unsaved " & String.Join(" and ", unsaved))
        End Function

        Private Sub InitializeComponent()
            Me.BackColor = ClinicalUi.PageBg
            Me.Font = UiTheme.BaseFont
            Me.Padding = New Padding(20, 16, 20, 12)

            ' Dock order: later-added Top controls sit higher -> gap spacer first, then the patient banner.
            Me.Controls.Add(New Panel With {.Dock = DockStyle.Top, .Height = 14})

            ' ---- Patient summary strip (white card) ----
            Dim banner As New Panel With {.Dock = DockStyle.Top, .Height = 92, .BackColor = Color.White}
            ClinicalUi.SetDoubleBuffered(banner)
            AddHandler banner.Paint,
                Sub(s, e)
                    Using pen As New Pen(ClinicalUi.CardBorder)
                        e.Graphics.DrawRectangle(pen, 0, 0, banner.Width - 1, banner.Height - 1)
                    End Using
                End Sub
            Me.Controls.Add(banner)
            lblBanner = New Label With {.Font = New Font("Segoe UI Semibold", 13.5F), .ForeColor = ClinicalUi.TextMain, .AutoSize = True, .Location = New Point(20, 12)}
            banner.Controls.Add(lblBanner)
            lblBannerMeta = New Label With {.Font = New Font("Segoe UI", 9.0F), .ForeColor = ClinicalUi.TextSoft, .AutoSize = True, .Location = New Point(20, 42)}
            banner.Controls.Add(lblBannerMeta)
            lblTags = New Label With {.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .ForeColor = ClinicalUi.DangerText, .AutoSize = True, .Location = New Point(20, 64)}
            banner.Controls.Add(lblTags)
            lblStatus = New Label With {.ForeColor = ClinicalUi.Green, .Font = New Font("Segoe UI Semibold", 10.0F), .AutoSize = True}
            banner.Controls.Add(lblStatus)
            UiTheme.PinToRight(lblStatus, banner, 20, 14)

            innerNav = New PillTabControl With {.Dock = DockStyle.Fill, .PageBackColor = ClinicalUi.PageBg}
            Me.Controls.Add(innerNav)
            innerNav.BringToFront()

            tabTriage = New TabPage("Triage") With {.BackColor = ClinicalUi.PageBg, .UseVisualStyleBackColor = False, .Padding = New Padding(0)}
            innerNav.TabPages.Add(tabTriage)
            BuildTriageTab(tabTriage)

            tabConsult = New TabPage("Consultation") With {.BackColor = ClinicalUi.PageBg, .UseVisualStyleBackColor = False, .Padding = New Padding(0)}
            innerNav.TabPages.Add(tabConsult)
            BuildConsultationTab(tabConsult)

            Dim actionBar As New Panel With {.Dock = DockStyle.Bottom, .Height = 62, .BackColor = Color.White}
            ClinicalUi.SetDoubleBuffered(actionBar)
            AddHandler actionBar.Paint,
                Sub(s, e)
                    Using pen As New Pen(ClinicalUi.CardBorder)
                        e.Graphics.DrawLine(pen, 0, 0, actionBar.Width, 0)
                    End Using
                End Sub
            Me.Controls.Add(actionBar)

            ' Consultation can't be opened until triage has been saved.
            AddHandler innerNav.Selecting,
                Sub(s, e)
                    If e.TabPageIndex = 1 AndAlso Not triageDone Then
                        e.Cancel = True
                        MessageBox.Show("Save the patient's triage vitals first -- the consultation opens only after triage.", "Triage Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End Sub

            Dim btnRequestLab As New Button With {.Text = "Request Lab", .Size = New Size(130, 38)}
            ClinicalUi.StyleSecondary(btnRequestLab)
            AddHandler btnRequestLab.Click, AddressOf BtnRequestLab_Click
            actionBar.Controls.Add(btnRequestLab)

            Dim btnCertificate As New Button With {.Text = "Medical Certificate", .Size = New Size(170, 38)}
            ClinicalUi.StyleSecondary(btnCertificate)
            AddHandler btnCertificate.Click, AddressOf BtnMedicalCertificate_Click
            actionBar.Controls.Add(btnCertificate)

            UiTheme.PinRowToRight(actionBar, 12, 20, 10, btnRequestLab, btnCertificate)
        End Sub

        ''' <summary>White footer strip with a top divider, used under the triage and consultation pages.</summary>
        Private Shared Function NewFooter() As Panel
            Dim footer As New Panel With {.Dock = DockStyle.Bottom, .Height = 64, .BackColor = Color.White}
            ClinicalUi.SetDoubleBuffered(footer)
            AddHandler footer.Paint,
                Sub(s, e)
                    Using pen As New Pen(ClinicalUi.CardBorder)
                        e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0)
                    End Using
                End Sub
            Return footer
        End Function

        ' ---------------- Triage tab (Figure 23) ----------------
        Private Sub BuildTriageTab(tab As TabPage)
            Dim left As TableLayoutPanel = Nothing
            Dim right As TableLayoutPanel = Nothing
            Dim page = ClinicalUi.TwoColumnPage(left, right)

            ' Left column: one Vital Signs card (BP systolic/diastolic side by side, units inside the inputs, weight + height paired).
            Dim vitals = ClinicalUi.AddCard(left, "Vital Signs")
            Dim g = ClinicalUi.NewGrid(vitals, 2)
            txtTemp = New TextBox() : ClinicalUi.AddField(g, "Temperature *", ClinicalUi.InputWrap(txtTemp, ChrW(176) & "C"))
            txtBpSys = New TextBox() : txtBpDia = New TextBox()
            ClinicalUi.AddBloodPressure(g, "BP Systolic", txtBpSys, "BP Diastolic", txtBpDia)
            txtHr = New TextBox() : ClinicalUi.AddField(g, "Heart Rate", ClinicalUi.InputWrap(txtHr, "bpm"))
            txtRr = New TextBox() : ClinicalUi.AddField(g, "Respiratory Rate", ClinicalUi.InputWrap(txtRr, "/min"))
            txtWeight = New TextBox() : ClinicalUi.AddField(g, "Weight", ClinicalUi.InputWrap(txtWeight, "kg"))
            txtHeight = New TextBox() : ClinicalUi.AddField(g, "Height", ClinicalUi.InputWrap(txtHeight, "cm"))
            AddHandler txtWeight.TextChanged, Sub() RecalculateBmi()
            AddHandler txtHeight.TextChanged, Sub() RecalculateBmi()
            txtSpo2 = New TextBox() : ClinicalUi.AddField(g, "O2 Saturation", ClinicalUi.InputWrap(txtSpo2, "%"))
            lblBmi = New Label() : ClinicalUi.AddField(g, "BMI (auto)", ClinicalUi.ReadOnlyWrap(lblBmi))

            ' Right column: stacked cards.
            Dim appearanceCard = ClinicalUi.AddCard(right, "General Appearance")
            txtGeneralAppearance = New TextBox With {.Multiline = True}
            ClinicalUi.AddRow(appearanceCard, ClinicalUi.InputWrap(txtGeneralAppearance, Nothing, 96))

            Dim classCard = ClinicalUi.AddCard(right, "Triage Classification", True)
            cboTriageClass = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Font = ClinicalUi.InputFont}
            cboTriageClass.Items.AddRange({"Non-urgent", "Priority", "Emergency"})
            ClinicalUi.AddRow(classCard, cboTriageClass)

            ' Footer with the primary action on the right.
            Dim footer = NewFooter()
            Dim btnSaveTriage As New Button With {.Text = "Save Triage", .Size = New Size(150, 38)}
            ClinicalUi.StylePrimary(btnSaveTriage)
            AddHandler btnSaveTriage.Click, AddressOf BtnSaveTriage_Click
            footer.Controls.Add(btnSaveTriage)
            UiTheme.PinToRight(btnSaveTriage, footer, 20, 13)

            tab.Controls.Add(footer)
            tab.Controls.Add(page)
            page.BringToFront()
        End Sub

        Private Sub RecalculateBmi()
            Dim w, h As Double
            If Double.TryParse(txtWeight.Text, w) AndAlso Double.TryParse(txtHeight.Text, h) AndAlso h > 0 Then
                Dim heightM = h / 100.0
                Dim bmi = w / (heightM * heightM)
                Dim category = If(bmi < 18.5, "Underweight", If(bmi < 25, "Normal", If(bmi < 30, "Overweight", "Obese")))
                lblBmi.Text = $"{bmi:0.0} kg/m2 - {category}"
                ' Colour-only cue: normal stays dark, out-of-range readings turn amber/red.
                lblBmi.ForeColor = If(category = "Normal", ClinicalUi.TextMain, If(category = "Obese", ClinicalUi.DangerText, ClinicalUi.WarnText))
            Else
                lblBmi.Text = ""
            End If
        End Sub

        ' ---------------- Consultation tab (Figures 24-29) ----------------
        ''' <summary>
        ''' One page of cards instead of six sub-tabs: left column = subjective + history + exam, right column =
        ''' diagnoses, plan and prescriptions. Every field/grid is the same control as before.
        ''' </summary>
        Private Sub BuildConsultationTab(tab As TabPage)
            Dim left As TableLayoutPanel = Nothing
            Dim right As TableLayoutPanel = Nothing
            Dim page = ClinicalUi.TwoColumnPage(left, right)

            ' -- History / Subjective (Chief Complaint) --
            Dim ccCard = ClinicalUi.AddCard(left, "History / Subjective", False, 1)
            Dim gCC = ClinicalUi.NewGrid(ccCard, 1)
            txtChiefComplaint = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gCC, "Chief Complaint *", ClinicalUi.InputWrap(txtChiefComplaint, Nothing, 76))
            txtHpi = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gCC, "History of Present Illness", ClinicalUi.InputWrap(txtHpi, Nothing, 110))

            ' -- Medical History --
            Dim histCard = ClinicalUi.AddCard(left, "Medical History", False, 2)
            Dim gHist = ClinicalUi.NewGrid(histCard, 2)
            txtPastHistory = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gHist, "Past Medical History", ClinicalUi.InputWrap(txtPastHistory, Nothing, 84))
            txtFamilyHistory = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gHist, "Family History", ClinicalUi.InputWrap(txtFamilyHistory, Nothing, 84))
            txtSocialHistory = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gHist, "Social History", ClinicalUi.InputWrap(txtSocialHistory, Nothing, 84))
            txtCurrentMeds = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gHist, "Current Medications", ClinicalUi.InputWrap(txtCurrentMeds, Nothing, 84))

            ' -- Physical Exam / Objective --
            Dim examCard = ClinicalUi.AddCard(left, "Physical Exam / Objective", False, 3)
            Dim gExam = ClinicalUi.NewGrid(examCard, 2)
            txtPeGeneral = ExamBox(gExam, "General")
            txtPeHeent = ExamBox(gExam, "HEENT")
            txtPeCardio = ExamBox(gExam, "Cardiovascular")
            txtPeRespi = ExamBox(gExam, "Respiratory")
            txtPeAbdomen = ExamBox(gExam, "Abdomen")
            txtPeExtrem = ExamBox(gExam, "Extremities")
            txtPeNeuro = ExamBox(gExam, "Neurological")

            ' -- Assessment / Diagnoses (Figure 27) --
            Dim diagCard = ClinicalUi.AddCard(right, "Current Diagnoses", False, 4)
            gridDiagnoses = New DataGridView()
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
            ClinicalUi.StyleClinicalGrid(gridDiagnoses)
            ClinicalUi.AddRow(diagCard, ClinicalUi.GridHost(gridDiagnoses, 220))

            ' -- Treatment Plan --
            Dim planCard = ClinicalUi.AddCard(right, "Plan / Treatment", False, 5)
            Dim gPlan = ClinicalUi.NewGrid(planCard, 2)
            txtTreatmentPlan = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gPlan, "Treatment Plan", ClinicalUi.InputWrap(txtTreatmentPlan, Nothing, 100), 2)
            chkNoFollowUp = New CheckBox With {.Text = "No follow-up needed", .AutoSize = True, .Margin = New Padding(0, 0, 0, 12)}
            gPlan.Controls.Add(chkNoFollowUp)
            gPlan.SetColumnSpan(chkNoFollowUp, 2)
            dtpFollowUp = New DateTimePicker With {.Format = DateTimePickerFormat.Short}
            ClinicalUi.AddField(gPlan, "Follow-up Date", dtpFollowUp)
            AddHandler chkNoFollowUp.CheckedChanged, Sub() dtpFollowUp.Enabled = Not chkNoFollowUp.Checked
            txtFollowUpInstructions = New TextBox With {.Multiline = True}
            ClinicalUi.AddField(gPlan, "Follow-up Instructions", ClinicalUi.InputWrap(txtFollowUpInstructions, Nothing, 76))

            ' -- Medications (Figure 29) --
            Dim medCard = ClinicalUi.AddCard(right, "Prescribed Medications", False, 6)
            gridMedications = New DataGridView()
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
            Dim colRxId As New DataGridViewTextBoxColumn With {.Name = "prescription_id", .Visible = False}
            gridMedications.Columns.Add(colRxId)
            gridMedications.Columns("medicine_name").FillWeight = 160
            gridMedications.Columns("instructions").FillWeight = 200
            ClinicalUi.StyleClinicalGrid(gridMedications)
            gridMedications.Columns("status").DefaultCellStyle.ForeColor = ClinicalUi.TextSoft
            AddHandler gridMedications.CellBeginEdit, AddressOf GridMedications_CellBeginEdit
            AddHandler gridMedications.UserDeletingRow, AddressOf GridMedications_UserDeletingRow
            ClinicalUi.AddRow(medCard, ClinicalUi.GridHost(gridMedications, 240))

            ' Complete Encounter, in a footer under the cards; enabled only once the consultation is finished.
            Dim pnlSave = NewFooter()
            btnComplete = New Button With {.Text = "Complete Encounter", .Size = New Size(190, 38), .Location = New Point(20, 13), .Enabled = False}
            ClinicalUi.StylePrimary(btnComplete)
            btnComplete.BackColor = DisabledGreen
            AddHandler btnComplete.EnabledChanged, Sub() btnComplete.BackColor = If(btnComplete.Enabled, ClinicalUi.Green, DisabledGreen)
            AddHandler btnComplete.Click, AddressOf BtnCompleteEncounter_Click
            pnlSave.Controls.Add(btnComplete)
            lblCompleteHint = New Label With {.ForeColor = ClinicalUi.TextSoft, .AutoSize = True, .Location = New Point(224, 24)}
            pnlSave.Controls.Add(lblCompleteHint)

            tab.Controls.Add(pnlSave)
            tab.Controls.Add(page)
            page.BringToFront()

            AddHandler txtChiefComplaint.TextChanged, Sub() UpdateCompleteButton()
            AddHandler txtTreatmentPlan.TextChanged, Sub() UpdateCompleteButton()
            AddHandler gridDiagnoses.CellValueChanged, Sub() UpdateCompleteButton()
            AddHandler gridDiagnoses.RowsRemoved, Sub() UpdateCompleteButton()
        End Sub

        Private Shared ReadOnly DisabledGreen As Color = Color.FromArgb(176, 196, 184)

        Private Function ExamBox(grid As TableLayoutPanel, caption As String) As TextBox
            Dim tb As New TextBox With {.Multiline = True}
            ClinicalUi.AddField(grid, caption, ClinicalUi.InputWrap(tb, Nothing, 76))
            Return tb
        End Function

        ''' <summary>
        ''' The consultation is finished when it has a chief complaint, at least one diagnosis
        ''' and a treatment plan (after triage, and while the encounter is still open).
        ''' </summary>
        Private Sub UpdateCompleteButton()
            If btnComplete Is Nothing Then Return
            Dim missing As New List(Of String)
            If String.IsNullOrWhiteSpace(txtChiefComplaint.Text) Then missing.Add("chief complaint")
            Dim hasDiagnosis = gridDiagnoses.Rows.Cast(Of DataGridViewRow)().
                Any(Function(r) Not r.IsNewRow AndAlso Not String.IsNullOrWhiteSpace(TryCellString(r, "diagnosis_desc")))
            If Not hasDiagnosis Then missing.Add("diagnosis (Assessment)")
            If String.IsNullOrWhiteSpace(txtTreatmentPlan.Text) Then missing.Add("treatment plan")

            If encounterClosed Then
                btnComplete.Enabled = False
                lblCompleteHint.Text = "This encounter is already completed."
            ElseIf Not triageDone Then
                btnComplete.Enabled = False
                lblCompleteHint.Text = "Save triage first."
            ElseIf missing.Count > 0 Then
                btnComplete.Enabled = False
                lblCompleteHint.Text = "To complete, fill in: " & String.Join(", ", missing) & "."
            Else
                btnComplete.Enabled = True
                lblCompleteHint.Text = ""
            End If
        End Sub

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
                If Not IsDBNull(r("triage_general_appearance")) Then txtGeneralAppearance.Text = r("triage_general_appearance").ToString()
                If Not IsDBNull(r("triage_classification")) Then cboTriageClass.SelectedItem = r("triage_classification").ToString()
                If Not IsDBNull(r("chief_complaint")) Then txtChiefComplaint.Text = r("chief_complaint").ToString()

                triageDone = Not IsDBNull(r("triage_at")) OrElse Not IsDBNull(r("triage_temp_c"))
                encounterClosed = r("status").ToString() = "Closed"

                LoadConsultation()
                LoadMedications()
                UpdateCompleteButton()
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
            If Not UiTheme.Confirm(Me, "Save these triage vitals?", "Save Triage") Then Return
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
                            triage_general_appearance = @General, triage_classification = @Class,
                            triage_by = @TriageBy, triage_at = NOW(), status = IF(status = 'Open', 'In Progress', status), updated_at = NOW()
                         WHERE encounter_id = @Id",
                        New With {
                            .Temp = ParseOrNull(txtTemp.Text), .BpSys = ParseIntOrNull(txtBpSys.Text), .BpDia = ParseIntOrNull(txtBpDia.Text),
                            .Hr = ParseIntOrNull(txtHr.Text), .Rr = ParseIntOrNull(txtRr.Text), .Spo2 = ParseOrNull(txtSpo2.Text),
                            .Weight = ParseOrNull(txtWeight.Text), .Height = ParseOrNull(txtHeight.Text), .Bmi = ParseBmiOrNull(),
                            .General = EmptyToNull(txtGeneralAppearance.Text), .Class = EmptyToNull(cboTriageClass.Text),
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
                If Not encounterClosed Then lblStatus.Text = "In Progress"
                triageDone = True
                triageChanges?.Reset()
                UpdateCompleteButton()
                innerNav.SelectedIndex = 1
            Catch ex As Exception
                UiTheme.ShowError(Me, "save triage", ex)
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
                    MessageBox.Show("Laboratory request submitted. RHU Management will issue it to the RHU laboratory or refer it to an external laboratory.", "Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End Using
        End Sub

        Private Sub BtnMedicalCertificate_Click(sender As Object, e As EventArgs)
            Using dlg As New IssueCertificateDialog(encounterId, lblBanner.Text)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.IssuedDocumentId.HasValue Then
                    MessageBox.Show("Medical certificate request submitted. It can be printed once RHU Management approves it.", "Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End Using
        End Sub

        Private Sub BtnCompleteEncounter_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtChiefComplaint.Text) Then
                MessageBox.Show("Chief complaint is required before completing the encounter.", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                innerNav.SelectedIndex = 1
                Return
            End If
            If Not UiTheme.Confirm(Me, "Complete this encounter?" & vbCrLf & vbCrLf &
                                   "The consultation notes, diagnoses and medications will be saved and the encounter marked Closed. It can't be reopened.",
                                   "Complete Encounter") Then Return

            Try
                SaveConsultationCore(status:="Completed")
                Db.Execute("UPDATE encounters SET status='Closed', updated_at=NOW() WHERE encounter_id=@Id", New With {.Id = encounterId})
                AuditLogger.Log("UPDATE", "encounters", $"Encounter #{encounterId}", "Encounter completed/closed")
                lblStatus.Text = "Closed"
                encounterClosed = True
                triageChanges?.Reset() : consultChanges?.Reset()
                UpdateCompleteButton()
                UiTheme.ShowSuccess(Me, "Encounter completed.", "Done")
            Catch ex As Exception
                UiTheme.ShowError(Me, "complete the encounter", ex)
            End Try
        End Sub

    End Class

End Namespace
