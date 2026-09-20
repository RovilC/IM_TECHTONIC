Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    Partial Public Class ReportsForm

        ' ---------------- 4. Prescription Report (Figure 34) ----------------
        Private Function BuildPrescriptionReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Prescription Report")

            Dim filter = BuildDateRangeAndStatusFilter(p, {"All Status", "Pending", "Partially Dispensed", "Dispensed", "Cancelled"})
            Dim dtpFrom = filter.dtpFrom
            Dim dtpTo = filter.dtpTo
            Dim cboStatus = filter.cboStatus

            p.Controls.Add(New Label With {.Text = "Patient / Medicine", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(520, 44)})
            Dim txtSearch As New TextBox With {.Location = New Point(520, 64), .Width = 220, .PlaceholderText = "Search..."}
            p.Controls.Add(txtSearch)

            Dim grid = MakeGrid(100)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim search = txtSearch.Text.Trim()
                        Dim table = Db.Query(
                            $"SELECT rx.prescription_number AS 'Rx No.', rx.medication_date AS Date, CONCAT(p2.last_name, ', ', p2.first_name) AS Patient,
                                    rx.medicine_name AS Medicine, rx.dosage AS Dosage, rx.frequency AS Frequency, rx.quantity AS Quantity,
                                    u.full_name AS 'Prescribed By', rx.status AS Status
                             FROM prescriptions_medication_orders rx
                             JOIN encounters e ON e.encounter_id = rx.encounter_id
                             JOIN patients p2 ON p2.patient_id = e.patient_id
                             LEFT JOIN users_staff u ON u.user_id = rx.doctor_id
                             WHERE rx.medication_date BETWEEN @From AND @To
                               AND (@Status = '' OR rx.status = @Status)
                               AND (@Search = '' OR p2.first_name LIKE @SearchLike OR p2.last_name LIKE @SearchLike OR rx.medicine_name LIKE @SearchLike)
                             ORDER BY rx.medication_date DESC
                             LIMIT {UiTheme.ResultCap}",
                            New With {
                                .From = dtpFrom.Value.Date, .To = dtpTo.Value.Date.AddDays(1).AddSeconds(-1),
                                .Status = If(cboStatus.SelectedIndex > 0, cboStatus.Text, ""),
                                .Search = search, .SearchLike = "%" & search & "%"
                            })
                        grid.DataSource = table
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            AddHandler cboStatus.SelectedIndexChanged, Sub() refresh()
            UiTheme.AttachDebouncedSearch(txtSearch, refresh)
            WireExportMenu(btnExport, "Prescription Report", "PrescriptionReport",
                Function() New List(Of (String, DataTable)) From {("Prescriptions", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

        ' ---------------- 3. Inventory In/Out Report (Figure 33) ----------------
        Private Function BuildInventoryReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Medicine Inventory In/Out Report")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo

            p.Controls.Add(New Label With {.Text = "Medicine", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(340, 44)})
            Dim txtMedicine As New TextBox With {.Location = New Point(340, 64), .Width = 220, .PlaceholderText = "Search medicine name..."}
            p.Controls.Add(txtMedicine)

            p.Controls.Add(New Label With {
                .Text = "'On Hand' is the medicine's current stock, not the balance at the end of the selected period (no historical daily snapshots are kept).",
                .ForeColor = UiTheme.TextMuted, .Font = New Font("Segoe UI", 8.0F, FontStyle.Italic), .AutoSize = True, .Location = New Point(0, 60), .MaximumSize = New Size(900, 0)
            })

            p.Controls.Add(New Label With {.Text = "Movement Summary", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 90)})
            Dim gridSummary As New DataGridView With {.Location = New Point(0, 112), .Size = New Size(1180, 160)}
            UiTheme.StyleGrid(gridSummary)
            p.Controls.Add(gridSummary)

            p.Controls.Add(New Label With {.Text = "Stock Received (In)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 282)})
            Dim gridIn As New DataGridView With {.Location = New Point(0, 304), .Size = New Size(1180, 150)}
            UiTheme.StyleGrid(gridIn)
            p.Controls.Add(gridIn)

            p.Controls.Add(New Label With {.Text = "Stock Dispensed (Out)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 464)})
            Dim gridOut = MakeGrid(486)
            gridOut.Size = New Size(1180, 150)
            p.Controls.Add(gridOut)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim fromD = dtpFrom.Value.Date
                        Dim toD = dtpTo.Value.Date.AddDays(1).AddSeconds(-1)
                        Dim medFilter = txtMedicine.Text.Trim()
                        Dim medLike = "%" & medFilter & "%"

                        gridSummary.DataSource = Db.Query(
                            "SELECT m.medicine_name AS Medicine,
                                    COALESCE((SELECT SUM(b.quantity_received) FROM medicine_stock_batches b WHERE b.medicine_id = m.medicine_id AND b.date_received BETWEEN @From AND @To), 0) AS 'Stock In',
                                    COALESCE((SELECT SUM(pd.quantity_dispensed) FROM pharmacy_dispensing pd WHERE pd.medicine_name = m.medicine_name AND pd.date_dispensed BETWEEN @From AND @To), 0) AS 'Stock Out',
                                    COALESCE((SELECT SUM(b2.quantity_remaining) FROM medicine_stock_batches b2 WHERE b2.medicine_id = m.medicine_id), 0) AS 'On Hand',
                                    m.minimum_stock_level AS 'Min. Stock'
                             FROM medicines m
                             WHERE m.status = 'Active' AND (@MedFilter = '' OR m.medicine_name LIKE @MedLike)
                             ORDER BY m.medicine_name",
                            New With {.From = fromD, .To = toD, .MedFilter = medFilter, .MedLike = medLike})

                        gridIn.DataSource = Db.Query(
                            $"SELECT b.date_received AS Date, m.medicine_name AS Medicine, b.quantity_received AS 'Quantity Received',
                                    b.batch_lot_no AS 'Batch/Lot No.', b.source AS Source, u.full_name AS 'Received By'
                             FROM medicine_stock_batches b
                             JOIN medicines m ON m.medicine_id = b.medicine_id
                             LEFT JOIN users_staff u ON u.user_id = b.received_by
                             WHERE b.date_received BETWEEN @From AND @To
                               AND (@MedFilter = '' OR m.medicine_name LIKE @MedLike)
                             ORDER BY b.date_received DESC
                             LIMIT {UiTheme.ResultCap}", New With {.From = fromD, .To = toD, .MedFilter = medFilter, .MedLike = medLike})

                        gridOut.DataSource = Db.Query(
                            $"SELECT pd.date_dispensed AS Date, pd.medicine_name AS Medicine, pd.quantity_dispensed AS 'Quantity Dispensed',
                                    CONCAT(p2.last_name, ', ', p2.first_name) AS Patient, u.full_name AS 'Dispensed By'
                             FROM pharmacy_dispensing pd
                             JOIN patients p2 ON p2.patient_id = pd.patient_id
                             LEFT JOIN users_staff u ON u.user_id = pd.dispensed_by
                             WHERE pd.date_dispensed BETWEEN @From AND @To
                               AND (@MedFilter = '' OR pd.medicine_name LIKE @MedLike)
                             ORDER BY pd.date_dispensed DESC
                             LIMIT {UiTheme.ResultCap}", New With {.From = fromD, .To = toD, .MedFilter = medFilter, .MedLike = medLike})
                    Catch ex As Exception
                        MessageBox.Show("Error loading report: " & ex.Message)
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            UiTheme.AttachDebouncedSearch(txtMedicine, refresh)
            WireExportMenu(btnExport, "Medicine Inventory In/Out Report", "InventoryReport",
                Function() New List(Of (String, DataTable)) From {
                    ("Movement Summary", CType(gridSummary.DataSource, DataTable)),
                    ("Stock Received (In)", CType(gridIn.DataSource, DataTable)),
                    ("Stock Dispensed (Out)", CType(gridOut.DataSource, DataTable))
                })
            refresh()
            Return p
        End Function

        ' ---------------- 6. Patient Medication Profile Report (Figure 38) ----------------
        Private Function BuildPatientMedicationProfileReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "Patient Medication Profile")

            p.Controls.Add(New Label With {.Text = "Patient (type to search)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 44)})
            Dim cboPatient As New ComboBox With {.Location = New Point(0, 64), .Width = 360}
            UiTheme.AttachSearchableCombo(cboPatient, 2,
                Function(text) SearchPatients(text))
            p.Controls.Add(cboPatient)

            Dim lblInfo = New Label With {.AutoSize = True, .Location = New Point(0, 104), .MaximumSize = New Size(1000, 0)}
            lblInfo.Text = "Type at least 2 letters of a patient's name or ID above, then pick them from the list."
            p.Controls.Add(lblInfo)

            Dim grid = MakeGrid(160)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    If cboPatient.SelectedItem Is Nothing Then Return
                    Dim patientId = CType(cboPatient.SelectedItem, PatientPickItem).Id
                    Try
                        Dim pt = Db.Query("SELECT * FROM patients WHERE patient_id = @Id", New With {.Id = patientId})
                        If pt.Rows.Count > 0 Then
                            Dim r = pt.Rows(0)
                            lblInfo.Text =
                                $"Conditions: {If(IsDBNull(r("existing_conditions")) OrElse r("existing_conditions").ToString() = "", "None declared", r("existing_conditions"))}" & vbCrLf &
                                $"Allergies: {If(IsDBNull(r("known_allergies")) OrElse r("known_allergies").ToString() = "", "None declared", r("known_allergies"))}"
                        End If

                        grid.DataSource = Db.Query(
                            "SELECT rx.medication_date AS Date, rx.prescription_number AS 'Rx No.', rx.medicine_name AS Medicine, rx.dosage AS Dosage,
                                    rx.frequency AS Frequency, rx.quantity AS Prescribed,
                                    COALESCE((SELECT SUM(pd.quantity_dispensed) FROM pharmacy_dispensing pd WHERE pd.prescription_id = rx.prescription_id), 0) AS Dispensed,
                                    rx.status AS Status
                             FROM prescriptions_medication_orders rx
                             JOIN encounters e ON e.encounter_id = rx.encounter_id
                             WHERE e.patient_id = @Id
                             ORDER BY rx.medication_date DESC", New With {.Id = patientId})
                    Catch ex As Exception
                        lblInfo.Text = "Error: " & ex.Message
                    End Try
                End Sub
            AddHandler cboPatient.SelectedIndexChanged, Sub() refresh()
            WireExportMenu(btnExport, "Patient Medication Profile", "PatientMedicationProfile",
                Function() New List(Of (String, DataTable)) From {("Medication History", CType(grid.DataSource, DataTable))})
            Return p
        End Function

        ''' <summary>Backs the searchable patient combo above -- and BuildConsultationReport's/BuildPrescriptionReport's patient filters -- with a bounded, indexed-friendly query instead of ever loading all 10,000+ patients into a ComboBox.</summary>
        Private Function SearchPatients(text As String) As List(Of Object)
            Dim results As New List(Of Object)
            Try
                Dim likePattern = "%" & text.Trim() & "%"
                For Each row As DataRow In Db.Query(
                    "SELECT patient_id, patient_no, first_name, last_name FROM patients
                     WHERE first_name LIKE @Like OR last_name LIKE @Like OR patient_no LIKE @Like
                     ORDER BY last_name, first_name LIMIT 20", New With {.Like = likePattern}).Rows
                    results.Add(New PatientPickItem(Convert.ToInt32(row("patient_id")), $"{row("last_name")}, {row("first_name")} ({row("patient_no")})"))
                Next
            Catch
                ' leave results empty -- the combo just won't show anything this keystroke
            End Try
            Return results
        End Function

        Private Class PatientPickItem
            Public ReadOnly Id As Integer
            Public ReadOnly Label As String
            Public Sub New(id As Integer, label As String)
                Me.Id = id : Me.Label = label
            End Sub
            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

        ' ---------------- shared filter builders ----------------

        Private Function BuildDateRangeFilter(container As Panel) As (dtpFrom As DateTimePicker, dtpTo As DateTimePicker)
            container.Controls.Add(New Label With {.Text = "From", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 44)})
            Dim dtpFrom As New DateTimePicker With {.Location = New Point(0, 64), .Width = 150, .Format = DateTimePickerFormat.Short, .Value = New DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)}
            container.Controls.Add(dtpFrom)

            container.Controls.Add(New Label With {.Text = "To", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(170, 44)})
            Dim dtpTo As New DateTimePicker With {.Location = New Point(170, 64), .Width = 150, .Format = DateTimePickerFormat.Short, .Value = DateTime.Today}
            container.Controls.Add(dtpTo)

            Return (dtpFrom, dtpTo)
        End Function

        Private Function BuildDateRangeAndStatusFilter(container As Panel, statusOptions As String()) As (dtpFrom As DateTimePicker, dtpTo As DateTimePicker, cboStatus As ComboBox)
            Dim range = BuildDateRangeFilter(container)
            container.Controls.Add(New Label With {.Text = "Status", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(340, 44)})
            Dim cboStatus As New ComboBox With {.Location = New Point(340, 64), .Width = 170, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange(statusOptions)
            cboStatus.SelectedIndex = 0
            container.Controls.Add(cboStatus)
            Return (range.dtpFrom, range.dtpTo, cboStatus)
        End Function

    End Class

End Namespace
