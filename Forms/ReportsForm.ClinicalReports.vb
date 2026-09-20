Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    Partial Public Class ReportsForm

        ' ---------------- 10. Consultation Report ----------------
        Private Function BuildConsultationReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Consultation Report")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo

            p.Controls.Add(New Label With {.Text = "Patient / Doctor", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(340, 44)})
            Dim txtSearch As New TextBox With {.Location = New Point(340, 64), .Width = 220, .PlaceholderText = "Search..."}
            p.Controls.Add(txtSearch)

            Dim lblCount = MakeSummaryLabel(0, 100)
            p.Controls.Add(lblCount)

            Dim grid = MakeGrid(126)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim search = txtSearch.Text.Trim()
                        Dim table = Db.Query(
                            $"SELECT c.consultation_date AS Date, CONCAT(p2.last_name, ', ', p2.first_name) AS Patient, u.full_name AS Doctor,
                                    c.chief_complaint AS 'Chief Complaint',
                                    (SELECT GROUP_CONCAT(d.diagnosis_desc SEPARATOR '; ') FROM diagnoses d WHERE d.consultation_id = c.consultation_id) AS Diagnoses,
                                    c.assessment AS Assessment, c.treatment_plan AS 'Treatment Plan', c.status AS Status
                             FROM consultations c
                             JOIN encounters e ON e.encounter_id = c.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             LEFT JOIN users_staff u ON u.user_id = c.doctor_id
                             WHERE c.consultation_date BETWEEN @From AND @To
                               AND (@Search = '' OR p2.first_name LIKE @SearchLike OR p2.last_name LIKE @SearchLike OR u.full_name LIKE @SearchLike)
                             ORDER BY c.consultation_date DESC
                             LIMIT {UiTheme.ResultCap}",
                            New With {.From = dtpFrom.Value.Date, .To = dtpTo.Value.Date.AddDays(1).AddSeconds(-1), .Search = search, .SearchLike = "%" & search & "%"})
                        grid.DataSource = table
                        lblCount.Text = UiTheme.FormatCountLabel(table.Rows.Count, "consultation")
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            UiTheme.AttachDebouncedSearch(txtSearch, refresh)
            WireExportMenu(btnExport, "Consultation Report", "ConsultationReport",
                Function() New List(Of (String, DataTable)) From {("Consultations", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

        ' ---------------- 5. Morbidity Report (Figure 35) ----------------
        Private Function BuildMorbidityReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Consolidated Morbidity Report")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo

            p.Controls.Add(New Label With {.Text = "Cases by Condition", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 100)})
            Dim gridCases As New DataGridView With {.Location = New Point(0, 122), .Size = New Size(1180, 200)}
            UiTheme.StyleGrid(gridCases)
            p.Controls.Add(gridCases)

            p.Controls.Add(New Label With {.Text = "Morbidity by Age Distribution", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 332)})
            Dim gridAge = MakeGrid(354)
            gridAge.Size = New Size(1180, 220)
            p.Controls.Add(gridAge)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim fromD = dtpFrom.Value.Date
                        Dim toD = dtpTo.Value.Date.AddDays(1).AddSeconds(-1)

                        Dim casesTable = Db.Query(
                            "SELECT d.diagnosis_desc AS 'Condition / Diagnosis',
                                    SUM(CASE WHEN p2.sex = 'Male' THEN 1 ELSE 0 END) AS Male,
                                    SUM(CASE WHEN p2.sex = 'Female' THEN 1 ELSE 0 END) AS Female,
                                    COUNT(*) AS 'Total Cases'
                             FROM diagnoses d
                             JOIN consultations c ON c.consultation_id = d.consultation_id
                             JOIN encounters e ON e.encounter_id = c.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             WHERE c.consultation_date BETWEEN @From AND @To
                             GROUP BY d.diagnosis_desc
                             ORDER BY COUNT(*) DESC, d.diagnosis_desc",
                            New With {.From = fromD, .To = toD})

                        Dim grandTotal = 0
                        For Each row As DataRow In casesTable.Rows
                            grandTotal += Convert.ToInt32(row("Total Cases"))
                        Next
                        If Not casesTable.Columns.Contains("Percent Share") Then casesTable.Columns.Add("Percent Share", GetType(String))
                        For Each row As DataRow In casesTable.Rows
                            Dim pct = If(grandTotal > 0, Convert.ToInt32(row("Total Cases")) / grandTotal * 100.0, 0)
                            row("Percent Share") = $"{pct:0.0}%"
                        Next
                        gridCases.DataSource = casesTable

                        gridAge.DataSource = Db.Query(
                            "SELECT d.diagnosis_desc AS 'Condition / Diagnosis',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) < 1 THEN 1 ELSE 0 END) AS '< 1 yr',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) BETWEEN 1 AND 4 THEN 1 ELSE 0 END) AS '1-4 yrs',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) BETWEEN 5 AND 14 THEN 1 ELSE 0 END) AS '5-14 yrs',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) BETWEEN 15 AND 49 THEN 1 ELSE 0 END) AS '15-49 yrs',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) BETWEEN 50 AND 59 THEN 1 ELSE 0 END) AS '50-59 yrs',
                                    SUM(CASE WHEN TIMESTAMPDIFF(YEAR, p2.date_of_birth, c.consultation_date) >= 60 THEN 1 ELSE 0 END) AS '60+ yrs',
                                    COUNT(*) AS Total
                             FROM diagnoses d
                             JOIN consultations c ON c.consultation_id = d.consultation_id
                             JOIN encounters e ON e.encounter_id = c.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             WHERE c.consultation_date BETWEEN @From AND @To
                             GROUP BY d.diagnosis_desc
                             ORDER BY Total DESC, d.diagnosis_desc",
                            New With {.From = fromD, .To = toD})
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            WireExportMenu(btnExport, "Consolidated Morbidity Report", "MorbidityReport",
                Function() New List(Of (String, DataTable)) From {
                    ("Cases by Condition", CType(gridCases.DataSource, DataTable)),
                    ("Morbidity by Age Distribution", CType(gridAge.DataSource, DataTable))
                })
            refresh()
            Return p
        End Function

        ' ---------------- 7. Case Finding & 8. Treatment Report ----------------
        ' The proposal's Figures 39-40 are DOH-style TB/HIV intake forms with
        ' dedicated fields (screening mode, bacteriological status, regimen,
        ' HIV testing, etc.) that have no home in this schema -- there is no
        ' TB/HIV-specific module. These two reports are a best-effort
        ' substitute: they surface patients whose recorded diagnoses mention
        ' TB/Tuberculosis/HIV, using the general consultation/diagnosis data
        ' that IS captured, so the report isn't empty -- not a full
        ' implementation of the DOH forms.

        Private Function BuildCaseFindingReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Case Finding (TB / HIV) - Simplified")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo
            AddSimplifiedDiseaseNotice(p, 100)

            Dim grid = MakeGrid(150)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        grid.DataSource = Db.Query(
                            "SELECT p2.patient_no AS 'Patient ID', CONCAT(p2.last_name, ', ', p2.first_name) AS Patient,
                                    TIMESTAMPDIFF(YEAR, p2.date_of_birth, CURDATE()) AS Age, p2.sex AS Sex,
                                    d.diagnosis_desc AS 'Diagnosis / Screening Result', d.classification AS Classification, d.status AS Status,
                                    c.consultation_date AS 'Date Diagnosed', u.full_name AS 'Attending Staff'
                             FROM diagnoses d
                             JOIN consultations c ON c.consultation_id = d.consultation_id
                             JOIN encounters e ON e.encounter_id = c.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             LEFT JOIN users_staff u ON u.user_id = c.doctor_id
                             WHERE c.consultation_date BETWEEN @From AND @To
                               AND (d.diagnosis_desc LIKE '%TB%' OR d.diagnosis_desc LIKE '%Tuberculosis%' OR d.diagnosis_desc LIKE '%HIV%')
                             ORDER BY c.consultation_date DESC",
                            New With {.From = dtpFrom.Value.Date, .To = dtpTo.Value.Date.AddDays(1).AddSeconds(-1)})
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            WireExportMenu(btnExport, "Case Finding (TB / HIV) - Simplified", "CaseFinding",
                Function() New List(Of (String, DataTable)) From {("Case Finding", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

        Private Function BuildTreatmentReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Treatment Report (TB / HIV) - Simplified")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo
            AddSimplifiedDiseaseNotice(p, 100)

            Dim grid = MakeGrid(150)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        grid.DataSource = Db.Query(
                            "SELECT p2.patient_no AS 'Patient ID', CONCAT(p2.last_name, ', ', p2.first_name) AS Patient,
                                    d.diagnosis_desc AS Diagnosis, c.treatment_plan AS 'Treatment Plan', c.follow_up_date AS 'Follow-up Date',
                                    c.follow_up_instructions AS 'Follow-up Instructions', c.status AS 'Consultation Status', u.full_name AS 'Attending Staff'
                             FROM diagnoses d
                             JOIN consultations c ON c.consultation_id = d.consultation_id
                             JOIN encounters e ON e.encounter_id = c.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             LEFT JOIN users_staff u ON u.user_id = c.doctor_id
                             WHERE c.consultation_date BETWEEN @From AND @To
                               AND (d.diagnosis_desc LIKE '%TB%' OR d.diagnosis_desc LIKE '%Tuberculosis%' OR d.diagnosis_desc LIKE '%HIV%')
                             ORDER BY c.consultation_date DESC",
                            New With {.From = dtpFrom.Value.Date, .To = dtpTo.Value.Date.AddDays(1).AddSeconds(-1)})
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            WireExportMenu(btnExport, "Treatment Report (TB / HIV) - Simplified", "TreatmentReport",
                Function() New List(Of (String, DataTable)) From {("Treatment Report", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

        Private Sub AddSimplifiedDiseaseNotice(container As Panel, y As Integer)
            container.Controls.Add(New Label With {
                .Text = "Simplified report: matches diagnoses mentioning TB/Tuberculosis/HIV against recorded consultations. There is no dedicated TB/HIV intake module (Figures 39-40's DOH forms) in this build.",
                .ForeColor = UiTheme.WarningAmber, .Font = New Font("Segoe UI", 8.0F, FontStyle.Italic), .AutoSize = True, .Location = New Point(0, y), .MaximumSize = New Size(1000, 0)
            })
        End Sub

    End Class

End Namespace
