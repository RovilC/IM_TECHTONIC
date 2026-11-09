Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Encounter worklist: every visit across all patients, filterable by status, opens the clinical workspace.</summary>
    Public Class EncounterListForm
        Inherits Form

        Private grid As DataGridView
        Private cboStatus As ComboBox
        Private txtSearch As TextBox
        Private lblCount As Label
        Private ReadOnly onOpenEncounter As Action(Of Integer)

        Public Sub New(onOpenEncounter As Action(Of Integer))
            Me.onOpenEncounter = onOpenEncounter
            InitializeComponent()
            LoadData()

        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {.Location = New Point(0, 10), .Width = 300, .PlaceholderText = "Search by patient name or encounter no..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            cboStatus = New ComboBox With {.Location = New Point(310, 10), .Width = 150, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"All Status", "Open", "In Progress", "Closed"})
            cboStatus.SelectedIndex = 0
            AddHandler cboStatus.SelectedIndexChanged, Sub() LoadData()
            toolbar.Controls.Add(cboStatus)

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
                Dim whereClauses As New List(Of String)
                Dim search = txtSearch.Text.Trim()
                If search.Length > 0 Then whereClauses.Add("(p.first_name LIKE @Search OR p.last_name LIKE @Search OR e.encounter_no LIKE @Search)")
                If cboStatus.SelectedIndex > 0 Then whereClauses.Add("e.status = @Status")
                Dim whereSql = ""
                If whereClauses.Count > 0 Then whereSql = " WHERE " & String.Join(" AND ", whereClauses)

                Dim table = Db.Query(
                    $"SELECT e.encounter_id AS Id, e.encounter_no AS 'Encounter No.', e.encounter_date AS Date,
                             CONCAT(p.last_name, ', ', p.first_name) AS Patient, e.visit_type AS 'Visit Type',
                             e.priority_level AS Priority, e.status AS Status, u.full_name AS 'Created By'
                      FROM encounters e
                      JOIN patients p ON p.patient_id = e.patient_id
                      LEFT JOIN users_staff u ON u.user_id = e.created_by
                      {whereSql}
                      ORDER BY e.encounter_date DESC
                      LIMIT {UiTheme.ResultCap}",
                    New With {.Search = "%" & search & "%", .Status = If(cboStatus.SelectedIndex > 0, cboStatus.Text, "")})
                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                lblCount.Text = UiTheme.FormatCountLabel(table.Rows.Count, "encounter")
            Catch ex As Exception
                UiTheme.ShowError(Me, "load encounters", ex)
            End Try
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim id = Convert.ToInt32(grid.Rows(e.RowIndex).Cells("Id").Value)
            onOpenEncounter?.Invoke(id)
        End Sub

    End Class

End Namespace
