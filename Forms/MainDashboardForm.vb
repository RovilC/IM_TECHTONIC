Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports System.Windows.Forms.DataVisualization.Charting
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' Application shell (Figure 4): sidebar navigation filtered by the
    ''' logged-in role's module_access, a header bar, and a content area that
    ''' swaps in each module's form. Acts as the "MainForm" the login screen
    ''' hands off to.
    ''' </summary>
    Public Class MainDashboardForm
        Inherits Form

        Private pnlSidebar As Panel
        Private pnlContent As Panel
        Private lblPageTitle As Label
        Private lblWelcome As Label
        Private navButtons As New List(Of Button)
        Private currentChildForm As Form = Nothing

        ' Back navigation: drill-in screens (patient profile, registration, encounter) push the view
        ' they came from; the sidebar starts a fresh history. Each entry re-creates its view.
        Private ReadOnly backStack As New Stack(Of Action)
        Private currentView As Action = Nothing
        Private btnBack As Button

        ' (Label, ModuleKey). "Consultation" is special-cased in RebuildNavButtons: it renders as
        ' one parent button that expands a Triage / Encounter dropdown underneath it, instead of
        ' three separate top-level buttons that all used to open the exact same encounter list.
        ' "registration" deliberately has no sidebar entry: it only grants the Patient Directory's "+ Register Patient" button.
        ' Likewise "encounters" only grants starting/opening encounters from a patient's profile; the Consultation
        ' (encounter list) sidebar tab is its own key, "encounter_list", so a role can do encounter work without the tab.
        Private ReadOnly navItems As (String, String)() = {
            ("Dashboard", "dashboard"),
            ("Patient Directory", "patients"),
            ("Family / Household", "family"),
            ("Consultation", "encounter_list"),
            ("Laboratory", "lab"),
            ("Pharmacy", "pharmacy"),
            ("Medicine Inventory", "inventory"),
            ("Medical Certificates", "certificates"),
            ("Lab Requests", "lab_requests"),
            ("Med Certs", "medcerts"),
            ("Reports", "reports"),
            ("User Management", "users"),
            ("Roles & Permissions", "roles"),
            ("Audit Logs", "audit")
        }

        Private consultationExpanded As Boolean = False

        Public Sub New()
            InitializeComponent()
        End Sub

        Private Sub InitializeComponent()
            Me.Text = "RHU Vinzons - Project C.A.R.E"
            Me.WindowState = FormWindowState.Maximized
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.MinimumSize = New Size(1100, 700)
            Me.Font = UiTheme.BaseFont
            Me.BackColor = UiTheme.BackgroundLight

            BuildSidebar()
            BuildHeaderAndContent()

            AddHandler Me.Shown, Sub() NavigateTo("dashboard")
            ' Closing the window with the X still protects unsaved work on the open screen.
            AddHandler Me.FormClosing, Sub(s, e) If e.CloseReason = CloseReason.UserClosing AndAlso Not ConfirmLeaveCurrent() Then e.Cancel = True
        End Sub

        Private Sub BuildSidebar()
            pnlSidebar = New Panel With {
                .Dock = DockStyle.Left,
                .Width = 240,
                .BackColor = UiTheme.SidebarDark,
                .AutoScroll = True
            }
            Me.Controls.Add(pnlSidebar)

            Dim lblLogo As New Label With {
                .Text = "RHU Vinzons",
                .ForeColor = Color.White,
                .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
                .AutoSize = True,
                .Location = New Point(20, 18)
            }
            pnlSidebar.Controls.Add(lblLogo)
            pnlSidebar.Controls.Add(New Label With {
                .Text = "Camarines Norte",
                .ForeColor = Color.FromArgb(160, 200, 180),
                .Font = New Font("Segoe UI", 8.0F),
                .AutoSize = True,
                .Location = New Point(20, 44)
            })

            RebuildNavButtons()

            Dim btnLogout As New Button With {
                .Text = "   Log Out",
                .FlatStyle = FlatStyle.Flat,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Dock = DockStyle.Bottom,
                .Height = 46,
                .ForeColor = Color.White,
                .BackColor = UiTheme.PrimaryGreenDark,
                .Font = UiTheme.BoldFont,
                .Cursor = Cursors.Hand
            }
            btnLogout.FlatAppearance.BorderSize = 0
            AddHandler btnLogout.Click, AddressOf BtnLogout_Click
            pnlSidebar.Controls.Add(btnLogout)
        End Sub

        ''' <summary>
        ''' (Re)builds the nav button stack. Pulled out of BuildSidebar so
        ''' toggling the Consultation dropdown (Triage / Encounter) can just
        ''' throw the whole stack away and relay it out top-to-bottom, rather
        ''' than hand-shuffling every button below it up or down by 36-42px.
        ''' Only ~13 buttons, so a full rebuild on toggle is cheap.
        ''' </summary>
        Private Sub RebuildNavButtons()
            For Each old In navButtons
                pnlSidebar.Controls.Remove(old)
                old.Dispose()
            Next
            navButtons.Clear()

            Dim y As Integer = 80
            For Each item In navItems
                Dim label = item.Item1
                Dim moduleKey = item.Item2
                If Session.Current Is Nothing OrElse Not Session.Current.HasModule(moduleKey) Then Continue For

                If label = "Consultation" Then
                    Dim arrow = If(consultationExpanded, ChrW(&H25BC), ChrW(&H25B6)) ' v when expanded, > when collapsed
                    Dim parent = MakeNavButton($"   {label}   {arrow}", moduleKey, y)
                    AddHandler parent.Click, Sub()
                                                  consultationExpanded = Not consultationExpanded
                                                  RebuildNavButtons()
                                                  NavigateTo(moduleKey, label)
                                              End Sub
                    y += 42

                    If consultationExpanded Then
                        For Each subItem In {"Triage", "Encounter"}
                            Dim subLabel = subItem
                            Dim subBtn = MakeNavButton("        " & subLabel, moduleKey, y, 36)
                            AddHandler subBtn.Click, Sub() NavigateTo(moduleKey, subLabel)
                            y += 36
                        Next
                    End If
                Else
                    Dim btn = MakeNavButton("   " & label, moduleKey, y)
                    AddHandler btn.Click, Sub() NavigateTo(moduleKey, label)
                    y += 42
                End If
            Next
        End Sub

        Private Function MakeNavButton(text As String, moduleKey As String, y As Integer, Optional height As Integer = 42) As Button
            Dim btn As New Button With {
                .Text = text,
                .Tag = moduleKey,
                .FlatStyle = FlatStyle.Flat,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Location = New Point(0, y),
                .Size = New Size(240, height),
                .ForeColor = Color.White,
                .BackColor = UiTheme.SidebarDark,
                .Font = UiTheme.BaseFont,
                .Cursor = Cursors.Hand
            }
            btn.FlatAppearance.BorderSize = 0
            btn.FlatAppearance.MouseOverBackColor = UiTheme.SidebarActive
            pnlSidebar.Controls.Add(btn)
            navButtons.Add(btn)
            Return btn
        End Function

        Private Sub BuildHeaderAndContent()
            Dim outer As New Panel With {.Dock = DockStyle.Fill, .BackColor = UiTheme.BackgroundLight}
            Me.Controls.Add(outer)
            outer.BringToFront()

            Dim header As New Panel With {
                .Dock = DockStyle.Top,
                .Height = 64,
                .BackColor = Color.White,
                .Padding = New Padding(24, 0, 24, 0)
            }
            outer.Controls.Add(header)

            lblPageTitle = New Label With {
                .Text = "Dashboard",
                .Font = UiTheme.SubHeaderFont,
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(24, 20)
            }
            header.Controls.Add(lblPageTitle)

            btnBack = New Button With {.Text = "← Back", .Size = New Size(90, 34), .Location = New Point(24, 15), .Visible = False}
            UiTheme.StyleSecondaryButton(btnBack)
            btnBack.Height = 34
            AddHandler btnBack.Click, Sub() GoBack()
            header.Controls.Add(btnBack)

            lblWelcome = New Label With {
                .Text = "",
                .Font = UiTheme.BaseFont,
                .ForeColor = UiTheme.TextMuted,
                .AutoSize = True
            }
            header.Controls.Add(lblWelcome)
            UiTheme.PinToRight(lblWelcome, header, 24, 22)

            Dim bottomBorder As New Panel With {.Dock = DockStyle.Bottom, .Height = 1, .BackColor = UiTheme.BorderLight}
            header.Controls.Add(bottomBorder)

            pnlContent = New Panel With {
                .Dock = DockStyle.Fill,
                .BackColor = UiTheme.BackgroundLight,
                .Padding = New Padding(24)
            }
            outer.Controls.Add(pnlContent)
            pnlContent.BringToFront()

            If Session.IsLoggedIn Then
                lblWelcome.Text = $"{Session.Current.FullName}  -  {Session.Current.RoleName}"
            End If
        End Sub

        Private Sub BtnLogout_Click(sender As Object, e As EventArgs)
            If Not ConfirmLeaveCurrent() Then Return
            Dim confirm = MessageBox.Show("Log out of Project C.A.R.E?", "Confirm Log Out", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If confirm = DialogResult.Yes Then
                currentChildForm = Nothing ' already confirmed; don't ask again while closing
                AuditLogger.Log("LOGOUT", "auth", Session.Current?.Username, $"{Session.Current?.FullName} signed out")
                Me.Close()
            End If
        End Sub

        ''' <summary>Sidebar navigation: opens a module as a fresh starting point (clears the Back history).</summary>
        Public Sub NavigateTo(moduleKey As String, Optional pageTitle As String = Nothing)
            If Not ConfirmLeaveCurrent() Then Return
            backStack.Clear()
            currentView = Sub() ShowModule(moduleKey, pageTitle)
            currentView.Invoke()
            UpdateBackButton()
        End Sub

        ''' <summary>Drill-in navigation: remembers the current view so Back returns to it.</summary>
        Private Sub GoToView(view As Action)
            If Not ConfirmLeaveCurrent() Then Return
            If currentView IsNot Nothing Then backStack.Push(currentView)
            currentView = view
            view.Invoke()
            UpdateBackButton()
        End Sub

        Private Sub GoBack()
            If backStack.Count = 0 Then Return
            If Not ConfirmLeaveCurrent() Then Return
            currentView = backStack.Pop()
            currentView.Invoke()
            UpdateBackButton()
        End Sub

        Private Sub UpdateBackButton()
            btnBack.Visible = backStack.Count > 0
            lblPageTitle.Left = If(btnBack.Visible, btnBack.Right + 14, 24)
        End Sub

        ''' <summary>Lets a screen with unsaved work (registration, encounter) ask before it is closed.</summary>
        Private Function ConfirmLeaveCurrent() As Boolean
            Dim guard = TryCast(currentChildForm, IConfirmLeave)
            Return guard Is Nothing OrElse guard.ConfirmLeave()
        End Function

        ''' <summary>Swaps the content panel to the requested module. "dashboard" renders the stat-card home view inline.</summary>
        Private Sub ShowModule(moduleKey As String, pageTitle As String)
            HighlightActiveButton(moduleKey)
            lblPageTitle.Text = If(pageTitle, "Dashboard")

            If currentChildForm IsNot Nothing Then
                currentChildForm.Close()
                currentChildForm.Dispose()
                currentChildForm = Nothing
            End If
            UiTheme.ClearAndDispose(pnlContent)

            Dim childForm As Form = Nothing
            Select Case moduleKey
                Case "dashboard"
                    lblPageTitle.Text = "Dashboard"
                    pnlContent.Controls.Add(BuildDashboardHomePanel())
                    Return
                Case "patients"
                    childForm = New PatientDirectoryForm(AddressOf OpenPatientProfile, AddressOf OpenRegistration)
                Case "registration"
                    childForm = New PatientRegistrationForm(Nothing, AddressOf OnNewPatientRegistered)
                Case "family"
                    childForm = New FamilyHouseholdForm()
                Case "encounters", "encounter_list"
                    childForm = New EncounterListForm(AddressOf OpenEncounterWorkspace)
                Case "lab"
                    childForm = New LaboratoryForm()
                Case "pharmacy"
                    childForm = New PharmacyForm()
                Case "inventory"
                    childForm = New MedicineInventoryForm()
                Case "certificates"
                    childForm = New MedicalCertificatesForm()
                Case "lab_requests"
                    childForm = New LabRequestApprovalsForm()
                Case "medcerts"
                    childForm = New MedCertApprovalsForm()
                Case "reports"
                    childForm = New ReportsForm()
                Case "users"
                    childForm = New UserManagementForm()
                Case "roles"
                    childForm = New RolesPermissionsForm()
                Case "audit"
                    childForm = New ReportsForm(ReportsForm.AuditLogsReportIndex, lockToInitialReport:=True)
                Case Else
                    childForm = New PlaceholderForm(pageTitle, "This module is not available yet.")
            End Select

            If childForm IsNot Nothing Then
                childForm.TopLevel = False
                childForm.FormBorderStyle = FormBorderStyle.None
                childForm.Dock = DockStyle.Fill
                pnlContent.Controls.Add(childForm)
                childForm.Show()
                currentChildForm = childForm
            End If
        End Sub

        Private Sub HighlightActiveButton(moduleKey As String)
            For Each btn In navButtons
                If btn.Tag IsNot Nothing AndAlso btn.Tag.ToString() = moduleKey Then
                    btn.BackColor = UiTheme.SidebarActive
                Else
                    btn.BackColor = UiTheme.SidebarDark
                End If
            Next
        End Sub

        ' ---- cross-module navigation callbacks ----
        Private Sub OpenPatientProfile(patientId As Integer)
            GoToView(Sub() ShowPatientProfile(patientId))
        End Sub

        Private Sub ShowPatientProfile(patientId As Integer)
            HighlightActiveButton("patients")
            lblPageTitle.Text = "Patient Profile"
            If currentChildForm IsNot Nothing Then
                currentChildForm.Close()
                currentChildForm.Dispose()
            End If
            UiTheme.ClearAndDispose(pnlContent)

            Dim frm As New PatientProfileForm(patientId, AddressOf OpenEncounterWorkspace)
            frm.TopLevel = False
            frm.FormBorderStyle = FormBorderStyle.None
            frm.Dock = DockStyle.Fill
            pnlContent.Controls.Add(frm)
            frm.Show()
            currentChildForm = frm
        End Sub

        Private Sub OpenRegistration()
            GoToView(Sub() ShowModule("registration", "Register Patient"))
        End Sub

        ''' <summary>
        ''' Fires after a brand-new patient is saved from the sidebar's
        ''' Register Patient screen (edit-mode registration -- reached only
        ''' via PatientProfileForm -- wires its own onSaved and never hits
        ''' this). Offers to jump straight into triage; the actual navigation
        ''' is deferred via BeginInvoke because we're still on the call stack
        ''' of PatientRegistrationForm's own Save click handler, which calls
        ''' ResetWizard() right after this returns -- disposing that form
        ''' synchronously here (by opening a new workspace over it) would
        ''' leave ResetWizard() touching disposed controls.
        ''' </summary>
        Private Sub OnNewPatientRegistered(patientId As Integer)
            Dim goToTriage = MessageBox.Show(
                "Start triage/encounter for this patient now?", "Patient Registered",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
            If Not goToTriage Then Return
            Me.BeginInvoke(Sub() StartEncounterForNewPatient(patientId))
        End Sub

        Private Sub StartEncounterForNewPatient(patientId As Integer)
            Dim name = Db.QueryScalar("SELECT CONCAT(last_name, ', ', first_name) FROM patients WHERE patient_id = @Id", New With {.Id = patientId})
            Using dlg As New NewEncounterDialog(patientId, If(name, "").ToString())
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.NewEncounterId.HasValue Then
                    OpenEncounterWorkspace(dlg.NewEncounterId.Value)
                End If
            End Using
        End Sub

        Private Sub OpenEncounterWorkspace(encounterId As Integer)
            GoToView(Sub() ShowEncounterWorkspace(encounterId))
        End Sub

        Private Sub ShowEncounterWorkspace(encounterId As Integer)
            HighlightActiveButton("encounters")
            lblPageTitle.Text = "Encounter"
            If currentChildForm IsNot Nothing Then
                currentChildForm.Close()
                currentChildForm.Dispose()
            End If
            UiTheme.ClearAndDispose(pnlContent)

            Dim frm As New EncounterWorkspaceForm(encounterId)
            frm.TopLevel = False
            frm.FormBorderStyle = FormBorderStyle.None
            frm.Dock = DockStyle.Fill
            pnlContent.Controls.Add(frm)
            frm.Show()
            currentChildForm = frm
        End Sub

        ' ---- Dashboard home (stat cards + recent activity, Figure 4) ----
        Private Function BuildDashboardHomePanel() As Panel
            Dim root As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}

            Dim cardsFlow As New FlowLayoutPanel With {
                .Location = New Point(0, 0),
                .Size = New Size(1400, 130),
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .AutoSize = True
            }
            root.Controls.Add(cardsFlow)

            Dim stats = LoadDashboardStats()
            For Each stat In stats
                cardsFlow.Controls.Add(BuildStatCard(stat.Item1, stat.Item2, stat.Item3))
            Next

            Dim lblCharts As New Label With {
                .Text = "Trends & Breakdown",
                .Font = UiTheme.SubHeaderFont,
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(4, 150)
            }
            root.Controls.Add(lblCharts)
            root.Controls.Add(BuildChartsRow())

            Dim lblRecent As New Label With {
                .Text = "Recent Audit Activity",
                .Font = UiTheme.SubHeaderFont,
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(4, 500)
            }
            root.Controls.Add(lblRecent)

            Dim grid As New DataGridView With {
                .Location = New Point(4, 534),
                .Size = New Size(1180, 320)
            }
            UiTheme.StyleGrid(grid)
            root.Controls.Add(grid)

            Try
                grid.DataSource = Db.Query(
                    "SELECT created_at AS Time, username_snapshot AS 'User', role_snapshot AS Role, action AS Action, module AS Module, description AS Details
                     FROM audit_logs ORDER BY created_at DESC LIMIT 25")
            Catch ex As Exception
                root.Controls.Add(New Label With {.Text = "Could not load audit activity: " & ex.Message, .ForeColor = UiTheme.DangerRed, .Location = New Point(4, 184), .AutoSize = True})
            End Try

            Return root
        End Function

        Private Function LoadDashboardStats() As List(Of (String, String, Color))
            Dim result As New List(Of (String, String, Color))
            Try
                Dim patients = Db.QueryScalar("SELECT COUNT(*) FROM patients")
                result.Add(("Registered Patients", If(patients, 0).ToString(), UiTheme.PrimaryGreen))

                Dim households = Db.QueryScalar("SELECT COUNT(*) FROM families_households")
                result.Add(("Households", If(households, 0).ToString(), UiTheme.PrimaryGreen))

                Dim todayEnc = Db.QueryScalar("SELECT COUNT(*) FROM encounters WHERE DATE(encounter_date) = CURDATE()")
                result.Add(("Today's Encounters", If(todayEnc, 0).ToString(), UiTheme.AccentGreen))

                Dim openEnc = Db.QueryScalar("SELECT COUNT(*) FROM encounters WHERE status <> 'Closed'")
                result.Add(("Open Encounters", If(openEnc, 0).ToString(), UiTheme.AccentGreen))

                Dim users = Db.QueryScalar("SELECT COUNT(*) FROM users_staff WHERE status = 'Active'")
                result.Add(("Active Staff Accounts", If(users, 0).ToString(), UiTheme.TextDark))

                Dim pendingLab = Db.QueryScalar("SELECT COUNT(*) FROM laboratory WHERE status = 'Pending'")
                result.Add(("Pending Lab Requests", If(pendingLab, 0).ToString(), UiTheme.WarningAmber))

                Dim labForApproval = Db.QueryScalar("SELECT COUNT(DISTINCT COALESCE(request_no, laboratory_id)) FROM laboratory WHERE status = 'For Approval'")
                result.Add(("Lab Requests For Issuance", If(labForApproval, 0).ToString(), UiTheme.WarningAmber))

                Dim certsForApproval = Db.QueryScalar("SELECT COUNT(*) FROM other_documents_services WHERE document_type = 'Medical Certificate' AND status = 'For Approval'")
                result.Add(("Med Certs For Approval", If(certsForApproval, 0).ToString(), UiTheme.WarningAmber))

                Dim lowStock = Db.QueryScalar(
                    "SELECT COUNT(*) FROM (
                        SELECT m.medicine_id, m.minimum_stock_level, COALESCE(SUM(b.quantity_remaining),0) AS remaining
                        FROM medicines m LEFT JOIN medicine_stock_batches b ON b.medicine_id = m.medicine_id
                        GROUP BY m.medicine_id, m.minimum_stock_level
                        HAVING remaining < m.minimum_stock_level
                     ) x")
                result.Add(("Low-Stock Medicines", If(lowStock, 0).ToString(), UiTheme.DangerRed))

                Dim expiring = Db.QueryScalar(
                    "SELECT COUNT(*) FROM medicine_stock_batches
                     WHERE quantity_remaining > 0 AND expiration_date IS NOT NULL
                       AND expiration_date <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)")
                result.Add(("Expiring <=30 Days", If(expiring, 0).ToString(), UiTheme.WarningAmber))

            Catch ex As Exception
                result.Add(("Database Error", ex.Message, UiTheme.DangerRed))
            End Try
            Return result
        End Function

        ' ---- Dashboard charts (encounter trend, medicine stock breakdown, lab request breakdown) ----
        Private Function BuildChartsRow() As Panel
            Dim row As New Panel With {.Location = New Point(4, 184), .Size = New Size(1180, 300)}

            Dim charts = New Chart() {BuildEncounterTrendChart(), BuildStockStatusChart(), BuildLabStatusChart()}
            Const gap As Integer = 16
            Dim chartWidth = (row.Width - gap * (charts.Length - 1)) \ charts.Length
            Dim x As Integer = 0
            For Each c In charts
                c.Location = New Point(x, 0)
                c.Size = New Size(chartWidth, row.Height)
                row.Controls.Add(c)
                x += chartWidth + gap
            Next

            Return row
        End Function

        ''' <summary>Common chart chrome: card-like white background, muted border, crisp text, and no distracting 3D/shadow effects.</summary>
        Private Function NewChart(chartTitle As String) As Chart
            Dim chart As New Chart With {
                .BackColor = Color.White,
                .BorderlineColor = UiTheme.BorderLight,
                .BorderlineWidth = 1,
                .BorderlineDashStyle = ChartDashStyle.Solid,
                .AntiAliasing = AntiAliasingStyles.All,
                .TextAntiAliasingQuality = TextAntiAliasingQuality.High,
                .Palette = ChartColorPalette.None
            }
            chart.Titles.Add(New Title(chartTitle, Docking.Top, UiTheme.SubHeaderFont, UiTheme.TextDark) With {.Alignment = ContentAlignment.MiddleLeft})
            Return chart
        End Function

        Private Sub ShowChartError(chart As Chart, ex As Exception)
            chart.Titles.Add(New Title("Could not load: " & ex.Message, Docking.Bottom, UiTheme.BaseFont, UiTheme.DangerRed))
        End Sub

        ''' <summary>Smooth area trend of daily encounter counts for the last 14 days, with days that had zero encounters shown as zero rather than skipped. Hovering a point shows the exact date and count.</summary>
        Private Function BuildEncounterTrendChart() As Chart
            Dim chart = NewChart("Encounters -- Last 14 Days")

            Dim area As New ChartArea With {.BackColor = Color.White}
            area.AxisX.LabelStyle.Angle = -45
            area.AxisX.LabelStyle.Font = UiTheme.BaseFont
            ' Explicit Interval -- left on Auto, the control's collision avoidance for angled
            ' category labels over-thins these 14 daily labels down to just one, leaving the
            ' trend unreadable. Forcing every-other-day keeps ~7 evenly spread date labels.
            area.AxisX.Interval = 2
            area.AxisX.MajorGrid.LineColor = UiTheme.BorderLight
            area.AxisX.LineColor = UiTheme.BorderLight
            area.AxisX.MajorTickMark.LineColor = UiTheme.BorderLight
            area.AxisY.MajorGrid.LineColor = UiTheme.BorderLight
            area.AxisY.LabelStyle.Format = "0"
            area.AxisY.LabelStyle.Font = UiTheme.BaseFont
            area.AxisY.LineColor = UiTheme.BorderLight
            area.AxisY.IsStartedFromZero = True
            chart.ChartAreas.Add(area)

            Dim series As New Series With {
                .ChartType = SeriesChartType.SplineArea,
                .Color = Color.FromArgb(50, UiTheme.PrimaryGreen),
                .BorderColor = UiTheme.PrimaryGreen,
                .BorderWidth = 3,
                .MarkerStyle = MarkerStyle.Circle,
                .MarkerSize = 6,
                .MarkerColor = UiTheme.PrimaryGreen,
                .MarkerBorderColor = Color.White,
                .MarkerBorderWidth = 1,
                .ToolTip = "#AXISLABEL: #VAL encounter(s)"
            }
            chart.Series.Add(series)

            Try
                Dim startDate = DateTime.Today.AddDays(-13)
                Dim counts As New Dictionary(Of DateTime, Integer)
                For i = 0 To 13
                    counts(startDate.AddDays(i)) = 0
                Next

                Dim dt = Db.Query(
                    "SELECT DATE(encounter_date) AS d, COUNT(*) AS c FROM encounters
                     WHERE encounter_date >= @Start GROUP BY DATE(encounter_date)",
                    New With {.Start = startDate})
                For Each r As DataRow In dt.Rows
                    Dim d = Convert.ToDateTime(r("d")).Date
                    If counts.ContainsKey(d) Then counts(d) = Convert.ToInt32(r("c"))
                Next

                ' Plot against a plain 1..14 numeric X so points land evenly spaced --
                ' passing "MM/dd" strings as X values let the axis auto-detect them as
                ' actual dates and collapse most points into a narrow band. AxisLabel
                ' carries the display text instead, decoupled from position.
                Dim total As Integer = 0
                Dim dayIndex As Integer = 1
                For Each kv In counts.OrderBy(Function(k) k.Key)
                    Dim idx = series.Points.AddXY(dayIndex, kv.Value)
                    series.Points(idx).AxisLabel = kv.Key.ToString("MM/dd")
                    total += kv.Value
                    dayIndex += 1
                Next
                chart.Titles(0).Text &= $"  ({total} total)"
            Catch ex As Exception
                ShowChartError(chart, ex)
            End Try

            Return chart
        End Function

        ''' <summary>Doughnut breakdown of the same Sufficient/Low Stock/Out of Stock split used by the Medicine Inventory grid and the "Low-Stock Medicines" stat card, so the numbers agree everywhere. Each slice is labeled with its share and a tooltip gives the exact count.</summary>
        Private Function BuildStockStatusChart() As Chart
            Dim chart = NewChart("Medicine Stock Status")
            chart.Legends.Add(New Legend With {.Docking = Docking.Bottom, .Font = UiTheme.BaseFont})

            Dim area As New ChartArea With {.BackColor = Color.White}
            chart.ChartAreas.Add(area)

            Dim series As New Series With {.ChartType = SeriesChartType.Doughnut}
            series("DoughnutRadius") = "55"
            series("PieLabelStyle") = "Outside"
            series.Font = UiTheme.BaseFont
            chart.Series.Add(series)

            Try
                Dim dt = Db.Query(
                    "SELECT Status, COUNT(*) AS c FROM (
                        SELECT m.medicine_id,
                               CASE
                                   WHEN COALESCE(SUM(b.quantity_remaining),0) <= 0 THEN 'Out of Stock'
                                   WHEN COALESCE(SUM(b.quantity_remaining),0) < m.minimum_stock_level THEN 'Low Stock'
                                   ELSE 'Sufficient'
                               END AS Status
                        FROM medicines m LEFT JOIN medicine_stock_batches b ON b.medicine_id = m.medicine_id
                        WHERE m.status = 'Active'
                        GROUP BY m.medicine_id, m.minimum_stock_level
                     ) x GROUP BY Status")

                If dt.Rows.Count = 0 Then
                    chart.Titles.Add(New Title("No active medicines yet", Docking.Bottom, UiTheme.BaseFont, UiTheme.TextMuted))
                    Return chart
                End If

                Dim total = dt.Rows.Cast(Of DataRow)().Sum(Function(r) Convert.ToInt32(r("c")))
                For Each r As DataRow In dt.Rows
                    Dim status = r("Status").ToString()
                    Dim count = Convert.ToInt32(r("c"))
                    Dim pct = If(total > 0, count / CDbl(total), 0)
                    Dim idx = series.Points.AddXY(status, count)
                    series.Points(idx).LegendText = $"{status} ({count})"
                    series.Points(idx).Label = pct.ToString("P0")
                    series.Points(idx).ToolTip = $"{status}: {count} ({pct:P0})"
                    series.Points(idx).Color = StockStatusColor(status)
                Next
            Catch ex As Exception
                ShowChartError(chart, ex)
            End Try

            Return chart
        End Function

        ''' <summary>
        ''' Doughnut breakdown of lab requests by status (Pending / In Progress /
        ''' Released), complementing the "Pending Lab Requests" stat card with the
        ''' full picture. A bar chart doesn't work here -- Released routinely
        ''' outnumbers Pending 8:1, so a shared value axis reduces the backlog to
        ''' an invisible sliver. Proportional slices make the (usually small,
        ''' reassuring) Pending+In Progress share legible regardless of scale.
        ''' </summary>
        Private Function BuildLabStatusChart() As Chart
            Dim chart = NewChart("Lab Requests by Status")
            chart.Legends.Add(New Legend With {.Docking = Docking.Bottom, .Font = UiTheme.BaseFont})

            Dim area As New ChartArea With {.BackColor = Color.White}
            chart.ChartAreas.Add(area)

            Dim series As New Series With {.ChartType = SeriesChartType.Doughnut}
            series("DoughnutRadius") = "55"
            series("PieLabelStyle") = "Outside"
            series.Font = UiTheme.BaseFont
            chart.Series.Add(series)

            Try
                Dim dt = Db.Query("SELECT status AS Status, COUNT(*) AS c FROM laboratory GROUP BY status")

                If dt.Rows.Count = 0 Then
                    chart.Titles.Add(New Title("No lab requests yet", Docking.Bottom, UiTheme.BaseFont, UiTheme.TextMuted))
                    Return chart
                End If

                Dim order = New String() {"Pending", "In Progress", "Released"}
                Dim byStatus = dt.Rows.Cast(Of DataRow)().ToDictionary(Function(r) r("Status").ToString(), Function(r) Convert.ToInt32(r("c")))
                Dim statuses = order.Where(Function(s) byStatus.ContainsKey(s)).
                    Concat(byStatus.Keys.Where(Function(s) Not order.Contains(s))).ToList()
                Dim total = byStatus.Values.Sum()

                For Each status In statuses
                    Dim count = byStatus(status)
                    Dim pct = If(total > 0, count / CDbl(total), 0)
                    Dim idx = series.Points.AddXY(status, count)
                    series.Points(idx).LegendText = $"{status} ({count})"
                    series.Points(idx).Label = pct.ToString("P0")
                    series.Points(idx).ToolTip = $"{status}: {count} ({pct:P0})"
                    series.Points(idx).Color = LabStatusColor(status)
                Next
            Catch ex As Exception
                ShowChartError(chart, ex)
            End Try

            Return chart
        End Function

        Private Function StockStatusColor(status As String) As Color
            Select Case status
                Case "Out of Stock" : Return UiTheme.DangerRed
                Case "Low Stock" : Return UiTheme.WarningAmber
                Case Else : Return UiTheme.PrimaryGreen
            End Select
        End Function

        Private Function LabStatusColor(status As String) As Color
            Select Case status
                Case "Pending" : Return UiTheme.WarningAmber
                Case "In Progress" : Return UiTheme.AccentGreen
                Case "Released" : Return UiTheme.PrimaryGreen
                Case Else : Return UiTheme.TextMuted
            End Select
        End Function

        Private Function BuildStatCard(title As String, value As String, accent As Color) As Panel
            Dim card As New Panel With {
                .Size = New Size(210, 100),
                .BackColor = Color.White,
                .Margin = New Padding(0, 0, 16, 16)
            }
            Dim accentBar As New Panel With {.Dock = DockStyle.Left, .Width = 5, .BackColor = accent}
            card.Controls.Add(accentBar)

            Dim lblValue As New Label With {
                .Text = value,
                .Font = New Font("Segoe UI", 20.0F, FontStyle.Bold),
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(20, 16)
            }
            card.Controls.Add(lblValue)

            Dim lblTitle As New Label With {
                .Text = title,
                .Font = New Font("Segoe UI", 8.75F),
                .ForeColor = UiTheme.TextMuted,
                .AutoSize = True,
                .MaximumSize = New Size(175, 0),
                .Location = New Point(20, 58)
            }
            card.Controls.Add(lblTitle)

            Return card
        End Function

    End Class

End Namespace
