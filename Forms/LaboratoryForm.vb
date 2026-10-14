Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Laboratory worklist (Lab Queue / Lab Results, mockup sidebar items): every request across all patients, opens LabResultDialog.</summary>
    Public Class LaboratoryForm
        Inherits Form

        Private grid As DataGridView
        Private cboStatus As ComboBox
        Private txtSearch As TextBox
        Private lblCount As Label

        Public Sub New()
            InitializeComponent()
            LoadData()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {.Location = New Point(0, 10), .Width = 280, .PlaceholderText = "Search patient or test name..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            cboStatus = New ComboBox With {.Location = New Point(290, 10), .Width = 160, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"All Status", "Pending", "Referred", "Released"})
            cboStatus.SelectedIndex = 0
            AddHandler cboStatus.SelectedIndexChanged, Sub() LoadData()
            toolbar.Controls.Add(cboStatus)

            Dim lblHint As New Label With {.Text = "Double-click a Pending or Referred request to enter and release its result, or a Released one to view it (read-only).", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(0, 44)}
            Dim hintPanel As New Panel With {.Dock = DockStyle.Top, .Height = 22}
            hintPanel.Controls.Add(lblHint)
            Me.Controls.Add(hintPanel)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            Me.Controls.Add(grid)
            grid.BringToFront()

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(lblCount)
        End Sub

        Private Sub LoadData()
            Try
                ' Requests awaiting RHU Management approval (or rejected) never reach the lab queue. Referred ones do,
                ' so the external laboratory's result can be recorded when it comes back.
                Dim whereClauses As New List(Of String) From {"l.status IN ('Pending','In Progress','Referred','Released')"}
                Dim search = txtSearch.Text.Trim()
                If search.Length > 0 Then whereClauses.Add("(p.first_name LIKE @Search OR p.last_name LIKE @Search OR l.test_name LIKE @Search)")
                If cboStatus.SelectedIndex > 0 Then whereClauses.Add(If(cboStatus.Text = "Pending", "l.status IN ('Pending','In Progress')", "l.status = @Status"))
                Dim whereSql = ""
                If whereClauses.Count > 0 Then whereSql = " WHERE " & String.Join(" AND ", whereClauses)

                Dim table = Db.Query(
                    $"SELECT l.laboratory_id AS Id, l.request_date AS Requested, CONCAT(p.last_name, ', ', p.first_name) AS Patient,
                             l.test_name AS Test, l.test_category AS Category, l.specimen_type AS Specimen, l.priority_level AS Priority,
                             u.full_name AS 'Requested By',
                             IF(l.lab_destination = 'External', CONCAT('External: ', l.external_facility), 'RHU Lab') AS 'Sent To',
                             IF(l.status = 'In Progress', 'Pending', l.status) AS Status, l.result_source AS 'Result From', l.result_clinic AS 'Clinic / Laboratory',
                             l.result_doctor AS 'Doctor / Pathologist', l.result AS Result
                      FROM laboratory l
                      JOIN encounters e ON e.encounter_id = l.encounter_id
                      JOIN patients p ON p.patient_id = e.patient_id
                      LEFT JOIN users_staff u ON u.user_id = l.requested_by
                      {whereSql}
                      ORDER BY FIELD(l.status,'Pending','In Progress','Referred','Released'), l.request_date DESC
                      LIMIT {UiTheme.ResultCap}",
                    New With {.Search = "%" & search & "%", .Status = If(cboStatus.SelectedIndex > 0, cboStatus.Text, "")})
                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                If grid.Columns.Contains("Result") Then grid.Columns("Result").Visible = False
                lblCount.Text = "  " & UiTheme.FormatCountLabel(table.Rows.Count, "request")
            Catch ex As Exception
                lblCount.Text = "Error loading lab requests: " & ex.Message
            End Try
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim row = grid.Rows(e.RowIndex)
            Dim id = Convert.ToInt32(row.Cells("Id").Value)
            Dim patient = row.Cells("Patient").Value.ToString()
            Dim test = row.Cells("Test").Value.ToString()
            Using dlg As New LabResultDialog(id, patient, test)
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadData()
            End Using
        End Sub

    End Class

End Namespace
