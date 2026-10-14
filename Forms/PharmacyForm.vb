Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Pharmacy dispensing worklist: pending prescriptions queue + dispensing history log.</summary>
    Public Class PharmacyForm
        Inherits Form

        Private gridPending, gridHistory As DataGridView
        Private txtSearch As TextBox
        Private lblCount As Label

        Public Sub New()
            InitializeComponent()
            LoadPending()
            LoadHistory()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)
            txtSearch = New TextBox With {.Location = New Point(0, 10), .Width = 300, .PlaceholderText = "Search patient or medicine..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadPending)
            toolbar.Controls.Add(txtSearch)

            Dim tabs As New TabControl With {.Dock = DockStyle.Fill}
            Me.Controls.Add(tabs)
            tabs.BringToFront()

            Dim tabPending As New TabPage("Pending Prescriptions")
            tabs.TabPages.Add(tabPending)
            gridPending = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridPending)
            AddHandler gridPending.CellDoubleClick, AddressOf GridPending_CellDoubleClick
            tabPending.Controls.Add(gridPending)

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            tabPending.Controls.Add(lblCount)
            Dim hint As New Label With {.Text = "Double-click a prescription to dispense it.", .Dock = DockStyle.Top, .Height = 22, .ForeColor = UiTheme.TextMuted}
            tabPending.Controls.Add(hint)

            Dim tabHistory As New TabPage("Dispensing History")
            tabs.TabPages.Add(tabHistory)
            gridHistory = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridHistory)
            tabHistory.Controls.Add(gridHistory)
        End Sub

        Private Sub LoadPending()
            Try
                Dim search = txtSearch.Text.Trim()
                Dim table = Db.Query(
                    $"SELECT rx.prescription_id AS Id, rx.medication_date AS Date, CONCAT(p.last_name, ', ', p.first_name) AS Patient,
                            p.patient_id AS PatientId, rx.medicine_name AS Medicine, rx.dosage AS Dosage, rx.frequency AS Frequency,
                            rx.quantity AS Prescribed, COALESCE((SELECT SUM(pd.quantity_dispensed) FROM pharmacy_dispensing pd WHERE pd.prescription_id = rx.prescription_id), 0) AS Dispensed,
                            rx.instructions AS Instructions, u.full_name AS Doctor, rx.status AS Status
                     FROM prescriptions_medication_orders rx
                     JOIN encounters e ON e.encounter_id = rx.encounter_id
                     JOIN patients p ON p.patient_id = e.patient_id
                     LEFT JOIN users_staff u ON u.user_id = rx.doctor_id
                     WHERE rx.status IN ('Pending','Partially Dispensed')
                       AND (@Search = '' OR p.first_name LIKE @SearchLike OR p.last_name LIKE @SearchLike OR rx.medicine_name LIKE @SearchLike)
                     ORDER BY rx.medication_date DESC
                     LIMIT {UiTheme.ResultCap}",
                    New With {.Search = search, .SearchLike = "%" & search & "%"})
                gridPending.DataSource = table
                For Each hidden In {"Id", "PatientId", "Instructions"}
                    If gridPending.Columns.Contains(hidden) Then gridPending.Columns(hidden).Visible = False
                Next
                lblCount.Text = "  " & UiTheme.FormatCountLabel(table.Rows.Count, "pending prescription")
            Catch ex As Exception
                lblCount.Text = "Error loading prescriptions: " & ex.Message
            End Try
        End Sub

        Private Sub LoadHistory()
            Try
                gridHistory.DataSource = Db.Query(
                    "SELECT pd.date_dispensed AS Date, CONCAT(p.last_name, ', ', p.first_name) AS Patient, pd.medicine_name AS Medicine,
                            pd.quantity_dispensed AS Quantity, pd.dosage AS Dosage, u.full_name AS 'Dispensed By'
                     FROM pharmacy_dispensing pd
                     JOIN patients p ON p.patient_id = pd.patient_id
                     LEFT JOIN users_staff u ON u.user_id = pd.dispensed_by
                     ORDER BY pd.date_dispensed DESC LIMIT 200")
            Catch ex As Exception
                UiTheme.ShowError(Me, "load dispensing history", ex)
            End Try
        End Sub

        Private Sub GridPending_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim row = gridPending.Rows(e.RowIndex)
            Dim prescriptionId = Convert.ToInt32(row.Cells("Id").Value)
            Dim patientId = Convert.ToInt32(row.Cells("PatientId").Value)
            Dim patientName = row.Cells("Patient").Value.ToString()
            Dim medicine = row.Cells("Medicine").Value.ToString()
            Dim dosage = If(IsDBNull(row.Cells("Dosage").Value), "", row.Cells("Dosage").Value.ToString())
            Dim frequency = If(IsDBNull(row.Cells("Frequency").Value), "", row.Cells("Frequency").Value.ToString())
            Dim instructions = If(IsDBNull(row.Cells("Instructions").Value), "", row.Cells("Instructions").Value.ToString())
            Dim prescribed = Convert.ToInt32(row.Cells("Prescribed").Value)
            Dim dispensed = Convert.ToInt32(row.Cells("Dispensed").Value)

            If prescribed - dispensed <= 0 Then
                MessageBox.Show("This prescription has already been fully dispensed.", "Nothing to dispense", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using dlg As New DispenseMedicationDialog(prescriptionId, patientId, patientName, medicine, dosage, frequency, instructions, prescribed, dispensed)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.Dispensed Then
                    LoadPending()
                    LoadHistory()
                End If
            End Using
        End Sub

    End Class

End Namespace
