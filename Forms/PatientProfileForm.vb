Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Patient profile / medical history hub (Figures 11-20).</summary>
    Public Class PatientProfileForm
        Inherits Form

        Private ReadOnly patientId As Integer
        Private ReadOnly onOpenEncounter As Action(Of Integer)

        ''' <summary>
        ''' Roles without clinical/encounter access (e.g. Records Staff, once
        ''' its module_access no longer includes "encounters") get a
        ''' summary-only profile: demographics + a read-only visit list, no
        ''' Medical History (consultations/diagnoses/lab/meds) and no way to
        ''' start or open a clinical encounter from here.
        ''' </summary>
        Private ReadOnly canAccessEncounters As Boolean

        Private lblName, lblMeta, lblConditions As Label
        Private tabs As TabControl
        Private gridEncounters, gridConsultations, gridDiagnoses, gridLabResults, gridMedications As DataGridView

        Private patientDisplayName As String = ""

        Public Sub New(patientId As Integer, onOpenEncounter As Action(Of Integer))
            Me.patientId = patientId
            Me.onOpenEncounter = onOpenEncounter
            Me.canAccessEncounters = Session.Current IsNot Nothing AndAlso Session.Current.HasModule("encounters")
            InitializeComponent()
            LoadPatient()
            LoadEncounters()
            If canAccessEncounters Then LoadMedicalHistory()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = UiTheme.BaseFont

            Dim card As New Panel With {.Dock = DockStyle.Top, .Height = 130, .BackColor = Color.White, .Padding = New Padding(20)}
            Me.Controls.Add(card)

            lblName = New Label With {.Font = New Font("Segoe UI", 15.0F, FontStyle.Bold), .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(20, 14)}
            card.Controls.Add(lblName)
            lblMeta = New Label With {.ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)}
            card.Controls.Add(lblMeta)
            lblConditions = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 68), .MaximumSize = New Size(700, 0)}
            card.Controls.Add(lblConditions)

            If canAccessEncounters Then
                Dim btnNewEncounter As New Button With {.Text = "+ New Encounter", .Size = New Size(160, 36)}
                UiTheme.StylePrimaryButton(btnNewEncounter)
                UiTheme.PinToRight(btnNewEncounter, card, 20, 20)
                AddHandler btnNewEncounter.Click, AddressOf BtnNewEncounter_Click
                card.Controls.Add(btnNewEncounter)
            End If

            Dim btnEdit As New Button With {.Text = "Edit Patient", .Size = New Size(120, 36)}
            UiTheme.StyleSecondaryButton(btnEdit)
            UiTheme.PinToRight(btnEdit, card, 20, If(canAccessEncounters, 64, 20))
            AddHandler btnEdit.Click, AddressOf BtnEdit_Click
            card.Controls.Add(btnEdit)

            tabs = New TabControl With {.Dock = DockStyle.Fill}
            Me.Controls.Add(tabs)
            tabs.BringToFront()

            Dim tabOverview As New TabPage("Overview")
            tabs.TabPages.Add(tabOverview)
            gridEncounters = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridEncounters)
            ' Records Staff (no "encounters" module access) get a read-only visit list here --
            ' drilling into the full clinical workspace (triage/consultation entry) isn't theirs to do.
            If canAccessEncounters Then AddHandler gridEncounters.CellDoubleClick, AddressOf GridEncounters_CellDoubleClick
            tabOverview.Controls.Add(gridEncounters)

            If Not canAccessEncounters Then Return

            Dim tabHistory As New TabPage("Medical History")
            tabs.TabPages.Add(tabHistory)
            Dim historyTabs As New TabControl With {.Dock = DockStyle.Fill}
            tabHistory.Controls.Add(historyTabs)

            Dim tConsult As New TabPage("Consultations")
            gridConsultations = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridConsultations)
            tConsult.Controls.Add(gridConsultations)
            historyTabs.TabPages.Add(tConsult)

            Dim tDiag As New TabPage("Diagnoses")
            gridDiagnoses = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridDiagnoses)
            tDiag.Controls.Add(gridDiagnoses)
            historyTabs.TabPages.Add(tDiag)

            Dim tLab As New TabPage("Lab Results")
            gridLabResults = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridLabResults)
            tLab.Controls.Add(gridLabResults)
            historyTabs.TabPages.Add(tLab)

            Dim tMeds As New TabPage("Medications")
            gridMedications = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridMedications)
            tMeds.Controls.Add(gridMedications)
            historyTabs.TabPages.Add(tMeds)
        End Sub

        Private Sub LoadPatient()
            Try
                Dim table = Db.Query(
                    "SELECT p.*, b.barangay_name FROM patients p JOIN barangays b ON b.barangay_id = p.barangay_id WHERE p.patient_id = @Id",
                    New With {.Id = patientId})
                If table.Rows.Count = 0 Then
                    lblName.Text = "Patient not found"
                    Return
                End If
                Dim r = table.Rows(0)
                patientDisplayName = $"{r("last_name")}, {r("first_name")} {r("middle_name")}".Trim()
                Dim age = DateTime.Today.Year - Convert.ToDateTime(r("date_of_birth")).Year
                lblName.Text = $"{patientDisplayName}   ({r("patient_no")})"
                lblMeta.Text = $"{age} years old - {r("sex")}   |   {If(IsDBNull(r("contact_number")), "no contact", r("contact_number"))}   |   {r("barangay_name")}, Vinzons"

                Dim flags As New List(Of String)
                If Not IsDBNull(r("existing_conditions")) AndAlso r("existing_conditions").ToString().Trim().Length > 0 Then flags.Add(r("existing_conditions").ToString())
                If Not IsDBNull(r("known_allergies")) AndAlso r("known_allergies").ToString().Trim().Length > 0 Then flags.Add("Allergy: " & r("known_allergies").ToString())
                lblConditions.Text = String.Join("   |   ", flags)
            Catch ex As Exception
                lblName.Text = "Error loading patient: " & ex.Message
            End Try
        End Sub

        Private Sub LoadEncounters()
            Try
                Dim table = Db.Query(
                    "SELECT e.encounter_id AS Id, e.encounter_no AS 'Encounter No.', e.encounter_date AS Date, e.visit_type AS 'Visit Type',
                            e.chief_complaint AS 'Chief Complaint', e.status AS Status, u.full_name AS 'Attending Staff'
                     FROM encounters e LEFT JOIN users_staff u ON u.user_id = e.created_by
                     WHERE e.patient_id = @Id ORDER BY e.encounter_date DESC",
                    New With {.Id = patientId})
                gridEncounters.DataSource = table
                If gridEncounters.Columns.Contains("Id") Then gridEncounters.Columns("Id").Visible = False
            Catch ex As Exception
                MessageBox.Show("Could not load encounters: " & ex.Message)
            End Try
        End Sub

        Private Sub LoadMedicalHistory()
            Try
                gridConsultations.DataSource = Db.Query(
                    "SELECT c.consultation_date AS Date, u.full_name AS Doctor, c.assessment AS Assessment, c.status AS Status
                     FROM consultations c JOIN encounters e ON e.encounter_id = c.encounter_id
                     JOIN users_staff u ON u.user_id = c.doctor_id
                     WHERE e.patient_id = @Id ORDER BY c.consultation_date DESC", New With {.Id = patientId})

                gridDiagnoses.DataSource = Db.Query(
                    "SELECT d.created_at AS Date, d.diagnosis_desc AS Diagnosis, d.classification AS Classification, d.status AS Status
                     FROM diagnoses d
                     JOIN consultations c ON c.consultation_id = d.consultation_id
                     JOIN encounters e ON e.encounter_id = c.encounter_id
                     WHERE e.patient_id = @Id ORDER BY d.created_at DESC", New With {.Id = patientId})

                gridLabResults.DataSource = Db.Query(
                    "SELECT l.request_date AS Requested, l.test_name AS Test, l.result AS Result, l.status AS Status
                     FROM laboratory l JOIN encounters e ON e.encounter_id = l.encounter_id
                     WHERE e.patient_id = @Id ORDER BY l.request_date DESC", New With {.Id = patientId})

                gridMedications.DataSource = Db.Query(
                    "SELECT rx.medication_date AS Date, rx.medicine_name AS Medicine, rx.dosage AS Dosage, rx.frequency AS Frequency, rx.status AS Status
                     FROM prescriptions_medication_orders rx JOIN encounters e ON e.encounter_id = rx.encounter_id
                     WHERE e.patient_id = @Id ORDER BY rx.medication_date DESC", New With {.Id = patientId})
            Catch ex As Exception
                MessageBox.Show("Could not load medical history: " & ex.Message)
            End Try
        End Sub

        Private Sub GridEncounters_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim id = Convert.ToInt32(gridEncounters.Rows(e.RowIndex).Cells("Id").Value)
            onOpenEncounter?.Invoke(id)
        End Sub

        Private Sub BtnNewEncounter_Click(sender As Object, e As EventArgs)
            Using dlg As New NewEncounterDialog(patientId, patientDisplayName)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.NewEncounterId.HasValue Then
                    onOpenEncounter?.Invoke(dlg.NewEncounterId.Value)
                End If
            End Using
        End Sub

        Private Sub BtnEdit_Click(sender As Object, e As EventArgs)
            Using dlg As New PatientRegistrationForm(patientId, Sub(id) LoadPatient())
                dlg.StartPosition = FormStartPosition.CenterParent
                dlg.Width = 1000 : dlg.Height = 700
                dlg.ShowDialog(Me)
            End Using
        End Sub

    End Class

End Namespace
