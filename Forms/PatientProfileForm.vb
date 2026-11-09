Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Patient profile / medical history hub (Figures 11-20): read-only record summary in tabs, printable via PatientSummaryPrinter.</summary>
    Public Class PatientProfileForm
        Inherits Form

        Private ReadOnly patientId As Integer
        Private ReadOnly onOpenEncounter As Action(Of Integer)

        ''' <summary>
        ''' Roles without clinical/encounter access (e.g. Records Staff) still
        ''' see the full read-only record summary (visits, diagnoses, labs,
        ''' medicines, certificates, referrals) and can print it, but cannot
        ''' start a new encounter or drill into the clinical workspace.
        ''' </summary>
        Private ReadOnly canAccessEncounters As Boolean

        Private lblName, lblMeta, lblConditions As Label
        Private tabs As TabControl
        Private pnlInfo As TableLayoutPanel

        Private patientDisplayName As String = ""

        Public Sub New(patientId As Integer, onOpenEncounter As Action(Of Integer))
            Me.patientId = patientId
            Me.onOpenEncounter = onOpenEncounter
            Me.canAccessEncounters = Session.Current IsNot Nothing AndAlso Session.Current.HasModule("encounters")
            InitializeComponent()
            LoadPatient()
            LoadSections()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = New Font("Segoe UI", 9.0F)
            Me.Padding = New Padding(20, 16, 20, 16)

            ' Dock order: later-added Top controls sit higher, so the gap spacer goes first, then the header card.
            Me.Controls.Add(New Panel With {.Dock = DockStyle.Top, .Height = 14})

            ' ---- Header card: 6px green top bar, thin border, white body ----
            Dim card As New Panel With {.Dock = DockStyle.Top, .Height = 146, .BackColor = Color.White}
            SetDoubleBuffered(card)
            AddHandler card.Paint,
                Sub(s, e)
                    Using pen As New Pen(UiTheme.BorderLight)
                        e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
                    End Using
                    Using bar As New SolidBrush(ProfileGreen)
                        e.Graphics.FillRectangle(bar, 0, 0, card.Width, 6)
                    End Using
                End Sub
            Me.Controls.Add(card)

            lblName = New Label With {.Font = New Font("Segoe UI", 13.5F, FontStyle.Bold), .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(24, 24)}
            card.Controls.Add(lblName)
            lblMeta = New Label With {.Font = New Font("Segoe UI", 9.0F), .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(24, 58)}
            card.Controls.Add(lblMeta)
            lblConditions = New Label With {.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(24, 88), .MaximumSize = New Size(640, 0)}
            card.Controls.Add(lblConditions)

            ' Action buttons: equal width, stacked vertically on the right, outlined in green.
            Dim buttons As New List(Of Button)
            If canAccessEncounters Then
                Dim btnNewEncounter As New Button With {.Text = "+ New Encounter"}
                AddHandler btnNewEncounter.Click, AddressOf BtnNewEncounter_Click
                buttons.Add(btnNewEncounter)
            End If

            ' Print Summary and Edit Patient belong to the records roles (those holding "registration");
            ' Healthcare Staff only view the record and work on encounters.
            If Session.Current IsNot Nothing AndAlso Session.Current.HasModule("registration") Then
                Dim btnPrint As New Button With {.Text = "Print Summary"}
                AddHandler btnPrint.Click, Sub() PatientSummaryPrinter.ShowPreview(Me, patientId)
                buttons.Add(btnPrint)

                Dim btnEdit As New Button With {.Text = "Edit Patient"}
                AddHandler btnEdit.Click, AddressOf BtnEdit_Click
                buttons.Add(btnEdit)
            End If

            For Each b In buttons
                StyleActionButton(b)
            Next
            card.Controls.AddRange(buttons.ToArray())
            Dim placeButtons =
                Sub()
                    For i = 0 To buttons.Count - 1
                        buttons(i).Location = New Point(card.Width - 24 - buttons(i).Width, 20 + i * (buttons(i).Height + 6))
                    Next
                End Sub
            AddHandler card.Resize, Sub() placeButtons()
            placeButtons()

            ' ---- Tab bar (pill-style active tab) + content ----
            tabs = New PillTabControl With {.Dock = DockStyle.Fill}
            Me.Controls.Add(tabs)
            tabs.BringToFront()

            Dim tabInfo As New TabPage("Patient Information") With {.BackColor = Color.White, .AutoScroll = True}
            ' Two-column grid; each cell stacks a small uppercase caption over its value.
            pnlInfo = New TableLayoutPanel With {.Location = New Point(24, 24), .AutoSize = True, .ColumnCount = 2}
            pnlInfo.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 380))
            pnlInfo.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 380))
            tabInfo.Controls.Add(pnlInfo)
            tabs.TabPages.Add(tabInfo)
        End Sub

        Private Shared ReadOnly ProfileGreen As Color = ColorTranslator.FromHtml("#1F5F3F")
        Private Shared ReadOnly ProfileGreenSoft As Color = ColorTranslator.FromHtml("#E8F3EC")

        Private Shared Sub SetDoubleBuffered(c As Control)
            GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(c, True)
        End Sub

        Private Shared Sub StyleActionButton(btn As Button)
            btn.Size = New Size(132, 30)
            btn.FlatStyle = FlatStyle.Flat
            btn.BackColor = Color.White
            btn.ForeColor = ProfileGreen
            btn.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
            btn.TextAlign = ContentAlignment.MiddleLeft
            btn.Padding = New Padding(8, 0, 0, 0)
            btn.Cursor = Cursors.Hand
            btn.UseVisualStyleBackColor = False
            btn.FlatAppearance.BorderColor = ProfileGreen
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.MouseOverBackColor = ProfileGreenSoft
            btn.FlatAppearance.MouseDownBackColor = ProfileGreenSoft
        End Sub

        Private Sub LoadPatient()
            Try
                Dim r = PatientSummaryData.LoadPatient(patientId)
                If r Is Nothing Then
                    lblName.Text = "Patient not found"
                    Return
                End If
                patientDisplayName = $"{r("last_name")}, {r("first_name")} {r("middle_name")}".Trim()
                lblName.Text = $"{patientDisplayName}   ({r("patient_no")})"
                lblMeta.Text = $"{r("age")} years old - {r("sex")}   |   {If(IsDBNull(r("contact_number")), "no contact", r("contact_number"))}   |   {r("barangay_name")}, Vinzons" &
                               If(IsDBNull(r("household_no")), "", $"   |   Household {r("household_no")}")

                Dim flags As New List(Of String)
                If Not IsDBNull(r("existing_conditions")) AndAlso r("existing_conditions").ToString().Trim().Length > 0 Then flags.Add(r("existing_conditions").ToString())
                If Not IsDBNull(r("known_allergies")) AndAlso r("known_allergies").ToString().Trim().Length > 0 Then flags.Add("Allergy: " & r("known_allergies").ToString())
                lblConditions.Text = String.Join("   |   ", flags)

                pnlInfo.SuspendLayout()
                UiTheme.ClearAndDispose(pnlInfo)
                For Each f In PatientSummaryData.PatientInfoFields(r)
                    Dim cell As New FlowLayoutPanel With {.FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoSize = True, .Margin = New Padding(0, 0, 16, 14)}
                    cell.Controls.Add(New Label With {.Text = f.Label.ToUpperInvariant(), .Font = New Font("Segoe UI", 7.5F), .ForeColor = ColorTranslator.FromHtml("#9CA3AF"), .AutoSize = True, .Margin = New Padding(0, 0, 0, 2)})
                    cell.Controls.Add(New Label With {.Text = f.Value, .Font = New Font("Segoe UI Semibold", 9.75F), .ForeColor = UiTheme.TextDark, .AutoSize = True, .MaximumSize = New Size(360, 0), .Margin = New Padding(0)})
                    pnlInfo.Controls.Add(cell)
                Next
                pnlInfo.ResumeLayout()
            Catch ex As Exception
                lblName.Text = "Error loading patient: " & ex.Message
            End Try
        End Sub

        ''' <summary>One read-only tab per history section (PatientSummaryData); tab titles carry the record count.</summary>
        Private Sub LoadSections()
            Try
                For Each sec In PatientSummaryData.LoadSections(patientId)
                    Dim page As New TabPage($"{sec.Title} ({sec.Table.Rows.Count})")
                    Dim grid As New DataGridView With {.Dock = DockStyle.Fill}
                    UiTheme.StyleGrid(grid)
                    page.Controls.Add(grid)
                    tabs.TabPages.Add(page)
                    grid.DataSource = sec.Table
                    If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                    ' Only clinical roles can drill from a visit into the encounter workspace.
                    If canAccessEncounters AndAlso sec.Table.Columns.Contains("Id") Then
                        AddHandler grid.CellDoubleClick,
                            Sub(s, e)
                                If e.RowIndex < 0 Then Return
                                onOpenEncounter?.Invoke(Convert.ToInt32(grid.Rows(e.RowIndex).Cells("Id").Value))
                            End Sub
                    End If
                Next
            Catch ex As Exception
                UiTheme.ShowError(Me, "load patient history", ex)
            End Try
        End Sub

        Private Sub BtnNewEncounter_Click(sender As Object, e As EventArgs)
            Using dlg As New NewEncounterDialog(patientId, patientDisplayName)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.NewEncounterId.HasValue Then
                    onOpenEncounter?.Invoke(dlg.NewEncounterId.Value)
                End If
            End Using
        End Sub

        Private Sub BtnEdit_Click(sender As Object, e As EventArgs)
            Using dlg As New PatientRegistrationForm(patientId, Sub(id) LoadPatient())
                dlg.StartPosition = FormStartPosition.CenterParent
                dlg.Width = 1000 : dlg.Height = 700
                dlg.ShowDialog(Me)
            End Using
        End Sub

    End Class

End Namespace
