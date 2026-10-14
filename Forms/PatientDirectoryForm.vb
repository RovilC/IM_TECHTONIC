Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Searchable/filterable patient list (Figure 5).</summary>
    Public Class PatientDirectoryForm
        Inherits Form

        Private txtSearch As TextBox
        Private cboSex As ComboBox
        Private grid As DataGridView
        Private lblCount As Label

        Private ReadOnly onOpenPatient As Action(Of Integer)
        Private ReadOnly onRegisterNew As Action

        Public Sub New(onOpenPatient As Action(Of Integer), onRegisterNew As Action)
            Me.onOpenPatient = onOpenPatient
            Me.onRegisterNew = onRegisterNew
            InitializeComponent()
            LoadData()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 56}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {
                .Location = New Point(0, 12),
                .Size = New Size(320, 30),
                .PlaceholderText = "Search by name, patient ID, or contact number..."
            }
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            cboSex = New ComboBox With {.Location = New Point(332, 12), .Size = New Size(110, 30), .DropDownStyle = ComboBoxStyle.DropDownList}
            cboSex.Items.AddRange({"All Sexes", "Male", "Female"})
            cboSex.SelectedIndex = 0
            AddHandler cboSex.SelectedIndexChanged, Sub() LoadData()
            toolbar.Controls.Add(cboSex)


            ' Only roles holding the "registration" module (e.g. Records Staff) may register patients;
            ' others (e.g. Healthcare Staff) only search and open existing records.
            If Models.Session.Current IsNot Nothing AndAlso Models.Session.Current.HasModule("registration") Then
                Dim btnRegister As New Button With {.Text = "+ Register Patient", .Size = New Size(170, 34)}
                UiTheme.StylePrimaryButton(btnRegister)
                UiTheme.PinToRight(btnRegister, toolbar, 0, 10)
                AddHandler btnRegister.Click, Sub() onRegisterNew?.Invoke()
                toolbar.Controls.Add(btnRegister)
            End If

            lblCount = New Label With {.Text = "", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(0, 58)}
            Dim countPanel As New Panel With {.Dock = DockStyle.Top, .Height = 26}
            countPanel.Controls.Add(lblCount)
            Me.Controls.Add(countPanel)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            Me.Controls.Add(grid)
            grid.BringToFront()
        End Sub

        Private Sub LoadData()
            Try
                Dim whereClauses As New List(Of String)
                Dim search = txtSearch.Text.Trim()
                If search.Length > 0 Then
                    whereClauses.Add("(p.patient_no LIKE @Search OR p.first_name LIKE @Search OR p.last_name LIKE @Search OR p.contact_number LIKE @Search)")
                End If
                If cboSex.SelectedIndex > 0 Then whereClauses.Add("p.sex = @Sex")

                Dim whereSql = ""
                If whereClauses.Count > 0 Then whereSql = " WHERE " & String.Join(" AND ", whereClauses)

                Dim table = Db.Query(
                    $"SELECT p.patient_id AS Id, p.patient_no AS 'Patient ID',
                             CONCAT(p.last_name, ', ', p.first_name, ' ', COALESCE(p.middle_name,'')) AS 'Patient Name',
                             TIMESTAMPDIFF(YEAR, p.date_of_birth, CURDATE()) AS Age,
                             p.sex AS Sex, p.contact_number AS Contact, b.barangay_name AS Barangay,
                             (SELECT MAX(e.encounter_date) FROM encounters e WHERE e.patient_id = p.patient_id) AS 'Last Visit'
                      FROM patients p
                      JOIN barangays b ON b.barangay_id = p.barangay_id
                      {whereSql}
                      ORDER BY p.date_registered DESC
                      LIMIT {UiTheme.ResultCap}",
                    New With {.Search = "%" & search & "%", .Sex = If(cboSex.SelectedIndex > 0, cboSex.Text, "")})

                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                lblCount.Text = FormatCountLabel(table.Rows.Count, "record")
            Catch ex As Exception
                lblCount.Text = "Error loading patients: " & ex.Message
            End Try
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim id = Convert.ToInt32(grid.Rows(e.RowIndex).Cells("Id").Value)
            onOpenPatient?.Invoke(id)
        End Sub

    End Class

End Namespace
