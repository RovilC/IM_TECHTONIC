Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' Issue a medical certificate (Figure 20). Certificates are stored in
    ''' other_documents_services and must be tied to an encounter (the
    ''' table's encounter_id is NOT NULL) -- pass a fixed encounterId when
    ''' opened from the encounter workspace, or Nothing to let the user pick
    ''' any patient + one of their existing encounters.
    ''' </summary>
    Public Class IssueCertificateDialog
        Inherits Form

        Private ReadOnly fixedEncounterId As Integer?
        Private cboPatient As ComboBox
        Private cboEncounter As ComboBox
        Private lblPatientFixed As Label
        Private txtPurpose As TextBox
        Private txtRemarks As TextBox
        Private lblError As Label
        Public IssuedDocumentId As Integer? = Nothing

        Private Class PickItem
            Public ReadOnly Id As Integer
            Public ReadOnly Label As String
            Public Sub New(id As Integer, label As String)
                Me.Id = id : Me.Label = label
            End Sub
            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

        ''' <summary>Open pre-bound to one encounter (from the encounter workspace).</summary>
        Public Sub New(encounterId As Integer, patientDisplayName As String)
            Me.fixedEncounterId = encounterId
            InitializeComponent(patientDisplayName)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        ''' <summary>Open free-standing (from the Medical Certificates module): user picks patient then encounter.</summary>
        Public Sub New()
            Me.fixedEncounterId = Nothing
            InitializeComponent(Nothing)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub InitializeComponent(fixedPatientName As String)
            Me.Text = "Issue Medical Certificate"
            ' Width was 520 while Issue Certificate sits at X=390 with Width=160 (right edge 550) --
            ' past the window's own outer edge, so it rendered clipped regardless of the height fix below.
            Me.Size = New Size(580, 480)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Dim card = ClinicalUi.NewDialogCard(Me, 548)
            Const x As Integer = 24
            Const w As Integer = 500

            card.Controls.Add(New Label With {.Text = "Issue Medical Certificate", .Font = New Font("Segoe UI Semibold", 11.5F), .ForeColor = ClinicalUi.TextMain, .AutoSize = True, .Location = New Point(x, 20)})

            Dim y = 62
            If fixedEncounterId.HasValue Then
                lblPatientFixed = New Label With {.Text = fixedPatientName, .ForeColor = ClinicalUi.TextSoft, .AutoSize = True, .Location = New Point(x, y)}
                card.Controls.Add(lblPatientFixed)
                y += 36
            Else
                ClinicalUi.AddLabelAt(card, "Patient * (type to search)", x, y)
                cboPatient = New ComboBox()
                UiTheme.AttachSearchableCombo(cboPatient, 2, Function(text) SearchPatients(text))
                AddHandler cboPatient.SelectedIndexChanged, AddressOf CboPatient_SelectedIndexChanged
                ClinicalUi.PlaceCombo(card, cboPatient, x, y + 22, w)
                y += 70

                ClinicalUi.AddLabelAt(card, "Encounter *", x, y)
                cboEncounter = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
                ClinicalUi.PlaceCombo(card, cboEncounter, x, y + 22, w)
                y += 70
            End If

            ClinicalUi.AddLabelAt(card, "Purpose *", x, y)
            txtPurpose = New TextBox With {.PlaceholderText = "e.g. Employment, School requirement, Travel"}
            ClinicalUi.PlaceInput(card, txtPurpose, x, y + 22, w)
            y += 74

            ClinicalUi.AddLabelAt(card, "Remarks / Findings", x, y)
            txtRemarks = New TextBox With {.Multiline = True}
            ClinicalUi.PlaceInput(card, txtRemarks, x, y + 22, w, 90)
            y += 22 + 90 + 16

            lblError = New Label With {.ForeColor = ClinicalUi.DangerText, .AutoSize = True, .Location = New Point(x, y), .MaximumSize = New Size(w, 0)}
            card.Controls.Add(lblError)
            y += 40

            ' Footer: divider, Cancel on the left, the primary action on the right.
            card.Controls.Add(New Panel With {.Location = New Point(x, y), .Size = New Size(w, 1), .BackColor = ClinicalUi.CardBorder})
            y += 18

            Dim btnCancel As New Button With {.Text = "Cancel", .Location = New Point(x, y + 4)}
            ClinicalUi.StyleCancel(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            card.Controls.Add(btnCancel)

            Dim btnIssue As New Button With {.Text = "Issue Certificate", .Size = New Size(170, 38)}
            ClinicalUi.StylePrimary(btnIssue)
            btnIssue.Location = New Point(x + w - 170, y)
            AddHandler btnIssue.Click, AddressOf BtnIssue_Click
            card.Controls.Add(btnIssue)

            ' The fixed-encounter variant (opened from the encounter workspace) has fewer fields than the
            ' free-standing one (which adds patient + encounter pickers); size to the content actually built.
            card.Height = y + 38 + 22
            Me.ClientSize = New Size(580, card.Bottom + 16)

        End Sub

        ''' <summary>Backs the searchable patient combo with a bounded query instead of preloading all 10,000+ patients.</summary>
        Private Function SearchPatients(text As String) As List(Of Object)
            Dim results As New List(Of Object)
            Try
                Dim likePattern = "%" & text.Trim() & "%"
                For Each row As DataRow In Db.Query(
                    "SELECT patient_id, patient_no, first_name, last_name FROM patients
                     WHERE first_name LIKE @Like OR last_name LIKE @Like OR patient_no LIKE @Like
                     ORDER BY last_name, first_name LIMIT 20", New With {.Like = likePattern}).Rows
                    results.Add(New PickItem(Convert.ToInt32(row("patient_id")), $"{row("last_name")}, {row("first_name")} ({row("patient_no")})"))
                Next
            Catch
                ' leave results empty -- the combo just won't show anything this keystroke
            End Try
            Return results
        End Function

        Private Sub CboPatient_SelectedIndexChanged(sender As Object, e As EventArgs)
            cboEncounter.Items.Clear()
            If cboPatient.SelectedItem Is Nothing Then Return
            Dim patientId = CType(cboPatient.SelectedItem, PickItem).Id
            Try
                For Each row As DataRow In Db.Query(
                    "SELECT encounter_id, encounter_no, encounter_date FROM encounters WHERE patient_id = @Id ORDER BY encounter_date DESC",
                    New With {.Id = patientId}).Rows
                    cboEncounter.Items.Add(New PickItem(Convert.ToInt32(row("encounter_id")), $"{row("encounter_no")} - {Convert.ToDateTime(row("encounter_date")):MMM dd, yyyy}"))
                Next
                If cboEncounter.Items.Count > 0 Then
                    cboEncounter.SelectedIndex = 0
                Else
                    lblError.Text = "This patient has no encounters yet. Start an encounter before issuing a certificate."
                End If
            Catch ex As Exception
                lblError.Text = "Could not load encounters: " & ex.Message
            End Try
        End Sub

        Private Sub BtnIssue_Click(sender As Object, e As EventArgs)
            lblError.Text = ""
            If String.IsNullOrWhiteSpace(txtPurpose.Text) Then
                lblError.Text = "Purpose is required."
                Return
            End If

            Dim encounterId As Integer
            If fixedEncounterId.HasValue Then
                encounterId = fixedEncounterId.Value
            Else
                If cboEncounter.SelectedItem Is Nothing Then
                    lblError.Text = "Select an encounter (or select a patient with at least one encounter)."
                    Return
                End If
                encounterId = CType(cboEncounter.SelectedItem, PickItem).Id
            End If
            If Not UiTheme.Confirm(Me, "Submit this medical certificate request to RHU Management for issuance?", "Request Medical Certificate") Then Return

            Try
                Dim referenceNo = $"MC-{DateTime.Now:yyyy}-{DateTime.Now:MMddHHmmss}"
                Dim docName = "Medical Certificate"
                Dim newId = Db.ExecuteInsertReturnId(
                    "INSERT INTO other_documents_services (encounter_id, document_type, document_name, purpose, created_by, created_at, status, remarks, reference_no)
                     VALUES (@EncounterId, 'Medical Certificate', @DocName, @Purpose, @CreatedBy, NOW(), 'For Approval', @Remarks, @ReferenceNo)",
                    New With {
                        .EncounterId = encounterId, .DocName = docName, .Purpose = txtPurpose.Text.Trim(), .CreatedBy = Session.Current.UserId,
                        .Remarks = If(String.IsNullOrWhiteSpace(txtRemarks.Text), CType(DBNull.Value, Object), txtRemarks.Text.Trim()), .ReferenceNo = referenceNo
                    })
                AuditLogger.Log("CREATE", "certificates", referenceNo, $"Requested medical certificate for encounter #{encounterId} (for approval)")
                IssuedDocumentId = CInt(newId)
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not issue certificate: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
