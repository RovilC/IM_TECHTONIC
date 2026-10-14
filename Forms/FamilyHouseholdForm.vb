Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Family/household directory: create households, link patients as members, set the family head.</summary>
    Public Class FamilyHouseholdForm
        Inherits Form

        Private gridHouseholds As DataGridView
        Private gridMembers As DataGridView
        Private lblSelected As Label
        Private txtSearch As TextBox
        Private lblCount As Label
        Private cboUnlinkedPatients, cboHeadCandidate As ComboBox
        Private selectedFamilyId As Integer? = Nothing

        Public Sub New()
            InitializeComponent()
            LoadHouseholds()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)
            Dim btnNew As New Button With {.Text = "+ New Household", .Size = New Size(150, 34), .Location = New Point(0, 8)}
            UiTheme.StylePrimaryButton(btnNew)
            AddHandler btnNew.Click, AddressOf BtnNewHousehold_Click
            toolbar.Controls.Add(btnNew)

            txtSearch = New TextBox With {.Location = New Point(160, 10), .Width = 260, .PlaceholderText = "Search household no. or barangay..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadHouseholds)
            toolbar.Controls.Add(txtSearch)

            Dim split As New SplitContainer With {.Dock = DockStyle.Fill}
            Me.Controls.Add(split)
            split.BringToFront()
            UiTheme.ConfigureSplitter(split, 400, 300, 620)

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            split.Panel1.Controls.Add(lblCount)

            gridHouseholds = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridHouseholds)
            AddHandler gridHouseholds.SelectionChanged, AddressOf GridHouseholds_SelectionChanged
            split.Panel1.Controls.Add(gridHouseholds)

            lblSelected = New Label With {.Text = "Select a household to view members", .Font = UiTheme.SubHeaderFont, .Dock = DockStyle.Top, .Height = 30, .Padding = New Padding(0, 4, 0, 0)}
            split.Panel2.Controls.Add(lblSelected)

            gridMembers = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridMembers)
            split.Panel2.Controls.Add(gridMembers)
            gridMembers.BringToFront()

            Dim addPanel As New Panel With {.Dock = DockStyle.Bottom, .Height = 80}
            split.Panel2.Controls.Add(addPanel)

            addPanel.Controls.Add(New Label With {.Text = "Link existing patient as member (type to search):", .AutoSize = True, .Location = New Point(0, 4)})
            cboUnlinkedPatients = New ComboBox With {.Location = New Point(0, 24), .Width = 260}
            UiTheme.AttachSearchableCombo(cboUnlinkedPatients, 2, Function(text) SearchUnlinkedPatients(text))
            addPanel.Controls.Add(cboUnlinkedPatients)
            Dim btnLink As New Button With {.Text = "Add", .Size = New Size(70, 28), .Location = New Point(268, 23)}
            UiTheme.StyleSecondaryButton(btnLink)
            AddHandler btnLink.Click, AddressOf BtnLink_Click
            addPanel.Controls.Add(btnLink)

            cboHeadCandidate = New ComboBox With {.Location = New Point(0, 58), .Width = 260, .DropDownStyle = ComboBoxStyle.DropDownList}
            addPanel.Controls.Add(cboHeadCandidate)
            Dim btnSetHead As New Button With {.Text = "Set as Head", .Size = New Size(110, 28), .Location = New Point(268, 57)}
            UiTheme.StyleSecondaryButton(btnSetHead)
            AddHandler btnSetHead.Click, AddressOf BtnSetHead_Click
            addPanel.Controls.Add(btnSetHead)
        End Sub

        Private Sub LoadHouseholds()
            Try
                Dim search = txtSearch.Text.Trim()
                Dim whereSql = ""
                If search.Length > 0 Then whereSql = " WHERE (f.household_no LIKE @Like OR b.barangay_name LIKE @Like)"

                Dim table = Db.Query(
                    $"SELECT f.family_id AS Id, f.household_no AS 'Household No.',
                            COALESCE(CONCAT(p.last_name, ', ', p.first_name), '(no head set)') AS 'Family Head',
                            b.barangay_name AS Barangay, f.household_status AS Status,
                            (SELECT COUNT(*) FROM patients mp WHERE mp.family_id = f.family_id) AS Members
                     FROM families_households f
                     JOIN barangays b ON b.barangay_id = f.barangay_id
                     LEFT JOIN patients p ON p.patient_id = f.family_head_id
                     {whereSql}
                     ORDER BY f.date_registered DESC
                     LIMIT {UiTheme.ResultCap}",
                    New With {.Like = "%" & search & "%"})
                gridHouseholds.DataSource = table
                If gridHouseholds.Columns.Contains("Id") Then gridHouseholds.Columns("Id").Visible = False
                lblCount.Text = UiTheme.FormatCountLabel(table.Rows.Count, "household")
            Catch ex As Exception
                UiTheme.ShowError(Me, "load households", ex)
            End Try
        End Sub

        Private Sub GridHouseholds_SelectionChanged(sender As Object, e As EventArgs)
            If gridHouseholds.CurrentRow Is Nothing Then Return
            selectedFamilyId = Convert.ToInt32(gridHouseholds.CurrentRow.Cells("Id").Value)
            lblSelected.Text = "Household: " & gridHouseholds.CurrentRow.Cells("Household No.").Value.ToString()
            LoadMembers()
            cboUnlinkedPatients.Items.Clear()
            cboUnlinkedPatients.Text = ""
        End Sub

        Private Sub LoadMembers()
            If Not selectedFamilyId.HasValue Then Return
            Try
                Dim table = Db.Query(
                    "SELECT patient_id AS Id, patient_no AS 'Patient ID', CONCAT(last_name, ', ', first_name) AS Name,
                            TIMESTAMPDIFF(YEAR, date_of_birth, CURDATE()) AS Age, sex AS Sex, civil_status AS 'Civil Status'
                     FROM patients WHERE family_id = @FamilyId ORDER BY last_name, first_name",
                    New With {.FamilyId = selectedFamilyId.Value})
                gridMembers.DataSource = table
                If gridMembers.Columns.Contains("Id") Then gridMembers.Columns("Id").Visible = False

                cboHeadCandidate.Items.Clear()
                For Each row As DataRow In table.Rows
                    cboHeadCandidate.Items.Add(New IdName(Convert.ToInt32(row("Id")), row("Name").ToString()))
                Next
            Catch ex As Exception
                UiTheme.ShowError(Me, "load members", ex)
            End Try
        End Sub

        ''' <summary>Backs the searchable "link patient" combo with a bounded query instead of preloading every family-less patient (thousands at bulk-seed scale).</summary>
        Private Function SearchUnlinkedPatients(text As String) As List(Of Object)
            Dim results As New List(Of Object)
            Try
                Dim likePattern = "%" & text.Trim() & "%"
                For Each row As DataRow In Db.Query(
                    "SELECT patient_id, CONCAT(last_name, ', ', first_name, ' (', patient_no, ')') AS Label
                     FROM patients
                     WHERE family_id IS NULL AND (first_name LIKE @Like OR last_name LIKE @Like OR patient_no LIKE @Like)
                     ORDER BY last_name LIMIT 20", New With {.Like = likePattern}).Rows
                    results.Add(New IdName(Convert.ToInt32(row("patient_id")), row("Label").ToString()))
                Next
            Catch
                ' leave results empty -- the combo just won't show anything this keystroke
            End Try
            Return results
        End Function

        Private Class IdName
            Public ReadOnly Id As Integer
            Public ReadOnly Name As String
            Public Sub New(id As Integer, name As String)
                Me.Id = id : Me.Name = name
            End Sub
            Public Overrides Function ToString() As String
                Return Name
            End Function
        End Class

        Private Sub BtnNewHousehold_Click(sender As Object, e As EventArgs)
            Using dlg As New NewHouseholdDialog()
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    LoadHouseholds()
                End If
            End Using
        End Sub

        Private Sub BtnLink_Click(sender As Object, e As EventArgs)
            If Not selectedFamilyId.HasValue Then
                MessageBox.Show("Select a household first.")
                Return
            End If
            If cboUnlinkedPatients.SelectedItem Is Nothing Then Return
            Dim patient = CType(cboUnlinkedPatients.SelectedItem, IdName)
            If Not UiTheme.Confirm(Me, $"Add {patient.Name} to this household?", "Link Member") Then Return
            Try
                Db.Execute("UPDATE patients SET family_id = @FamilyId, date_updated = NOW() WHERE patient_id = @PatientId",
                    New With {.FamilyId = selectedFamilyId.Value, .PatientId = patient.Id})
                AuditLogger.Log("UPDATE", "family", $"Patient #{patient.Id}", $"Linked to household #{selectedFamilyId.Value}")
                LoadMembers()
                cboUnlinkedPatients.Items.Clear()
                cboUnlinkedPatients.Text = ""
                LoadHouseholds()
            Catch ex As Exception
                UiTheme.ShowError(Me, "link patient", ex)
            End Try
        End Sub

        Private Sub BtnSetHead_Click(sender As Object, e As EventArgs)
            If Not selectedFamilyId.HasValue OrElse cboHeadCandidate.SelectedItem Is Nothing Then Return
            Dim member = CType(cboHeadCandidate.SelectedItem, IdName)
            If Not UiTheme.Confirm(Me, $"Make {member.Name} the head of this household? This replaces the current head.", "Set Family Head") Then Return
            Try
                ' All three writes must land together -- a crash between the first and the
                ' other two would leave families_households.family_head_id pointing at someone
                ' whose own family_head_or_no flag was never actually set (or the reverse: the
                ' old head's flag cleared but the new head's never set).
                Using tx As New DbTransaction()
                    tx.Execute("UPDATE families_households SET family_head_id = @HeadId, date_updated = NOW() WHERE family_id = @FamilyId",
                        New With {.HeadId = member.Id, .FamilyId = selectedFamilyId.Value})
                    tx.Execute("UPDATE patients SET family_head_or_no = FALSE WHERE family_id = @FamilyId", New With {.FamilyId = selectedFamilyId.Value})
                    tx.Execute("UPDATE patients SET family_head_or_no = TRUE WHERE patient_id = @HeadId", New With {.HeadId = member.Id})
                    tx.Commit()
                End Using
                AuditLogger.Log("UPDATE", "family", $"Household #{selectedFamilyId.Value}", $"Set family head to patient #{member.Id}")
                LoadHouseholds()
                MessageBox.Show($"{member.Name} set as family head.", "Updated", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                UiTheme.ShowError(Me, "set family head", ex)
            End Try
        End Sub

    End Class

    ''' <summary>Small modal for creating a household (family_id, household_no auto-generated).</summary>
    Public Class NewHouseholdDialog
        Inherits Form

        Private txtAddress As TextBox
        Private cboBarangay, cboStatus As ComboBox
        Private lblError As Label

        Public Sub New()
            Me.Text = "New Household"
            Me.Size = New Size(460, 340)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont
            Me.Padding = New Padding(20)

            Me.Controls.Add(New Label With {.Text = "Register Household", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})

            Me.Controls.Add(New Label With {.Text = "Address", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 60)})
            txtAddress = New TextBox With {.Location = New Point(20, 80), .Width = 400}
            Me.Controls.Add(txtAddress)

            Me.Controls.Add(New Label With {.Text = "Barangay *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 116)})
            cboBarangay = New ComboBox With {.Location = New Point(20, 136), .Width = 250, .DropDownStyle = ComboBoxStyle.DropDownList}
            Me.Controls.Add(cboBarangay)

            Me.Controls.Add(New Label With {.Text = "Household Status", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 172)})
            cboStatus = New ComboBox With {.Location = New Point(20, 192), .Width = 250, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"Active", "Regularly Monitored", "Inactive"})
            cboStatus.SelectedIndex = 0
            Me.Controls.Add(cboStatus)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 228), .MaximumSize = New Size(400, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(90, 34), .Location = New Point(210, 260)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Save", .Size = New Size(110, 34), .Location = New Point(310, 260)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)

            LoadBarangays()
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Class BarangayItem
            Public ReadOnly Id As Integer
            Public ReadOnly Name As String
            Public Sub New(id As Integer, name As String)
                Me.Id = id : Me.Name = name
            End Sub
            Public Overrides Function ToString() As String
                Return Name
            End Function
        End Class

        Private Sub LoadBarangays()
            Try
                Dim table = Db.Query("SELECT barangay_id, barangay_name FROM barangays ORDER BY barangay_name")
                For Each row As DataRow In table.Rows
                    cboBarangay.Items.Add(New BarangayItem(Convert.ToInt32(row("barangay_id")), row("barangay_name").ToString()))
                Next
                If cboBarangay.Items.Count > 0 Then cboBarangay.SelectedIndex = 0
            Catch ex As Exception
                lblError.Text = "Could not load barangays: " & ex.Message
            End Try
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If cboBarangay.SelectedItem Is Nothing Then
                lblError.Text = "Barangay is required."
                Return
            End If
            If Not UiTheme.Confirm(Me, $"Register a new household in {cboBarangay.Text}?", "New Household") Then Return
            Try
                Dim barangayId = CType(cboBarangay.SelectedItem, BarangayItem).Id
                Dim householdNo = IdGenerator.NextHouseholdNo()
                Db.Execute(
                    "INSERT INTO families_households (household_no, address, barangay_id, household_status, date_registered)
                     VALUES (@HouseholdNo, @Address, @BarangayId, @Status, CURDATE())",
                    New With {.HouseholdNo = householdNo, .Address = If(String.IsNullOrWhiteSpace(txtAddress.Text), CType(DBNull.Value, Object), txtAddress.Text.Trim()), .BarangayId = barangayId, .Status = cboStatus.Text})
                AuditLogger.Log("CREATE", "family", householdNo, "Registered new household")
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not save household: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
