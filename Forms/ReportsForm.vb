Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>
    ''' Reports shell (Table 4 in the proposal): a left list of the 10 report
    ''' templates, a right content panel that swaps in each report's filter
    ''' + grid + CSV export. Individual report builders live in the sibling
    ''' partial-class files ReportsForm.PatientReports.vb,
    ''' ReportsForm.ClinicalReports.vb, ReportsForm.PharmacyReports.vb, and
    ''' ReportsForm.AuditReport.vb.
    ''' </summary>
    Public Partial Class ReportsForm
        Inherits Form

        Private lstReports As ListBox
        Private pnlContent As Panel

        Private ReadOnly reportNames As String() = {
            "1. Master List Report",
            "2. Family Folder Report",
            "3. Inventory In/Out Report",
            "4. Prescription Report",
            "5. Morbidity Report",
            "6. Patient Medication Profile",
            "7. Case Finding",
            "8. Treatment Report",
            "9. Audit Logs",
            "10. Consultation Report"
        }

        Public Const AuditLogsReportIndex As Integer = 8 ' "9. Audit Logs" above -- used by MainDashboardForm's standalone "Audit Logs" sidebar item

        ''' <summary>Pass a specific reportNames index (e.g. AuditLogsReportIndex) to open the shell straight into that report instead of the default Master List.</summary>
        Public Sub New(Optional initialReportIndex As Integer = 0)
            InitializeComponent()
            lstReports.SelectedIndex = Math.Max(0, Math.Min(initialReportIndex, reportNames.Length - 1))
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = UiTheme.BaseFont

            lstReports = New ListBox With {
                .Dock = DockStyle.Left, .Width = 240, .BorderStyle = BorderStyle.None,
                .Font = UiTheme.BaseFont, .IntegralHeight = False, .BackColor = Color.White
            }
            lstReports.Items.AddRange(reportNames)
            AddHandler lstReports.SelectedIndexChanged, AddressOf LstReports_SelectedIndexChanged
            Me.Controls.Add(lstReports)

            pnlContent = New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(20), .AutoScroll = True}
            Me.Controls.Add(pnlContent)
            pnlContent.BringToFront()
        End Sub

        Private Sub LstReports_SelectedIndexChanged(sender As Object, e As EventArgs)
            UiTheme.ClearAndDispose(pnlContent)
            Dim panel As Panel
            Select Case lstReports.SelectedIndex
                Case 0 : panel = BuildMasterListReport()
                Case 1 : panel = BuildFamilyFolderReport()
                Case 2 : panel = BuildInventoryReport()
                Case 3 : panel = BuildPrescriptionReport()
                Case 4 : panel = BuildMorbidityReport()
                Case 5 : panel = BuildPatientMedicationProfileReport()
                Case 6 : panel = BuildCaseFindingReport()
                Case 7 : panel = BuildTreatmentReport()
                Case 8 : panel = BuildAuditLogReport()
                Case 9 : panel = BuildConsultationReport()
                Case Else : panel = New Panel()
            End Select
            ' Every report lays out its grids at a fixed pixel width/height rather than
            ' resizing them to the viewport, so make the panel scrollable: it fits as-is
            ' on a wide/maximized window, and gains scrollbars instead of clipping content
            ' on a smaller one.
            panel.AutoScroll = True
            panel.Dock = DockStyle.Fill
            pnlContent.Controls.Add(panel)
        End Sub

        ' ---------------- shared helpers used by every report ----------------

        Private Function ReportHeader(container As Panel, title As String) As Button
            container.Controls.Add(New Label With {.Text = title, .Font = UiTheme.SubHeaderFont, .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(0, 0)})
            Dim btnExport As New Button With {.Text = "Export ▾", .Size = New Size(120, 32), .Location = New Point(0, 0)}
            UiTheme.StyleSecondaryButton(btnExport)
            container.Controls.Add(btnExport)
            AddHandler container.SizeChanged, Sub() btnExport.Left = Math.Max(container.Width - btnExport.Width - 24, 0)
            btnExport.Left = Math.Max(container.Width - btnExport.Width - 24, 0)
            Return btnExport
        End Function

        ''' <summary>
        ''' Wires an "Export" button to a small menu offering CSV / PDF / Excel.
        ''' `sections` is re-evaluated every time the menu opens, so it always
        ''' reflects whatever the report's grids currently show (after filters
        ''' change). CSV only ever writes the first section (CSV has no concept
        ''' of multiple tables); PDF and Excel include every section given.
        ''' </summary>
        Private Sub WireExportMenu(btnExport As Button, reportTitle As String, fileBaseName As String, sections As Func(Of List(Of (Label As String, Table As DataTable))))
            Dim menu As New ContextMenuStrip()
            menu.Items.Add("Export as CSV...", Nothing, Sub(s, e)
                                                             Dim list = sections()
                                                             CsvExporter.ExportToCsv(If(list.Count > 0, list(0).Table, Nothing), fileBaseName & ".csv")
                                                         End Sub)
            menu.Items.Add("Export as PDF...", Nothing, Sub(s, e) PdfExporter.ExportToPdf(reportTitle, sections(), fileBaseName & ".pdf"))
            menu.Items.Add("Export as Excel...", Nothing, Sub(s, e) ExcelExporter.ExportToExcel(sections(), fileBaseName & ".xlsx"))
            AddHandler btnExport.Click, Sub() menu.Show(btnExport, New Point(0, btnExport.Height))
        End Sub

        Private Function MakeGrid(y As Integer) As DataGridView
            Dim grid As New DataGridView With {.Location = New Point(0, y), .Size = New Size(1180, 420)}
            UiTheme.StyleGrid(grid)
            Return grid
        End Function

        Private Function MakeSummaryLabel(x As Integer, y As Integer) As Label
            Dim lbl As New Label With {.AutoSize = True, .Location = New Point(x, y), .ForeColor = UiTheme.TextMuted}
            Return lbl
        End Function

    End Class

End Namespace
