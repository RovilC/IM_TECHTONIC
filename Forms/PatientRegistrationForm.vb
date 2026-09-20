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

        Private ReadOnly editingPatientId As Integer?
        Private ReadOnly onSaved As Action(Of Integer)

        Private steps As Panel()
        Private currentStep As Integer = 0
        Private lblStepIndicator As Label
        Private btnBack, btnNext, btnCancel, btnSave As Button
        Private pnlButtons As Panel
        Private pnlBody As Panel
        Private lblFormError As Label
        Private pnlProgressTrack, pnlProgressFill As Panel

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

        ' Step 3
        Private cboBloodType, cboMembership, cboPhilhealthStatus, cboPhilhealthCategory As ComboBox
        Private txtAllergies, txtConditions As TextBox

        ' Step 4
        Private lblReview As Label

        Public Sub New(patientId As Integer?, onSaved As Action(Of Integer))
            Me.editingPatientId = patientId
            Me.onSaved = onSaved
            InitializeComponent()
            LoadLookups()
            If editingPatientId.HasValue Then LoadExisting(editingPatientId.Value)
            ShowStep(0)
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(header)
            header.Controls.Add(New Label With {
                .Text = If(editingPatientId.HasValue, "Edit Patient Record", "Patient Registration"),
                .Font = UiTheme.SubHeaderFont, .ForeColor = UiTheme.TextDark, .AutoSize = True, .Location = New Point(0, 4)
            })
            lblStepIndicator = New Label With {.Text = "", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(0, 28)}
            header.Controls.Add(lblStepIndicator)

            ' Visual step progress -- a thin fill bar under the header that grows with currentStep,
            ' kept in sync with the track's actual (possibly not-yet-final at construction time)
            ' width via a Resize handler rather than a one-time pixel calculation.
            pnlProgressTrack = New Panel With {.Dock = DockStyle.Top, .Height = 5, .BackColor = UiTheme.BorderLight}
            Me.Controls.Add(pnlProgressTrack)
            pnlProgressFill = New Panel With {.Dock = DockStyle.Left, .Width = 0, .BackColor = UiTheme.PrimaryGreen}
            pnlProgressTrack.Controls.Add(pnlProgressFill)
            AddHandler pnlProgressTrack.Resize, Sub() UpdateProgressBar()

            pnlButtons = New Panel With {.Dock = DockStyle.Bottom, .Height = 56}
            Me.Controls.Add(pnlButtons)

            btnCancel = New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(0, 10)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() ResetWizard()
            pnlButtons.Controls.Add(btnCancel)

            btnBack = New Button With {.Text = "<- Back", .Size = New Size(100, 36), .Location = New Point(110, 10)}
            UiTheme.StyleSecondaryButton(btnBack)
            AddHandler btnBack.Click, Sub() ShowStep(currentStep - 1)
            pnlButtons.Controls.Add(btnBack)

            btnNext = New Button With {.Text = "Next ->", .Size = New Size(110, 36)}
            UiTheme.StylePrimaryButton(btnNext)
            UiTheme.PinToRight(btnNext, pnlButtons, 0, 10)
            AddHandler btnNext.Click, Sub() TryAdvance()
            pnlButtons.Controls.Add(btnNext)

            btnSave = New Button With {.Text = "Save Patient", .Size = New Size(140, 36)}
            UiTheme.StylePrimaryButton(btnSave)
            UiTheme.PinToRight(btnSave, pnlButtons, 0, 10)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            pnlButtons.Controls.Add(btnSave)

            ' Panel is 24px tall but the label sat at Y=40 inside it -- entirely below the
            ' panel's own visible bounds, so every validation error on this form (including the
            ' new contact-number checks) rendered invisible. Panel height now actually fits the
            ' label, with room for a two-line wrapped message.
            lblFormError = New Label With {.Text = "", .ForeColor = UiTheme.DangerRed, .AutoSize = True, .MaximumSize = New Size(700, 0), .Location = New Point(0, 4)}
            Dim errPanel As New Panel With {.Dock = DockStyle.Top, .Height = 40}
            errPanel.Controls.Add(lblFormError)
            Me.Controls.Add(errPanel)

            pnlBody = New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
            Me.Controls.Add(pnlBody)
            pnlBody.BringToFront()

            steps = New Panel() {BuildStep1(), BuildStep2(), BuildStep3(), BuildStep4()}
            For Each s In steps
                s.Visible = False
                s.Dock = DockStyle.Fill
                pnlBody.Controls.Add(s)
            Next

            ' PhilHealth Status/Category only mean anything once a PhilHealth No. is on file --
            ' wired here (not in BuildStep1/BuildStep3) since both controls must already exist.
            AddHandler txtPhilhealthNo.TextChanged, Sub() UpdatePhilhealthFieldsState()
            UpdatePhilhealthFieldsState()
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
            container.Controls.Add(New Label With {.Text = label, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(x, y)})
            ctrl.Location = New Point(x, y + 20)
            ctrl.Width = width
            container.Controls.Add(ctrl)
            Return ctrl
        End Function

        Private Function BuildStep1() As Panel
            Dim p As New Panel()
            p.Controls.Add(New Label With {.Text = "Personal Information", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            txtPhilhealthNo = New TextBox() : LabeledField(p, "PhilHealth No.", 0, 40, txtPhilhealthNo)
            txtFirstName = New TextBox() : LabeledField(p, "First Name *", 280, 40, txtFirstName)
            txtMiddleName = New TextBox() : LabeledField(p, "Middle Name", 560, 40, txtMiddleName)

            txtLastName = New TextBox() : LabeledField(p, "Last Name *", 0, 96, txtLastName)
            txtSuffix = New TextBox() : LabeledField(p, "Suffix", 280, 96, txtSuffix, 120)

            dtpDob = New DateTimePicker With {.Format = DateTimePickerFormat.Short, .MaxDate = DateTime.Today}
            LabeledField(p, "Date of Birth *", 0, 152, dtpDob)
            AddHandler dtpDob.ValueChanged, Sub() UpdateAgeLabel()
            lblAge = New Label With {.Text = "", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 176)}
            p.Controls.Add(lblAge)

            cboSex = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboSex.Items.AddRange({"Male", "Female"})
            LabeledField(p, "Sex *", 560, 152, cboSex, 150)

            cboCivilStatus = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboCivilStatus.Items.AddRange({"Single", "Married", "Widowed", "Separated", "Divorced"})
            LabeledField(p, "Civil Status", 0, 208, cboCivilStatus)

            txtOccupation = New TextBox() : LabeledField(p, "Occupation", 280, 208, txtOccupation)

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
            p.Controls.Add(New Label With {.Text = "Contact & Address", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(0, 0)})

            txtContact = New TextBox() : LabeledField(p, "Contact Number *", 0, 40, txtContact)
            txtAltContact = New TextBox() : LabeledField(p, "Alternate Contact", 280, 40, txtAltContact)

            txtAddress = New TextBox() : LabeledField(p, "House No. / Street / Purok", 0, 96, txtAddress, 540)

            cboBarangay = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            LabeledField(p, "Barangay *", 0, 152, cboBarangay)
            p.Controls.Add(New Label With {.Text = "Municipality: Vinzons   |   Province: Camarines Norte", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(280, 176)})

            p.Controls.Add(New Label With {.Text = "Emergency Contact (optional)", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(0, 220)})
            txtEmergName = New TextBox() : LabeledField(p, "Contact Name", 0, 250, txtEmergName)
            cboEmergRelationship = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboEmergRelationship.Items.AddRange({"Parent", "Spouse", "Sibling", "Child", "Relative", "Guardian", "Other"})
            LabeledField(p, "Relationship", 280, 250, cboEmergRelationship, 180)
            txtEmergContact = New TextBox() : LabeledField(p, "Contact No.", 480, 250, txtEmergContact)

            Return p
        End Function

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
            lblReview = New Label With {.AutoSize = True, .Location = New Point(0, 40), .Font = UiTheme.BaseFont}
            p.Controls.Add(lblReview)
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
        End Sub

        ' ---------------- Wizard flow ----------------

        Private Sub ShowStep(index As Integer)
            If index < 0 OrElse index > steps.Length - 1 Then Return
            lblFormError.Text = ""
            For i = 0 To steps.Length - 1
                steps(i).Visible = (i = index)
            Next
            currentStep = index
            lblStepIndicator.Text = $"Step {currentStep + 1} of {steps.Length}"
            btnBack.Visible = currentStep > 0
            btnNext.Visible = currentStep < steps.Length - 1
            btnSave.Visible = currentStep = steps.Length - 1
            If currentStep = steps.Length - 1 Then BuildReviewText()
            UpdateProgressBar()
        End Sub

        Private Sub UpdateProgressBar()
            If pnlProgressTrack Is Nothing OrElse pnlProgressTrack.Width <= 0 Then Return
            Dim pct = (currentStep + 1) / CDbl(steps.Length)
            pnlProgressFill.Width = CInt(pnlProgressTrack.Width * pct)
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
            Dim barangayName = If(cboBarangay.SelectedItem IsNot Nothing, CType(cboBarangay.SelectedItem, BarangayItem).Name, "")
            lblReview.Text =
                $"Full Name: {txtLastName.Text}, {txtFirstName.Text} {txtMiddleName.Text} {txtSuffix.Text}" & vbCrLf &
                $"Date of Birth: {dtpDob.Value:MM/dd/yyyy}   ({lblAge.Text})" & vbCrLf &
                $"Sex: {cboSex.Text}   Civil Status: {cboCivilStatus.Text}   Occupation: {txtOccupation.Text}" & vbCrLf & vbCrLf &
                $"Contact: {txtContact.Text}   Alt. Contact: {txtAltContact.Text}" & vbCrLf &
                $"Address: {txtAddress.Text}, {barangayName}, Vinzons, Camarines Norte" & vbCrLf &
                $"Emergency Contact: {txtEmergName.Text} ({txtEmergRelationship_Text()}) - {txtEmergContact.Text}" & vbCrLf & vbCrLf &
                $"Blood Type: {cboBloodType.Text}   Membership: {cboMembership.Text}" & vbCrLf &
                $"PhilHealth: {cboPhilhealthStatus.Text} / {cboPhilhealthCategory.Text}   No: {txtPhilhealthNo.Text}" & vbCrLf &
                $"Allergies: {AllergiesOrNA()}" & vbCrLf &
                $"Existing Conditions: {If(String.IsNullOrWhiteSpace(txtConditions.Text), "None declared", txtConditions.Text)}" & vbCrLf & vbCrLf &
                "Review all information before saving. Go back to correct any errors."
        End Sub

        Private Function txtEmergRelationship_Text() As String
            Return If(cboEmergRelationship.SelectedItem IsNot Nothing, cboEmergRelationship.Text, "-")
        End Function

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If Not ValidateStep(0) OrElse Not ValidateStep(1) Then
                ShowStep(0)
                Return
            End If

            Try
                Dim barangayId = CType(cboBarangay.SelectedItem, BarangayItem).Id
                Dim is4ps = (cboMembership.SelectedIndex = 1)
                Dim isIndigent = (cboMembership.SelectedIndex = 2)

                If editingPatientId.HasValue Then
                    Db.Execute(
                        "UPDATE patients SET philhealth_no=@PhilhealthNo, first_name=@FirstName, middle_name=@MiddleName, last_name=@LastName,
                            suffix=@Suffix, date_of_birth=@Dob, sex=@Sex, civil_status=@CivilStatus, occupation=@Occupation,
                            contact_number=@Contact, alternate_contact=@AltContact, address=@Address, barangay_id=@BarangayId,
                            emergency_contact_name=@EmergName, emergency_relationship=@EmergRel, emergency_contact_no=@EmergContact,
                            blood_type=@BloodType, is_4ps_member=@Is4ps, is_indigent=@IsIndigent,
                            known_allergies=@Allergies, existing_conditions=@Conditions,
                            philhealth_status=@PhStatus, philhealth_category=@PhCategory, date_updated = NOW()
                         WHERE patient_id=@Id",
                        BuildSaveParams(barangayId, is4ps, isIndigent, editingPatientId.Value))
                    AuditLogger.Log("UPDATE", "patients", $"Patient #{editingPatientId.Value}", $"Updated patient {txtLastName.Text}, {txtFirstName.Text}")
                    MessageBox.Show("Patient record updated.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    onSaved?.Invoke(editingPatientId.Value)
                    ' Edit mode is only ever reached via PatientProfileForm's ShowDialog -- safe to close here;
                    ' new-registration mode (the Else branch below) stays open via ResetWizard for rapid re-entry.
                    Me.DialogResult = DialogResult.OK
                    Me.Close()
                Else
                    Dim patientNo = IdGenerator.NextPatientNo()
                    Dim newId = Db.ExecuteInsertReturnId(
                        "INSERT INTO patients (patient_no, philhealth_no, first_name, middle_name, last_name, suffix, date_of_birth, sex, civil_status,
                            occupation, contact_number, alternate_contact, address, barangay_id, emergency_contact_name, emergency_relationship, emergency_contact_no,
                            blood_type, is_4ps_member, is_indigent, known_allergies, existing_conditions, philhealth_status, philhealth_category,
                            status, date_registered, registered_by)
                         VALUES (@PatientNo, @PhilhealthNo, @FirstName, @MiddleName, @LastName, @Suffix, @Dob, @Sex, @CivilStatus,
                            @Occupation, @Contact, @AltContact, @Address, @BarangayId, @EmergName, @EmergRel, @EmergContact,
                            @BloodType, @Is4ps, @IsIndigent, @Allergies, @Conditions, @PhStatus, @PhCategory,
                            'Active', NOW(), @RegisteredBy)",
                        BuildSaveParamsForInsert(patientNo, barangayId, is4ps, isIndigent))

                    AuditLogger.Log("CREATE", "patients", patientNo, $"Registered new patient {txtLastName.Text}, {txtFirstName.Text}")
                    MessageBox.Show($"Patient {patientNo} registered successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    onSaved?.Invoke(CInt(newId))
                    ResetWizard()
                End If
            Catch ex As Exception
                lblFormError.Text = "Could not save patient: " & ex.Message
            End Try
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
            lblFormError.Text = ""
            ShowStep(0)
        End Sub

    End Class

End Namespace
