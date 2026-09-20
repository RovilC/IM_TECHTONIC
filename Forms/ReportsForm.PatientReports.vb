Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    Partial Public Class ReportsForm

        ' ---------------- 1. Master List Report (Figure 31) ----------------
        Private Function BuildMasterListReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Patient Master List Report")

            p.Controls.Add(New Label With {.Text = "Barangay", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 44)})
            Dim cboBarangay As New ComboBox With {.Location = New Point(0, 64), .Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboBarangay.Items.Add("All Barangays")
            Try
                For Each row As DataRow In Db.Query("SELECT barangay_name FROM barangays ORDER BY barangay_name").Rows
                    cboBarangay.Items.Add(row("barangay_name").ToString())
                Next
            Catch
            End Try
            cboBarangay.SelectedIndex = 0
            p.Controls.Add(cboBarangay)

            p.Controls.Add(New Label With {.Text = "Sex", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(220, 44)})
            Dim cboSex As New ComboBox With {.Location = New Point(220, 64), .Width = 120, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboSex.Items.AddRange({"All Sexes", "Male", "Female"})
            cboSex.SelectedIndex = 0
            p.Controls.Add(cboSex)

            p.Controls.Add(New Label With {.Text = "Status", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(360, 44)})
            Dim cboStatus As New ComboBox With {.Location = New Point(360, 64), .Width = 120, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"All Status", "Active", "Inactive"})
            cboStatus.SelectedIndex = 0
            p.Controls.Add(cboStatus)

            Dim lblSummary = MakeSummaryLabel(0, 102)
            p.Controls.Add(lblSummary)

            Dim grid = MakeGrid(130)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim barangayFilter = If(cboBarangay.SelectedIndex > 0, cboBarangay.Text, "")
                        Dim sexFilter = If(cboSex.SelectedIndex > 0, cboSex.Text, "")
                        Dim statusFilter = If(cboStatus.SelectedIndex > 0, cboStatus.Text, "")
                        Dim filterParams = New With {.Barangay = barangayFilter, .Sex = sexFilter, .Status = statusFilter}

                        Dim table = Db.Query(
                            $"SELECT p.patient_no AS 'Patient ID', CONCAT(p.last_name, ', ', p.first_name, ' ', COALESCE(p.middle_name,'')) AS 'Full Name',
                                     p.sex AS Sex, TIMESTAMPDIFF(YEAR, p.date_of_birth, CURDATE()) AS Age, b.barangay_name AS Barangay,
                                     p.contact_number AS 'Contact Number', COALESCE(f.household_no, '-') AS 'Family ID', p.date_registered AS 'Date Registered'
                              FROM patients p
                              JOIN barangays b ON b.barangay_id = p.barangay_id
                              LEFT JOIN families_households f ON f.family_id = p.family_id
                              WHERE (@Barangay = '' OR b.barangay_name = @Barangay)
                                AND (@Sex = '' OR p.sex = @Sex)
                                AND (@Status = '' OR p.status = @Status)
                              ORDER BY p.last_name, p.first_name
                              LIMIT {UiTheme.ResultCap}",
                            filterParams)
                        grid.DataSource = table

                        ' Computed via COUNT(*) against the same filter rather than pulling every
                        ' matching row into memory to count them -- correct and fast even when the
                        ' filter matches all 10,000+ patients (the grid above still caps at
                        ' ResultCap, but these totals must reflect the true matching count).
                        Dim total = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM patients p JOIN barangays b ON b.barangay_id = p.barangay_id
                             WHERE (@Barangay = '' OR b.barangay_name = @Barangay) AND (@Sex = '' OR p.sex = @Sex) AND (@Status = '' OR p.status = @Status)", filterParams))
                        Dim male = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM patients p JOIN barangays b ON b.barangay_id = p.barangay_id
                             WHERE p.sex = 'Male' AND (@Barangay = '' OR b.barangay_name = @Barangay) AND (@Status = '' OR p.status = @Status)", filterParams))
                        Dim female = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM patients p JOIN barangays b ON b.barangay_id = p.barangay_id
                             WHERE p.sex = 'Female' AND (@Barangay = '' OR b.barangay_name = @Barangay) AND (@Status = '' OR p.status = @Status)", filterParams))
                        Dim thisMonth = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM patients p JOIN barangays b ON b.barangay_id = p.barangay_id
                             WHERE YEAR(p.date_registered) = YEAR(CURDATE()) AND MONTH(p.date_registered) = MONTH(CURDATE())
                               AND (@Barangay = '' OR b.barangay_name = @Barangay) AND (@Sex = '' OR p.sex = @Sex) AND (@Status = '' OR p.status = @Status)", filterParams))
                        lblSummary.Text = $"Total Registered: {total}     Male: {male}     Female: {female}     New Registrations (This Month): {thisMonth}" &
                            If(total > UiTheme.ResultCap, $"   -- showing first {UiTheme.ResultCap} below", "")
                    Catch ex As Exception
                        lblSummary.Text = "Error: " & ex.Message
                    End Try
                End Sub
            AddHandler cboBarangay.SelectedIndexChanged, Sub() refresh()
            AddHandler cboSex.SelectedIndexChanged, Sub() refresh()
            AddHandler cboStatus.SelectedIndexChanged, Sub() refresh()
            WireExportMenu(btnExport, "Patient Master List Report", "PatientMasterList",
                Function() New List(Of (String, DataTable)) From {("Registered Patients", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

        ' ---------------- 2. Family Folder Report (Figure 32) ----------------
        Private Function BuildFamilyFolderReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Family Folder Report")

            p.Controls.Add(New Label With {.Text = "Household (type to search)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 44)})
            Dim cboHousehold As New ComboBox With {.Location = New Point(0, 64), .Width = 320}
            UiTheme.AttachSearchableCombo(cboHousehold, 2,
                Function(text) SearchHouseholds(text))
            p.Controls.Add(cboHousehold)

            Dim lblInfo = New Label With {.AutoSize = True, .Location = New Point(0, 104), .Font = UiTheme.BaseFont, .MaximumSize = New Size(1000, 0)}
            lblInfo.Text = "Type at least 2 letters of a household number or barangay above, then pick it from the list."
            p.Controls.Add(lblInfo)

            Dim lblMembersTitle = New Label With {.Text = "Registered Family Members", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 190)}
            p.Controls.Add(lblMembersTitle)
            Dim gridMembers = New DataGridView With {.Location = New Point(0, 214), .Size = New Size(1180, 180)}
            UiTheme.StyleGrid(gridMembers)
            p.Controls.Add(gridMembers)

            Dim lblHealthTitle = New Label With {.Text = "Family Health Record Summary", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 404)}
            p.Controls.Add(lblHealthTitle)
            Dim gridHealth = MakeGrid(428)
            gridHealth.Size = New Size(1180, 180)
            p.Controls.Add(gridHealth)

            Dim refresh As Action =
                Sub()
                    If cboHousehold.SelectedItem Is Nothing Then Return
                    Dim familyId = CType(cboHousehold.SelectedItem, HouseholdItem).Id
                    Try
                        Dim householdTable = Db.Query(
                            "SELECT f.household_no, f.address, b.barangay_name, f.household_status, f.date_registered, f.date_updated, f.remarks,
                                    COALESCE(CONCAT(head.last_name, ', ', head.first_name), '(no head set)') AS head_name
                             FROM families_households f
                             JOIN barangays b ON b.barangay_id = f.barangay_id
                             LEFT JOIN patients head ON head.patient_id = f.family_head_id
                             WHERE f.family_id = @Id", New With {.Id = familyId})
                        If householdTable.Rows.Count > 0 Then
                            Dim h = householdTable.Rows(0)
                            lblInfo.Text =
                                $"Household No.: {h("household_no")}      Family Head: {h("head_name")}" & vbCrLf &
                                $"Address: {h("address")}, {h("barangay_name")}, Vinzons, Camarines Norte" & vbCrLf &
                                $"Status: {h("household_status")}      Date Registered: {Convert.ToDateTime(h("date_registered")):MMM dd, yyyy}" & vbCrLf &
                                $"Remarks: {If(IsDBNull(h("remarks")), "-", h("remarks"))}"
                        End If

                        gridMembers.DataSource = Db.Query(
                            "SELECT patient_no AS 'Patient ID', CONCAT(last_name, ', ', first_name) AS 'Patient Name', sex AS Sex,
                                    date_of_birth AS 'Date of Birth', TIMESTAMPDIFF(YEAR, date_of_birth, CURDATE()) AS Age,
                                    civil_status AS 'Civil Status', philhealth_category AS 'PhilHealth Category'
                             FROM patients WHERE family_id = @Id ORDER BY family_head_or_no DESC, last_name", New With {.Id = familyId})

                        gridHealth.DataSource = Db.Query(
                            "SELECT p.patient_no AS 'Patient ID', CONCAT(p.last_name, ', ', p.first_name) AS 'Patient Name',
                                    (SELECT MAX(e.encounter_date) FROM encounters e WHERE e.patient_id = p.patient_id) AS 'Last Checkup',
                                    (SELECT c.chief_complaint FROM consultations c JOIN encounters e2 ON e2.encounter_id = c.encounter_id
                                        WHERE e2.patient_id = p.patient_id ORDER BY c.consultation_date DESC LIMIT 1) AS 'Chief Complaint',
                                    (SELECT d.diagnosis_desc FROM diagnoses d
                                        JOIN consultations c2 ON c2.consultation_id = d.consultation_id
                                        JOIN encounters e3 ON e3.encounter_id = c2.encounter_id
                                        WHERE e3.patient_id = p.patient_id ORDER BY d.created_at DESC LIMIT 1) AS Diagnosis,
                                    p.status AS 'Current Status'
                             FROM patients p WHERE p.family_id = @Id ORDER BY p.family_head_or_no DESC, p.last_name", New With {.Id = familyId})
                    Catch ex As Exception
                        lblInfo.Text = "Error: " & ex.Message
                    End Try
                End Sub
            AddHandler cboHousehold.SelectedIndexChanged, Sub() refresh()
            WireExportMenu(btnExport, "Family Folder Report", "FamilyFolder",
                Function() New List(Of (String, DataTable)) From {
                    ("Registered Family Members", CType(gridMembers.DataSource, DataTable)),
                    ("Family Health Record Summary", CType(gridHealth.DataSource, DataTable))
                })
            Return p
        End Function

        ''' <summary>Backs the searchable household combo above with a bounded query instead of loading every household into a ComboBox.</summary>
        Private Function SearchHouseholds(text As String) As List(Of Object)
            Dim results As New List(Of Object)
            Try
                Dim likePattern = "%" & text.Trim() & "%"
                For Each row As DataRow In Db.Query(
                    "SELECT f.family_id, f.household_no, b.barangay_name
                     FROM families_households f JOIN barangays b ON b.barangay_id = f.barangay_id
                     WHERE f.household_no LIKE @Like OR b.barangay_name LIKE @Like
                     ORDER BY f.household_no LIMIT 20", New With {.Like = likePattern}).Rows
                    results.Add(New HouseholdItem(Convert.ToInt32(row("family_id")), $"{row("household_no")} - {row("barangay_name")}"))
                Next
            Catch
                ' leave results empty -- the combo just won't show anything this keystroke
            End Try
            Return results
        End Function

        Private Class HouseholdItem
            Public ReadOnly Id As Integer
            Public ReadOnly Label As String
            Public Sub New(id As Integer, label As String)
                Me.Id = id : Me.Label = label
            End Sub
            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

    End Class

End Namespace
