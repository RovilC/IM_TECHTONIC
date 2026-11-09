Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>
    ''' Four-step patient registration wizard (Figures 6-10): Personal Info,
    ''' Contact &amp; Address, Health Info, Review. Supports create and edit
    ''' (pass an existing patientId to edit that record).
    ''' </summary>
    Public Class PatientRegistrationForm
        Inherits Form
        Implements IConfirmLeave

        Private changes As ChangeTracker
        Private closeConfirmed As Boolean = False

        Private ReadOnly editingPatientId As Integer?
        Private ReadOnly onSaved As Action(Of Integer)

        Private steps As Panel()
        Private currentStep As Integer = 0
        Private lblStepIndicator As Label
        Private btnBack, btnNext, btnCancel, btnSave As Button
        Private pnlButtons As Panel
        Private pnlBody As Panel
        Private lblFormError As Label
        Private pnlStepper As Panel
        Private Shared ReadOnly StepNames As String() = {"Personal Information", "Contact & Address", "Health Information", "Review"}

        ' Step 1
        Private txtPhilhealthNo, txtFirstName, txtMiddleName, txtLastName, txtSuffix, txtOccupation As TextBox
        Private dtpDob As DateTimePicker
        Private lblAge As Label
        Private cboSex, cboCivilStatus As ComboBox

        ' Step 2
        Private txtContact, txtAltContact, txtAddress As TextBox
        Private cboBarangay As ComboBox
        Private txtEmergName, txtEmergContact As TextBox
        Private cboEmergRelationship As ComboBox

        ' Step 2 -- household membership
        Private rbHouseholdYes, rbHouseholdNo As RadioButton
        Private pnlHouseholdExisting As Panel
        Private cboHousehold As ComboBox
        Private lblHouseholdInfo As Label
        Private chkHeadExisting, chkHeadNew As CheckBox
        Private selectedHousehold As HouseholdItem = Nothing
        Private loadedFamilyId As Integer? = Nothing
        Private loadedIsHead As Boolean = False

        ' Step 3
        Private cboBloodType, cboMembership, cboPhilhealthStatus, cboPhilhealthCategory As ComboBox
        Private txtAllergies, txtConditions As TextBox

        ' Step 4
        Private pnlReview As TableLayoutPanel

        Public Sub New(patientId As Integer?, onSaved As Action(Of Integer))
            Me.editingPatientId = patientId
            Me.onSaved = onSaved
            InitializeComponent()
            LoadLookups()
            If editingPatientId.HasValue Then LoadExisting(editingPatientId.Value)
            ShowStep(0)
            changes = UiTheme.TrackChanges(Me)
            ' Edit mode runs as a dialog: closing it with the X also asks before discarding edits.
            AddHandler Me.FormClosing,
                Sub(s, e)
                    If Me.Modal AndAlso Not closeConfirmed AndAlso Me.DialogResult <> DialogResult.OK AndAlso Not ConfirmLeave() Then e.Cancel = True
                End Sub
        End Sub

        ''' <summary>Asked before leaving the screen (Back / sidebar / log out / closing the edit dialog) while it holds unsaved input.</summary>
        Public Function ConfirmLeave() As Boolean Implements IConfirmLeave.ConfirmLeave
            If changes Is Nothing OrElse Not changes.IsDirty Then Return True
            Return UiTheme.ConfirmDiscard(Me, If(editingPatientId.HasValue, "your changes to this patient record", "this unsaved patient registration"))
        End Function

        Private Sub BtnCancel_Click()
            If Not ConfirmLeave() Then Return
            If editingPatientId.HasValue Then
                closeConfirmed = True
                Me.DialogResult = DialogResult.Cancel
                Me.Close()
            Else
                ResetWizard()
            End If
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = UiTheme.BaseFont
            Me.Padding = New Padding(20, 16, 20, 16)

            ' ---- Title + subtitle ----
            Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 62}
            header.Controls.Add(New Label With {
                .Text = If(editingPatientId.HasValue, "Edit Patient Record", "Patient Registration"),
                .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold), .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(0, 0)
            })
            lblStepIndicator = New Label With {
                .Text = If(editingPatientId.HasValue, "Update this patient's details in the RHU Health Records System", "Register a new patient in the RHU Health Records System"),
                .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(2, 36)
            }
            header.Controls.Add(lblStepIndicator)

            ' ---- Stepper card: current step highlighted, finished steps ticked, upcoming steps greyed ----
            Dim stepperWrap As New Panel With {.Dock = DockStyle.Top, .Height = 80, .Padding = New Padding(0, 0, 0, 14)}
            pnlStepper = New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
            SetDoubleBuffered(pnlStepper)
            AddHandler pnlStepper.Paint, AddressOf PaintStepper
            AddHandler pnlStepper.Paint, Sub(s, e) DrawCardBorder(e.Graphics, pnlStepper)
            ' Finished steps can be clicked to jump back to them.
            AddHandler pnlStepper.MouseClick,
                Sub(s, e)
                    Dim i = (e.X - StepperLeft) \ StepItemWidth
                    If e.X >= StepperLeft AndAlso i >= 0 AndAlso i < currentStep Then ShowStep(i)
                End Sub
            AddHandler pnlStepper.MouseMove,
                Sub(s, e)
                    Dim i = (e.X - StepperLeft) \ StepItemWidth
                    pnlStepper.Cursor = If(e.X >= StepperLeft AndAlso i >= 0 AndAlso i < currentStep, Cursors.Hand, Cursors.Default)
                End Sub
            stepperWrap.Controls.Add(pnlStepper)

            ' ---- Form card holding the step body and the button row ----
            Dim card As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(28, 24, 28, 8)}
            SetDoubleBuffered(card)
            AddHandler card.Paint, Sub(s, e) DrawCardBorder(e.Graphics, card)

            pnlButtons = New Panel With {.Dock = DockStyle.Bottom, .Height = 64}
            AddHandler pnlButtons.Paint,
                Sub(s, e)
                    Using pen As New Pen(UiTheme.BorderLight)
                        e.Graphics.DrawLine(pen, 0, 0, pnlButtons.Width, 0)
                    End Using
                End Sub
            card.Controls.Add(pnlButtons)

            btnCancel = New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(0, 16)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() BtnCancel_Click()
            pnlButtons.Controls.Add(btnCancel)

            btnBack = New Button With {.Text = "<- Back", .Size = New Size(100, 36), .Location = New Point(110, 16)}
            UiTheme.StyleSecondaryButton(btnBack)
            AddHandler btnBack.Click, Sub() ShowStep(currentStep - 1)
            pnlButtons.Controls.Add(btnBack)

            btnNext = New Button With {.Text = "Next ->", .Size = New Size(110, 36)}
            UiTheme.StylePrimaryButton(btnNext)
            UiTheme.PinToRight(btnNext, pnlButtons, 0, 16)
            AddHandler btnNext.Click, Sub() TryAdvance()
            pnlButtons.Controls.Add(btnNext)

            btnSave = New Button With {.Text = "Save Patient", .Size = New Size(140, 36)}
            UiTheme.StylePrimaryButton(btnSave)
            UiTheme.PinToRight(btnSave, pnlButtons, 0, 16)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            pnlButtons.Controls.Add(btnSave)

            ' Validation messages: the panel must be tall enough to actually show the label
            ' (an earlier version placed the label below the panel bounds, hiding every error).
            lblFormError = New Label With {.Text = "", .ForeColor = UiTheme.DangerRed, .AutoSize = True, .MaximumSize = New Size(700, 0), .Location = New Point(0, 0)}
            Dim errPanel As New Panel With {.Dock = DockStyle.Top, .Height = 30}
            errPanel.Controls.Add(lblFormError)

            pnlBody = New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = Color.White}
            card.Controls.Add(pnlBody)
            pnlBody.BringToFront()

            ' Fill control first, then Top panels in reverse visual order (last added sits at the very top).
            Me.Controls.Add(card)
            Me.Controls.Add(errPanel)
            Me.Controls.Add(stepperWrap)
            Me.Controls.Add(header)

            steps = New Panel() {BuildStep1(), BuildStep2(), BuildStep3(), BuildStep4()}
            For Each s In steps
                s.Visible = False
                s.Dock = DockStyle.Fill
                s.BackColor = Color.White
                pnlBody.Controls.Add(s)
            Next

            ' PhilHealth Status/Category only mean anything once a PhilHealth No. is on file --
            ' wired here (not in BuildStep1/BuildStep3) since both controls must already exist.
            AddHandler txtPhilhealthNo.TextChanged, Sub() UpdatePhilhealthFieldsState()
            UpdatePhilhealthFieldsState()
        End Sub

        Private Const StepperLeft As Integer = 20
        Private Const StepItemWidth As Integer = 190

        Private Shared Sub SetDoubleBuffered(c As Control)
            GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(c, True)
        End Sub

        Private Shared Sub DrawCardBorder(g As Graphics, c As Control)
            Using pen As New Pen(UiTheme.BorderLight)
                g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1)
            End Using
        End Sub

        Private Sub PaintStepper(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Const d As Integer = 28
            Dim cy = pnlStepper.Height \ 2
            Using numFont As New Font("Segoe UI", 9.0F, FontStyle.Bold),
                  activeFont As New Font("Segoe UI", 10.0F, FontStyle.Bold),
                  idleFont As New Font("Segoe UI", 10.0F),
                  greyFill As New SolidBrush(Color.FromArgb(235, 239, 236)),
                  greenFill As New SolidBrush(UiTheme.PrimaryGreen),
                  darkBrush As New SolidBrush(UiTheme.TextDark),
                  mutedBrush As New SolidBrush(UiTheme.TextMuted)
                For i = 0 To StepNames.Length - 1
                    Dim x = StepperLeft + i * StepItemWidth
                    Dim circle As New Rectangle(x, cy - d \ 2, d, d)
                    Dim isCurrent = (i = currentStep)
                    Dim isDone = (i < currentStep)
                    g.FillEllipse(If(isCurrent OrElse isDone, greenFill, greyFill), circle)
                    Dim glyph = If(isDone, ChrW(&H2713).ToString(), (i + 1).ToString())
                    Using fmt As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                        g.DrawString(glyph, numFont, If(isCurrent OrElse isDone, Brushes.White, Brushes.DimGray), circle, fmt)
                    End Using
                    Dim labelRect As New Rectangle(x + d + 10, cy - 12, StepItemWidth - d - 14, 24)
                    Using fmt As New StringFormat With {.LineAlignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter, .FormatFlags = StringFormatFlags.NoWrap}
                        g.DrawString(StepNames(i), If(isCurrent, activeFont, idleFont), If(isCurrent, greenFill, If(isDone, darkBrush, mutedBrush)), labelRect, fmt)
                    End Using
                Next
            End Using
        End Sub

        ''' <summary>Enter behaves like Tab (moves to the next field) instead of doing nothing/beeping, except inside a multiline box (where Enter must still insert a newline) or while a dropdown is open (where Enter should pick the highlighted item, not skip past it).</summary>
        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = Keys.Enter Then
                Dim ctrl = Me.ActiveControl
                Dim combo = TryCast(ctrl, ComboBox)
                If combo IsNot Nothing AndAlso combo.DroppedDown Then Return MyBase.ProcessCmdKey(msg, keyData)
                Dim box = TryCast(ctrl, TextBox)
                If box IsNot Nothing AndAlso box.Multiline Then Return MyBase.ProcessCmdKey(msg, keyData)
                Me.ProcessTabKey(True)
                Return True
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        ''' <summary>PhilHealth Status/Category are meaningless without a PhilHealth No. on file -- disabled (and cleared) rather than left editable with nothing to attach them to.</summary>
        Private Sub UpdatePhilhealthFieldsState()
            Dim hasNo = Not String.IsNullOrWhiteSpace(txtPhilhealthNo.Text)
            cboPhilhealthStatus.Enabled = hasNo
            cboPhilhealthCategory.Enabled = hasNo
            If Not hasNo Then
                cboPhilhealthStatus.SelectedIndex = -1
                cboPhilhealthCategory.SelectedIndex = -1
            End If
        End Sub

        ' ---------------- Step builders ----------------

        Private Function LabeledField(container As Panel, label As String, x As Integer, y As Integer, ctrl As Control, Optional width As Integer = 260) As Control
            Dim required = label.EndsWith("*")
            Dim text = label.TrimEnd("*"c).TrimEnd()
            Dim lblFont As New Font("Segoe UI", 8.75F, FontStyle.Bold)
            container.Controls.Add(New Label With {.Text = text, .Font = lblFont, .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(x, y)})
            If required Then
                Dim tw = TextRenderer.MeasureText(text, lblFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                container.Controls.Add(New Label With {.Text = "*", .Font = lblFont, .ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(x + tw + 1, y)})
            End If
            Dim tb = TryCast(ctrl, TextBox)
            If tb IsNot Nothing AndAlso Not tb.Multiline Then tb.BorderStyle = BorderStyle.FixedSingle
            ctrl.Font = New Font("Segoe UI", 10.0F)
            ctrl.Location = New Point(x, y + 20)
            ctrl.Width = width
            container.Controls.Add(ctrl)
            Return ctrl
        End Function

        Private Function BuildStep1() As Panel
            Dim p As New Panel()
            p.Controls.Add(New Label With {.Text = "Personal Information", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            ' Grouped into labelled sections, each with a rule above it; same 3-column grid (x = 0 / 280 / 560)
            ' as the other steps, with the name group using four columns so all name parts sit on one row.
            AddSectionHeading(p, "PhilHealth", 40)
            txtPhilhealthNo = New TextBox() : LabeledField(p, "PhilHealth No.", 0, 80, txtPhilhealthNo)
            p.Controls.Add(New Label With {.Text = "Leave blank if the patient has no PhilHealth number.", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 106)})

            AddSectionHeading(p, "Name", 150)
            txtFirstName = New TextBox() : LabeledField(p, "First Name *", 0, 190, txtFirstName, 195)
            txtMiddleName = New TextBox() : LabeledField(p, "Middle Name", 205, 190, txtMiddleName, 195)
            txtLastName = New TextBox() : LabeledField(p, "Last Name *", 410, 190, txtLastName, 195)
            txtSuffix = New TextBox() : LabeledField(p, "Suffix", 615, 190, txtSuffix, 205)

            AddSectionHeading(p, "Birthday && Sex", 260)
            dtpDob = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .MaxDate = DateTime.Today}
            LabeledField(p, "Date of Birth *", 0, 300, dtpDob)
            AddHandler dtpDob.ValueChanged, Sub() UpdateAgeLabel()
            p.Controls.Add(New Label With {.Text = "Age (auto-computed)", .Font = New Font("Segoe UI", 8.75F, FontStyle.Bold), .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(280, 300)})
            lblAge = New Label With {.Text = "", .Font = New Font("Segoe UI", 10.0F), .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 324)}
            p.Controls.Add(lblAge)
            cboSex = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboSex.Items.AddRange({"Male", "Female"})
            LabeledField(p, "Sex *", 560, 300, cboSex)

            AddSectionHeading(p, "Civil Status && Occupation", 370)
            cboCivilStatus = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboCivilStatus.Items.AddRange({"Single", "Married", "Widowed", "Separated", "Divorced"})
            LabeledField(p, "Civil Status", 0, 410, cboCivilStatus)
            txtOccupation = New TextBox() : LabeledField(p, "Occupation", 280, 410, txtOccupation, 540)

            UpdateAgeLabel()
            Return p
        End Function

        Private Sub UpdateAgeLabel()
            If dtpDob Is Nothing Then Return
            Dim age = DateTime.Today.Year - dtpDob.Value.Year
            If dtpDob.Value.Date > DateTime.Today.AddYears(-age) Then age -= 1
            lblAge.Text = $"{Math.Max(age, 0)} yrs old"
        End Sub

        Private Function BuildStep2() As Panel
            Dim p As New Panel()
            ' "&&" -- a single "&" in a Label is swallowed as a keyboard-mnemonic marker.
            p.Controls.Add(New Label With {.Text = "Contact && Address", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            ' Three-column grid (x = 0 / 280 / 560) and a steady 70px row pitch so the fields breathe evenly.
            txtContact = New TextBox() : LabeledField(p, "Contact Number *", 0, 40, txtContact)
            txtAltContact = New TextBox() : LabeledField(p, "Alternate Contact", 280, 40, txtAltContact)

            txtAddress = New TextBox() : LabeledField(p, "House No. / Street / Purok", 0, 110, txtAddress, 540)

            cboBarangay = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            LabeledField(p, "Barangay *", 0, 180, cboBarangay)
            p.Controls.Add(New Label With {.Text = "Municipality: Vinzons   |   Province: Camarines Norte", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 206)})

            AddSectionHeading(p, "Emergency Contact (optional)", 270)
            txtEmergName = New TextBox() : LabeledField(p, "Contact Name", 0, 310, txtEmergName)
            cboEmergRelationship = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboEmergRelationship.Items.AddRange({"Parent", "Spouse", "Sibling", "Child", "Relative", "Guardian", "Other"})
            LabeledField(p, "Relationship", 280, 310, cboEmergRelationship)
            txtEmergContact = New TextBox() : LabeledField(p, "Contact No.", 560, 310, txtEmergContact)

            BuildHouseholdSection(p, 400)
            p.AutoScroll = True
            Return p
        End Function

        ''' <summary>Section heading with a thin rule above it, used to separate groups inside a step.</summary>
        Private Sub AddSectionHeading(p As Panel, text As String, y As Integer)
            p.Controls.Add(New Panel With {.Location = New Point(0, y), .Size = New Size(820, 1), .BackColor = UiTheme.BorderLight})
            p.Controls.Add(New Label With {.Text = text, .Font = UiTheme.BoldFont, .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(0, y + 12)})
        End Sub

        ''' <summary>
        ''' "Is the patient part of a household?" -- the search field is always on screen but locked
        ''' (greyed) until an answer is picked. Yes: search an existing household (suggestions as you
        ''' type, pre-filled with households headed by someone sharing the patient's last name) and
        ''' optionally make the patient its head. No: the locked field is auto-filled with the new
        ''' household registered at the patient's address, with the patient as head unless unticked.
        ''' </summary>
        Private Sub BuildHouseholdSection(p As Panel, y As Integer)
            AddSectionHeading(p, "Household", y)
            p.Controls.Add(New Label With {.Text = "Is the patient part of an existing household? *", .AutoSize = True, .Location = New Point(0, y + 46)})

            Dim radios As New Panel With {.Location = New Point(330, y + 40), .Size = New Size(200, 30)}
            rbHouseholdYes = New RadioButton With {.Text = "Yes", .AutoSize = True, .Location = New Point(0, 4)}
            rbHouseholdNo = New RadioButton With {.Text = "No", .AutoSize = True, .Location = New Point(80, 4)}
            radios.Controls.AddRange({rbHouseholdYes, rbHouseholdNo})
            p.Controls.Add(radios)
            AddHandler rbHouseholdYes.CheckedChanged, Sub() UpdateHouseholdPanels()
            AddHandler rbHouseholdNo.CheckedChanged, Sub() UpdateHouseholdPanels()

            pnlHouseholdExisting = New Panel With {.Location = New Point(0, y + 86), .Size = New Size(840, 120)}
            pnlHouseholdExisting.Controls.Add(New Label With {.Text = "Search Household (household no., head's name, address or barangay) *", .Font = New Font("Segoe UI", 8.75F, FontStyle.Bold), .AutoSize = True, .Location = New Point(0, 0)})
            cboHousehold = New ComboBox With {.Location = New Point(0, 22), .Width = 820, .Font = New Font("Segoe UI", 10.0F)}
            UiTheme.AttachSearchableCombo(cboHousehold, 2, Function(text) SearchHouseholds(text))
            AddHandler cboHousehold.SelectedIndexChanged,
                Sub()
                    selectedHousehold = TryCast(cboHousehold.SelectedItem, HouseholdItem)
                    UpdateHouseholdInfo()
                End Sub
            ' Typing over a picked household invalidates the pick (the search refill keeps the typed text, not the item).
            AddHandler cboHousehold.TextChanged,
                Sub()
                    If selectedHousehold IsNot Nothing AndAlso cboHousehold.Text <> selectedHousehold.Label Then
                        selectedHousehold = Nothing
                        UpdateHouseholdInfo()
                    End If
                End Sub
            pnlHouseholdExisting.Controls.Add(cboHousehold)
            lblHouseholdInfo = New Label With {.AutoSize = True, .ForeColor = UiTheme.TextMuted, .Location = New Point(0, 62), .MaximumSize = New Size(820, 0)}
            pnlHouseholdExisting.Controls.Add(lblHouseholdInfo)
            chkHeadExisting = New CheckBox With {.Text = "Patient is the head of this household", .AutoSize = True, .Location = New Point(0, 92)}
            pnlHouseholdExisting.Controls.Add(chkHeadExisting)
            chkHeadNew = New CheckBox With {.Text = "Patient is the head of the new household", .AutoSize = True, .Checked = True, .Location = New Point(0, 92), .Visible = False}
            pnlHouseholdExisting.Controls.Add(chkHeadNew)
            p.Controls.Add(pnlHouseholdExisting)

            ' Keep the auto-filled new household current when the address/barangay change.
            AddHandler txtAddress.TextChanged, Sub() If rbHouseholdNo.Checked Then FillNewHouseholdPreview()
            AddHandler cboBarangay.SelectedIndexChanged, Sub() If rbHouseholdNo.Checked Then FillNewHouseholdPreview()
            AddHandler chkHeadNew.CheckedChanged, Sub() If rbHouseholdNo.Checked Then FillNewHouseholdPreview()

            UpdateHouseholdPanels() ' initial state: nothing answered -> everything locked and grey
        End Sub

        Private Sub UpdateHouseholdPanels()
            Dim isNew = rbHouseholdNo.Checked
            Dim isYes = rbHouseholdYes.Checked

            cboHousehold.Enabled = isYes
            cboHousehold.BackColor = If(isYes, Color.White, Color.FromArgb(236, 239, 237))
            chkHeadExisting.Visible = Not isNew
            chkHeadExisting.Enabled = isYes
            chkHeadNew.Visible = isNew
            If isNew Then
                selectedHousehold = Nothing
                cboHousehold.Items.Clear()
                FillNewHouseholdPreview()
            ElseIf isYes Then
                If cboHousehold.Text.StartsWith(NewHouseholdMarker) Then cboHousehold.Text = ""
                If selectedHousehold Is Nothing AndAlso cboHousehold.Text.Trim().Length = 0 Then LoadSuggestedHouseholds()
            Else
                selectedHousehold = Nothing
                cboHousehold.Items.Clear()
                cboHousehold.Text = ""
                chkHeadExisting.Checked = False
            End If
            UpdateHouseholdInfo()
            If Not isNew AndAlso Not isYes Then lblHouseholdInfo.Text = "Answer the question above to enable this section."
        End Sub

        Private Const NewHouseholdMarker As String = "(New) "

        ''' <summary>Shows, in the locked search field, the household that "No" will register: next household no., head and address.</summary>
        Private Sub FillNewHouseholdPreview()
            Dim householdNo As String
            Try
                householdNo = IdGenerator.NextHouseholdNo()
            Catch
                householdNo = "HH-(new)"
            End Try
            Dim patientName = $"{txtLastName.Text.Trim()}, {txtFirstName.Text.Trim()}".Trim(","c, " "c)
            Dim head = If(chkHeadNew.Checked AndAlso patientName.Length > 0, patientName, "(none)")
            Dim barangay = If(cboBarangay.SelectedItem IsNot Nothing, cboBarangay.SelectedItem.ToString(), "")
            Dim place = String.Join(", ", {txtAddress.Text.Trim(), barangay}.Where(Function(s) s.Length > 0))
            cboHousehold.Text = $"{NewHouseholdMarker}{householdNo}  -  Head: {head}  -  {If(place.Length > 0, place, "(address not set)")}"
        End Sub

        Private Sub UpdateHouseholdInfo()
            If rbHouseholdNo.Checked Then
                lblHouseholdInfo.Text = "A new household will be registered using the patient's address and barangay above."
            ElseIf selectedHousehold Is Nothing Then
                lblHouseholdInfo.Text = "Type at least 2 characters, then pick the household from the suggestions."
            Else
                lblHouseholdInfo.Text = $"Head: {If(selectedHousehold.HeadName, "(no head set)")}   |   {selectedHousehold.Members} registered member(s)" &
                    If(selectedHousehold.HeadName IsNot Nothing AndAlso selectedHousehold.HeadId <> If(editingPatientId, -1), "   --   ticking below replaces the current head", "")
            End If
        End Sub

        Private Const HouseholdSearchSql As String =
            "SELECT f.family_id, f.household_no, f.address, b.barangay_name, f.family_head_id,
                    CONCAT(h.last_name, ', ', h.first_name) AS head_name,
                    (SELECT COUNT(*) FROM patients m WHERE m.family_id = f.family_id) AS members
             FROM families_households f
             JOIN barangays b ON b.barangay_id = f.barangay_id
             LEFT JOIN patients h ON h.patient_id = f.family_head_id "

        Private Function SearchHouseholds(text As String) As List(Of Object)
            Dim results As New List(Of Object)
            If rbHouseholdNo.Checked Then Return results ' the field shows the auto-filled new household, not a search
            Try
                For Each row As DataRow In Db.Query(
                    HouseholdSearchSql &
                    "WHERE f.household_no LIKE @Like OR f.address LIKE @Like OR b.barangay_name LIKE @Like
                        OR h.last_name LIKE @Like OR h.first_name LIKE @Like OR CONCAT(h.last_name, ', ', h.first_name) LIKE @Like
                     ORDER BY f.household_no LIMIT 20", New With {.Like = "%" & text.Trim() & "%"}).Rows
                    results.Add(HouseholdItem.FromRow(row))
                Next
            Catch
                ' leave results empty -- the combo just won't show anything this keystroke
            End Try
            Return results
        End Function

        ''' <summary>Suggests households whose head shares the patient's last name (same barangay first) as soon as "Yes" is picked.</summary>
        Private Sub LoadSuggestedHouseholds()
            If String.IsNullOrWhiteSpace(txtLastName.Text) Then Return
            Try
                Dim barangayId = If(cboBarangay.SelectedItem IsNot Nothing, CType(cboBarangay.SelectedItem, BarangayItem).Id, 0)
                Dim table = Db.Query(
                    HouseholdSearchSql &
                    "WHERE h.last_name = @LastName
                     ORDER BY (f.barangay_id = @BarangayId) DESC, f.household_no LIMIT 20",
                    New With {.LastName = txtLastName.Text.Trim(), .BarangayId = barangayId})
                If table.Rows.Count = 0 Then Return
                cboHousehold.Items.Clear()
                For Each row As DataRow In table.Rows
                    cboHousehold.Items.Add(HouseholdItem.FromRow(row))
                Next
                lblHouseholdInfo.Text = $"Suggested: {table.Rows.Count} household(s) headed by a ""{txtLastName.Text.Trim()}"" -- open the list or type to search."
                cboHousehold.Focus()
                cboHousehold.DroppedDown = True
            Catch
                ' suggestions are a convenience only
            End Try
        End Sub

        Private Class HouseholdItem
            Public Id As Integer
            Public Label As String
            Public HeadId As Integer
            Public HeadName As String
            Public Members As Integer
            Public Shared Function FromRow(row As DataRow) As HouseholdItem
                Dim head = If(IsDBNull(row("head_name")), Nothing, row("head_name").ToString())
                Dim place = If(IsDBNull(row("address")) OrElse row("address").ToString().Trim() = "", "", row("address").ToString().Trim() & ", ") & row("barangay_name").ToString()
                Return New HouseholdItem With {
                    .Id = Convert.ToInt32(row("family_id")),
                    .HeadId = If(IsDBNull(row("family_head_id")), -1, Convert.ToInt32(row("family_head_id"))),
                    .HeadName = head,
                    .Members = Convert.ToInt32(row("members")),
                    .Label = $"{row("household_no")}  -  Head: {If(head, "(none)")}  -  {place}"
                }
            End Function
            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

        Private Function BuildStep3() As Panel
            Dim p As New Panel()
            p.Controls.Add(New Label With {.Text = "Health Information", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            cboBloodType = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboBloodType.Items.AddRange({"Unknown", "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"})
            LabeledField(p, "Blood Type", 0, 40, cboBloodType, 150)

            cboMembership = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboMembership.Items.AddRange({"Not a Member", "4Ps Member", "Indigent"})
            LabeledField(p, "4Ps / Indigent", 280, 40, cboMembership)

            txtAllergies = New TextBox With {.Multiline = True, .Height = 60}
            LabeledField(p, "Known Allergies", 0, 96, txtAllergies, 540)

            txtConditions = New TextBox With {.Multiline = True, .Height = 60}
            LabeledField(p, "Existing Medical Conditions", 0, 176, txtConditions, 540)

            cboPhilhealthStatus = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboPhilhealthStatus.Items.AddRange({"Member", "Dependent"})
            LabeledField(p, "PhilHealth Status", 0, 256, cboPhilhealthStatus)

            cboPhilhealthCategory = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboPhilhealthCategory.Items.AddRange({"Formal Economy", "Informal Economy", "Indigent", "Senior Citizen", "Sponsored"})
            LabeledField(p, "PhilHealth Category", 280, 256, cboPhilhealthCategory)

            Return p
        End Function

        Private Function BuildStep4() As Panel
            Dim p As New Panel()
            p.Controls.Add(New Label With {.Text = "Review Registration Details", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            ' Tinted summary panel: small muted caption above a bold value, two columns.
            pnlReview = New TableLayoutPanel With {
                .ColumnCount = 2, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .BackColor = Color.FromArgb(241, 246, 242), .Padding = New Padding(22, 20, 22, 4), .Margin = New Padding(0)
            }
            pnlReview.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 400))
            pnlReview.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 400))
            AddHandler pnlReview.Paint,
                Sub(s, e)
                    Using pen As New Pen(Color.FromArgb(221, 232, 224))
                        e.Graphics.DrawRectangle(pen, 0, 0, pnlReview.Width - 1, pnlReview.Height - 1)
                    End Using
                End Sub

            Dim note As New Label With {
                .Text = "Review all information before saving. Go back to correct any errors.",
                .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Margin = New Padding(0, 14, 0, 0)
            }
            Dim stack As New FlowLayoutPanel With {
                .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoSize = True,
                .Location = New Point(0, 40), .Margin = New Padding(0)
            }
            stack.Controls.Add(pnlReview)
            stack.Controls.Add(note)
            p.Controls.Add(stack)
            p.AutoScroll = True
            Return p
        End Function

        Private Sub LoadLookups()
            Try
                cboBarangay.Items.Clear()
                Dim table = Db.Query("SELECT barangay_id, barangay_name FROM barangays ORDER BY barangay_name")
                For Each row As DataRow In table.Rows
                    cboBarangay.Items.Add(New BarangayItem(Convert.ToInt32(row("barangay_id")), row("barangay_name").ToString()))
                Next
                If cboBarangay.Items.Count > 0 Then cboBarangay.SelectedIndex = 0
            Catch ex As Exception
                lblFormError.Text = "Could not load barangays: " & ex.Message
            End Try
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

        Private Sub LoadExisting(patientId As Integer)
            Dim table = Db.Query("SELECT * FROM patients WHERE patient_id = @Id", New With {.Id = patientId})
            If table.Rows.Count = 0 Then Return
            Dim r = table.Rows(0)

            txtPhilhealthNo.Text = r("philhealth_no").ToString()
            txtFirstName.Text = r("first_name").ToString()
            txtMiddleName.Text = r("middle_name").ToString()
            txtLastName.Text = r("last_name").ToString()
            txtSuffix.Text = r("suffix").ToString()
            If Not IsDBNull(r("date_of_birth")) Then dtpDob.Value = Convert.ToDateTime(r("date_of_birth"))
            cboSex.SelectedItem = r("sex").ToString()
            cboCivilStatus.SelectedItem = r("civil_status").ToString()
            txtOccupation.Text = r("occupation").ToString()

            txtContact.Text = r("contact_number").ToString()
            txtAltContact.Text = r("alternate_contact").ToString()
            txtAddress.Text = r("address").ToString()
            For Each item As BarangayItem In cboBarangay.Items
                If item.Id = Convert.ToInt32(r("barangay_id")) Then cboBarangay.SelectedItem = item
            Next
            txtEmergName.Text = r("emergency_contact_name").ToString()
            cboEmergRelationship.SelectedItem = r("emergency_relationship").ToString()
            txtEmergContact.Text = r("emergency_contact_no").ToString()

            cboBloodType.SelectedItem = r("blood_type").ToString()
            cboMembership.SelectedIndex = If(Convert.ToBoolean(r("is_4ps_member")), 1, If(Convert.ToBoolean(r("is_indigent")), 2, 0))
            txtAllergies.Text = r("known_allergies").ToString()
            txtConditions.Text = r("existing_conditions").ToString()
            cboPhilhealthStatus.SelectedItem = r("philhealth_status").ToString()
            cboPhilhealthCategory.SelectedItem = r("philhealth_category").ToString()

            If Not IsDBNull(r("family_id")) Then
                loadedFamilyId = Convert.ToInt32(r("family_id"))
                loadedIsHead = Convert.ToBoolean(r("family_head_or_no"))
                Dim hh = Db.Query(HouseholdSearchSql & "WHERE f.family_id = @Id", New With {.Id = loadedFamilyId.Value})
                If hh.Rows.Count > 0 Then
                    Dim item = HouseholdItem.FromRow(hh.Rows(0))
                    rbHouseholdYes.Checked = True
                    cboHousehold.Items.Clear()
                    cboHousehold.Items.Add(item)
                    cboHousehold.SelectedItem = item
                    chkHeadExisting.Checked = loadedIsHead
                End If
            End If
        End Sub

        ' ---------------- Wizard flow ----------------

        Private Sub ShowStep(index As Integer)
            If index < 0 OrElse index > steps.Length - 1 Then Return
            lblFormError.Text = ""
            For i = 0 To steps.Length - 1
                steps(i).Visible = (i = index)
            Next
            currentStep = index
            btnBack.Visible = currentStep > 0
            btnNext.Visible = currentStep < steps.Length - 1
            btnSave.Visible = currentStep = steps.Length - 1
            If currentStep = 1 AndAlso rbHouseholdNo.Checked Then FillNewHouseholdPreview()
            If currentStep = steps.Length - 1 Then BuildReviewText()
            UpdateProgressBar()
        End Sub

        Private Sub UpdateProgressBar()
            If pnlStepper IsNot Nothing Then pnlStepper.Invalidate()
        End Sub

        Private Sub TryAdvance()
            If Not ValidateStep(currentStep) Then Return
            ShowStep(currentStep + 1)
        End Sub

        Private Function ValidateStep(index As Integer) As Boolean
            lblFormError.Text = ""
            Select Case index
                Case 0
                    If String.IsNullOrWhiteSpace(txtFirstName.Text) OrElse String.IsNullOrWhiteSpace(txtLastName.Text) Then
                        lblFormError.Text = "First name and last name are required."
                        Return False
                    End If
                    If cboSex.SelectedItem Is Nothing Then
                        lblFormError.Text = "Sex is required."
                        Return False
                    End If
                Case 1
                    If String.IsNullOrWhiteSpace(txtContact.Text) Then
                        lblFormError.Text = "Contact number is required."
                        Return False
                    End If
                    If cboBarangay.SelectedItem Is Nothing Then
                        lblFormError.Text = "Barangay is required."
                        Return False
                    End If
                    ' Edit mode may leave an un-linked legacy patient as-is; new registrations must answer.
                    If Not editingPatientId.HasValue AndAlso Not rbHouseholdYes.Checked AndAlso Not rbHouseholdNo.Checked Then
                        lblFormError.Text = "Please answer whether the patient is part of an existing household."
                        Return False
                    End If
                    If rbHouseholdYes.Checked AndAlso selectedHousehold Is Nothing Then
                        lblFormError.Text = "Search and select the patient's household from the suggestions (or choose ""No"" to register a new one)."
                        Return False
                    End If
                    Dim contact = txtContact.Text.Trim()
                    Dim altContact = txtAltContact.Text.Trim()
                    Dim emergContact = txtEmergContact.Text.Trim()
                    If altContact.Length > 0 AndAlso altContact.Equals(contact, StringComparison.OrdinalIgnoreCase) Then
                        lblFormError.Text = "Alternate contact must be different from the contact number."
                        Return False
                    End If
                    If emergContact.Length > 0 Then
                        If emergContact.Equals(contact, StringComparison.OrdinalIgnoreCase) Then
                            lblFormError.Text = "Emergency contact must be different from the contact number."
                            Return False
                        End If
                        If altContact.Length > 0 AndAlso emergContact.Equals(altContact, StringComparison.OrdinalIgnoreCase) Then
                            lblFormError.Text = "Emergency contact must be different from the alternate contact."
                            Return False
                        End If
                    End If
            End Select
            Return True
        End Function

        Private Sub BuildReviewText()
            Dim barangayName = If(cboBarangay.SelectedItem IsNot Nothing, CType(cboBarangay.SelectedItem, BarangayItem).Name, "-")
            Dim fullName = String.Join(" ", {txtFirstName.Text.Trim(), txtMiddleName.Text.Trim(), txtLastName.Text.Trim(), txtSuffix.Text.Trim()}.Where(Function(x) x.Length > 0))
            Dim emerg = If(String.IsNullOrWhiteSpace(txtEmergName.Text), "-",
                           $"{txtEmergName.Text.Trim()} ({txtEmergRelationship_Text()})" & If(String.IsNullOrWhiteSpace(txtEmergContact.Text), "", $" - {txtEmergContact.Text.Trim()}"))
            Dim philhealth = If(String.IsNullOrWhiteSpace(txtPhilhealthNo.Text), "-",
                                $"{txtPhilhealthNo.Text.Trim()} ({cboPhilhealthStatus.Text} / {cboPhilhealthCategory.Text})")

            ' Two columns, read left-to-right then down, like the reference mockup.
            Dim items As (String, String)() = {
                ("Full Name", fullName),
                ("Date of Birth", dtpDob.Value.ToString("yyyy-MM-dd")),
                ("Age", lblAge.Text.Replace("yrs old", "years old")),
                ("Sex", OrDash(cboSex.Text)),
                ("Civil Status", OrDash(cboCivilStatus.Text)),
                ("Occupation", OrDash(txtOccupation.Text)),
                ("Contact No.", OrDash(txtContact.Text)),
                ("Alternate Contact", OrDash(txtAltContact.Text)),
                ("Address", OrDash(txtAddress.Text)),
                ("Barangay", barangayName),
                ("Municipality", "Vinzons, Camarines Norte"),
                ("Emergency Contact", emerg),
                ("Household", HouseholdReviewText()),
                ("PhilHealth", philhealth),
                ("Blood Type", OrDash(cboBloodType.Text)),
                ("Membership", OrDash(cboMembership.Text)),
                ("Allergies", AllergiesOrNA()),
                ("Existing Conditions", If(String.IsNullOrWhiteSpace(txtConditions.Text), "None declared", txtConditions.Text.Trim()))
            }

            pnlReview.SuspendLayout()
            UiTheme.ClearAndDispose(pnlReview)
            pnlReview.RowStyles.Clear()
            pnlReview.RowCount = 0
            Dim captionFont As New Font("Segoe UI", 8.5F)
            Dim valueFont As New Font("Segoe UI", 10.5F, FontStyle.Bold)
            For i = 0 To items.Length - 1
                Dim cell As New FlowLayoutPanel With {
                    .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoSize = True,
                    .Margin = New Padding(0, 0, 16, 16), .BackColor = Color.Transparent
                }
                cell.Controls.Add(New Label With {.Text = items(i).Item1, .Font = captionFont, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Margin = New Padding(0, 0, 0, 2)})
                cell.Controls.Add(New Label With {.Text = items(i).Item2, .Font = valueFont, .ForeColor = UiTheme.TextDark, .AutoSize = True, .MaximumSize = New Size(370, 0), .Margin = New Padding(0)})
                pnlReview.Controls.Add(cell, i Mod 2, i \ 2)
            Next
            pnlReview.ResumeLayout()
        End Sub

        Private Shared Function OrDash(s As String) As String
            Return If(String.IsNullOrWhiteSpace(s), "-", s.Trim())
        End Function

        Private Function HouseholdReviewText() As String
            If rbHouseholdYes.Checked AndAlso selectedHousehold IsNot Nothing Then
                Return selectedHousehold.Label & If(chkHeadExisting.Checked, "  (patient is head)", "  (member)")
            End If
            If rbHouseholdNo.Checked Then Return "New household will be registered" & If(chkHeadNew.Checked, " with this patient as head", "")
            Return "Not linked"
        End Function

        Private Function txtEmergRelationship_Text() As String
            Return If(cboEmergRelationship.SelectedItem IsNot Nothing, cboEmergRelationship.Text, "-")
        End Function

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If Not ValidateStep(0) OrElse Not ValidateStep(1) Then
                ShowStep(0)
                Return
            End If
            If Not UiTheme.Confirm(Me, If(editingPatientId.HasValue,
                                          "Save the changes to this patient record?",
                                          $"Register {txtFirstName.Text.Trim()} {txtLastName.Text.Trim()} as a new patient?"),
                                   If(editingPatientId.HasValue, "Save Changes", "Register Patient")) Then Return

            Try
                Dim barangayId = CType(cboBarangay.SelectedItem, BarangayItem).Id
                Dim is4ps = (cboMembership.SelectedIndex = 1)
                Dim isIndigent = (cboMembership.SelectedIndex = 2)

                ' Generated before the transaction opens -- IdGenerator reads on its own connection.
                Dim newHouseholdNo = If(rbHouseholdNo.Checked, IdGenerator.NextHouseholdNo(), Nothing)

                If editingPatientId.HasValue Then
                    Using tx As New DbTransaction()
                    tx.Execute(
                        "UPDATE patients SET philhealth_no=@PhilhealthNo, first_name=@FirstName, middle_name=@MiddleName, last_name=@LastName,
                            suffix=@Suffix, date_of_birth=@Dob, sex=@Sex, civil_status=@CivilStatus, occupation=@Occupation,
                            contact_number=@Contact, alternate_contact=@AltContact, address=@Address, barangay_id=@BarangayId,
                            emergency_contact_name=@EmergName, emergency_relationship=@EmergRel, emergency_contact_no=@EmergContact,
                            blood_type=@BloodType, is_4ps_member=@Is4ps, is_indigent=@IsIndigent,
                            known_allergies=@Allergies, existing_conditions=@Conditions,
                            philhealth_status=@PhStatus, philhealth_category=@PhCategory, date_updated = NOW()
                         WHERE patient_id=@Id",
                        BuildSaveParams(barangayId, is4ps, isIndigent, editingPatientId.Value))
                    ApplyHousehold(tx, editingPatientId.Value, barangayId, newHouseholdNo)
                    tx.Commit()
                    End Using
                    AuditLogger.Log("UPDATE", "patients", $"Patient #{editingPatientId.Value}", $"Updated patient {txtLastName.Text}, {txtFirstName.Text}")
                    MessageBox.Show("Patient record updated.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    changes.Reset()
                    onSaved?.Invoke(editingPatientId.Value)
                    ' Edit mode is only ever reached via PatientProfileForm's ShowDialog -- safe to close here;
                    ' new-registration mode (the Else branch below) stays open via ResetWizard for rapid re-entry.
                    Me.DialogResult = DialogResult.OK
                    Me.Close()
                Else
                    Dim patientNo = IdGenerator.NextPatientNo()
                    Dim newId As Long
                    Using tx As New DbTransaction()
                    newId = tx.ExecuteInsertReturnId(
                        "INSERT INTO patients (patient_no, philhealth_no, first_name, middle_name, last_name, suffix, date_of_birth, sex, civil_status,
                            occupation, contact_number, alternate_contact, address, barangay_id, emergency_contact_name, emergency_relationship, emergency_contact_no,
                            blood_type, is_4ps_member, is_indigent, known_allergies, existing_conditions, philhealth_status, philhealth_category,
                            status, date_registered, registered_by)
                         VALUES (@PatientNo, @PhilhealthNo, @FirstName, @MiddleName, @LastName, @Suffix, @Dob, @Sex, @CivilStatus,
                            @Occupation, @Contact, @AltContact, @Address, @BarangayId, @EmergName, @EmergRel, @EmergContact,
                            @BloodType, @Is4ps, @IsIndigent, @Allergies, @Conditions, @PhStatus, @PhCategory,
                            'Active', NOW(), @RegisteredBy)",
                        BuildSaveParamsForInsert(patientNo, barangayId, is4ps, isIndigent))
                    ApplyHousehold(tx, CInt(newId), barangayId, newHouseholdNo)
                    tx.Commit()
                    End Using

                    AuditLogger.Log("CREATE", "patients", patientNo, $"Registered new patient {txtLastName.Text}, {txtFirstName.Text}")
                    MessageBox.Show($"Patient {patientNo} registered successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    changes.Reset() ' saved -- moving on to triage must not ask about discarding
                    onSaved?.Invoke(CInt(newId))
                    ResetWizard()
                End If
            Catch ex As Exception
                lblFormError.Text = "Could not save patient: " & ex.Message
                UiTheme.ShowError(Me, "save the patient", ex)
            End Try
        End Sub

        ''' <summary>
        ''' Links the patient to the chosen/new household inside the same transaction as the
        ''' patient save, and keeps families_households.family_head_id and every member's
        ''' patients.family_head_or_no flag in agreement (same rule as FamilyHouseholdForm).
        ''' </summary>
        Private Sub ApplyHousehold(tx As DbTransaction, patientId As Integer, barangayId As Integer, newHouseholdNo As String)
            Dim familyId As Integer
            Dim makeHead As Boolean
            If rbHouseholdNo.Checked Then
                familyId = CInt(tx.ExecuteInsertReturnId(
                    "INSERT INTO families_households (household_no, address, barangay_id, household_status, date_registered)
                     VALUES (@HouseholdNo, @Address, @BarangayId, 'Active', CURDATE())",
                    New With {.HouseholdNo = newHouseholdNo, .Address = EmptyToNull(txtAddress.Text), .BarangayId = barangayId}))
                makeHead = chkHeadNew.Checked
                AuditLogger.Log("CREATE", "family", newHouseholdNo, $"Registered new household during registration of {txtLastName.Text}, {txtFirstName.Text}")
            ElseIf rbHouseholdYes.Checked AndAlso selectedHousehold IsNot Nothing Then
                familyId = selectedHousehold.Id
                makeHead = chkHeadExisting.Checked
            Else
                Return ' edit of a legacy un-linked patient with no answer: leave as-is
            End If

            ' Leaving a household they headed (moved, or no longer head) clears that household's head.
            tx.Execute("UPDATE families_households SET family_head_id = NULL, date_updated = NOW()
                        WHERE family_head_id = @PatientId AND (family_id <> @FamilyId OR @MakeHead = FALSE)",
                       New With {.PatientId = patientId, .FamilyId = familyId, .MakeHead = makeHead})
            tx.Execute("UPDATE patients SET family_id = @FamilyId, family_head_or_no = FALSE WHERE patient_id = @PatientId",
                       New With {.FamilyId = familyId, .PatientId = patientId})
            If makeHead Then
                tx.Execute("UPDATE patients SET family_head_or_no = FALSE WHERE family_id = @FamilyId", New With {.FamilyId = familyId})
                tx.Execute("UPDATE patients SET family_head_or_no = TRUE WHERE patient_id = @PatientId", New With {.PatientId = patientId})
                tx.Execute("UPDATE families_households SET family_head_id = @PatientId, date_updated = NOW() WHERE family_id = @FamilyId",
                           New With {.PatientId = patientId, .FamilyId = familyId})
            End If
        End Sub

        Private Function BuildSaveParams(barangayId As Integer, is4ps As Boolean, isIndigent As Boolean, patientId As Integer) As Object
            Return New With {
                .PhilhealthNo = EmptyToNull(txtPhilhealthNo.Text), .FirstName = txtFirstName.Text.Trim(), .MiddleName = EmptyToNull(txtMiddleName.Text),
                .LastName = txtLastName.Text.Trim(), .Suffix = EmptyToNull(txtSuffix.Text), .Dob = dtpDob.Value.Date, .Sex = cboSex.Text,
                .CivilStatus = EmptyToNull(cboCivilStatus.Text), .Occupation = EmptyToNull(txtOccupation.Text), .Contact = txtContact.Text.Trim(),
                .AltContact = EmptyToNull(txtAltContact.Text), .Address = EmptyToNull(txtAddress.Text), .BarangayId = barangayId,
                .EmergName = EmptyToNull(txtEmergName.Text), .EmergRel = EmptyToNull(cboEmergRelationship.Text), .EmergContact = EmptyToNull(txtEmergContact.Text),
                .BloodType = EmptyToNull(cboBloodType.Text), .Is4ps = is4ps, .IsIndigent = isIndigent,
                .Allergies = AllergiesOrNA(), .Conditions = EmptyToNull(txtConditions.Text),
                .PhStatus = EmptyToNull(cboPhilhealthStatus.Text), .PhCategory = EmptyToNull(cboPhilhealthCategory.Text),
                .Id = patientId
            }
        End Function

        Private Function BuildSaveParamsForInsert(patientNo As String, barangayId As Integer, is4ps As Boolean, isIndigent As Boolean) As Object
            Return New With {
                .PatientNo = patientNo,
                .PhilhealthNo = EmptyToNull(txtPhilhealthNo.Text), .FirstName = txtFirstName.Text.Trim(), .MiddleName = EmptyToNull(txtMiddleName.Text),
                .LastName = txtLastName.Text.Trim(), .Suffix = EmptyToNull(txtSuffix.Text), .Dob = dtpDob.Value.Date, .Sex = cboSex.Text,
                .CivilStatus = EmptyToNull(cboCivilStatus.Text), .Occupation = EmptyToNull(txtOccupation.Text), .Contact = txtContact.Text.Trim(),
                .AltContact = EmptyToNull(txtAltContact.Text), .Address = EmptyToNull(txtAddress.Text), .BarangayId = barangayId,
                .EmergName = EmptyToNull(txtEmergName.Text), .EmergRel = EmptyToNull(cboEmergRelationship.Text), .EmergContact = EmptyToNull(txtEmergContact.Text),
                .BloodType = EmptyToNull(cboBloodType.Text), .Is4ps = is4ps, .IsIndigent = isIndigent,
                .Allergies = AllergiesOrNA(), .Conditions = EmptyToNull(txtConditions.Text),
                .PhStatus = EmptyToNull(cboPhilhealthStatus.Text), .PhCategory = EmptyToNull(cboPhilhealthCategory.Text),
                .RegisteredBy = If(Models.Session.IsLoggedIn, CType(Models.Session.Current.UserId, Object), DBNull.Value)
            }
        End Function

        Private Function EmptyToNull(s As String) As Object
            Return If(String.IsNullOrWhiteSpace(s), CType(DBNull.Value, Object), s.Trim())
        End Function

        ''' <summary>An unfilled Allergies field is ambiguous (never asked? none reported?) -- defaulting it to "N/A" makes "checked, nothing to report" explicit everywhere it's read (encounter banner tags, review screen, etc.).</summary>
        Private Function AllergiesOrNA() As String
            Return If(String.IsNullOrWhiteSpace(txtAllergies.Text), "N/A", txtAllergies.Text.Trim())
        End Function

        Private Sub ResetWizard()
            txtPhilhealthNo.Clear() : txtFirstName.Clear() : txtMiddleName.Clear() : txtLastName.Clear() : txtSuffix.Clear() : txtOccupation.Clear()
            dtpDob.Value = DateTime.Today.AddYears(-20)
            cboSex.SelectedIndex = -1 : cboCivilStatus.SelectedIndex = -1
            txtContact.Clear() : txtAltContact.Clear() : txtAddress.Clear()
            If cboBarangay.Items.Count > 0 Then cboBarangay.SelectedIndex = 0
            txtEmergName.Clear() : cboEmergRelationship.SelectedIndex = -1 : txtEmergContact.Clear()
            cboBloodType.SelectedIndex = -1 : cboMembership.SelectedIndex = -1
            txtAllergies.Clear() : txtConditions.Clear()
            cboPhilhealthStatus.SelectedIndex = -1 : cboPhilhealthCategory.SelectedIndex = -1
            rbHouseholdYes.Checked = False : rbHouseholdNo.Checked = False
            cboHousehold.Items.Clear() : cboHousehold.Text = "" : selectedHousehold = Nothing
            chkHeadExisting.Checked = False : chkHeadNew.Checked = True
            lblFormError.Text = ""
            ShowStep(0)
            changes?.Reset()
        End Sub

    End Class

End Namespace
