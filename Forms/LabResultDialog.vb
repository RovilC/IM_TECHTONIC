Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' Enter/release a laboratory result for one request row, recording where
    ''' it was performed: the RHU laboratory or an external laboratory, with
    ''' the clinic/laboratory name and the doctor/pathologist who signed it.
    ''' </summary>
    Public Class LabResultDialog
        Inherits Form

        Public Const RhuLabName As String = "RHU Vinzons Laboratory"
        Private Const SourceRhu As String = "RHU Laboratory"
        Private Const SourceExternal As String = "External Laboratory"

        Private ReadOnly laboratoryId As Integer
        Private rbRhu, rbExternal As RadioButton
        Private txtClinic, txtDoctor, txtResult As TextBox
        Private lblError As Label
        Private externalFacility As String = ""
        Private lastExternalClinic As String = ""
        Private sourceLocked As Boolean = False
        Private lblLocked As Label
        Private btnCancel, btnSave As Button
        Private viewOnly As Boolean = False
        Public Saved As Boolean = False

        Public Sub New(laboratoryId As Integer, patientName As String, testName As String)
            Me.laboratoryId = laboratoryId
            Me.Text = "Laboratory Result"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont
            Me.ClientSize = New Size(520, 522)

            Me.Controls.Add(New Label With {.Text = testName, .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})
            Me.Controls.Add(New Label With {.Text = patientName, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Me.Controls.Add(New Label With {.Text = "Result From *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 78)})
            Dim radios As New Panel With {.Location = New Point(20, 98), .Size = New Size(480, 26)}
            rbRhu = New RadioButton With {.Text = SourceRhu, .AutoSize = True, .Location = New Point(0, 2)}
            rbExternal = New RadioButton With {.Text = SourceExternal, .AutoSize = True, .Location = New Point(160, 2)}
            radios.Controls.AddRange({rbRhu, rbExternal})
            Me.Controls.Add(radios)
            AddHandler rbRhu.CheckedChanged, Sub() UpdateSourceFields()
            AddHandler rbExternal.CheckedChanged, Sub() UpdateSourceFields()

            lblLocked = New Label With {.ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(130, 78)}
            Me.Controls.Add(lblLocked)

            Me.Controls.Add(New Label With {.Text = "Clinic / Laboratory Name *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 134)})
            txtClinic = New TextBox With {.Location = New Point(20, 154), .Width = 480}
            Me.Controls.Add(txtClinic)

            Me.Controls.Add(New Label With {.Text = "Doctor / Pathologist *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 190)})
            txtDoctor = New TextBox With {.Location = New Point(20, 210), .Width = 480, .PlaceholderText = "e.g. Dr. Juan Dela Cruz, MD, FPSP"}
            Me.Controls.Add(txtDoctor)

            Me.Controls.Add(New Label With {.Text = "Result / Findings *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 246)})
            txtResult = New TextBox With {.Location = New Point(20, 266), .Width = 480, .Height = 160, .Multiline = True, .ScrollBars = ScrollBars.Vertical}
            Me.Controls.Add(txtResult)

            ' No status choice: saving a result releases it.
            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 436), .MaximumSize = New Size(480, 0)}
            Me.Controls.Add(lblError)

            btnCancel = New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(240, 470)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            btnSave = New Button With {.Text = "Release Result", .Size = New Size(150, 36), .Location = New Point(350, 470)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)

            LoadExisting()
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub LoadExisting()
            Try
                Dim t = Db.Query(
                    "SELECT l.result, l.status, l.lab_destination, l.external_facility, l.result_source, l.result_clinic, l.result_doctor,
                            l.result_date, u.full_name AS released_by
                     FROM laboratory l LEFT JOIN users_staff u ON u.user_id = l.verified_by
                     WHERE l.laboratory_id = @Id", New With {.Id = laboratoryId})
                If t.Rows.Count = 0 Then Return
                Dim r = t.Rows(0)
                Dim s = Function(col As String) If(IsDBNull(r(col)), "", r(col).ToString())

                externalFacility = s("external_facility")
                Dim isReferred = s("lab_destination") = "External" OrElse s("status") = "Referred"

                txtResult.Text = s("result")
                txtDoctor.Text = s("result_doctor")
                Dim source = s("result_source")
                If source = SourceExternal OrElse (source = "" AndAlso isReferred) Then
                    lastExternalClinic = If(s("result_clinic") <> "", s("result_clinic"), externalFacility)
                    rbExternal.Checked = True
                Else
                    rbRhu.Checked = True
                End If

                ' Once a result's source has been saved it is permanent: the RHU/External choice and the
                ' clinic name are locked here, and the UPDATE below never overwrites them either.
                If source <> "" Then
                    sourceLocked = True
                    rbRhu.Enabled = False : rbExternal.Enabled = False
                    txtClinic.Text = If(s("result_clinic") <> "", s("result_clinic"), txtClinic.Text)
                    txtClinic.ReadOnly = True
                    lblLocked.Text = "(locked -- already recorded)"
                End If

                ' A released result is final: show it read-only with who released it and when.
                If s("status") = "Released" Then
                    viewOnly = True
                    Me.Text = "Laboratory Result (Released)"
                    txtClinic.ReadOnly = True : txtDoctor.ReadOnly = True : txtResult.ReadOnly = True
                    lblLocked.Text = ""
                    btnSave.Visible = False
                    btnCancel.Text = "Close"
                    btnCancel.Location = New Point(400, 470)
                    Dim releasedAt = If(IsDBNull(r("result_date")), "", " on " & Convert.ToDateTime(r("result_date")).ToString("MMM d, yyyy h:mm tt"))
                    Dim releasedBy = If(s("released_by") <> "", " by " & s("released_by"), "")
                    lblError.ForeColor = UiTheme.TextMuted
                    lblError.Text = "Released" & releasedBy & releasedAt & ". This result can no longer be edited."
                End If
            Catch ex As Exception
                lblError.Text = "Could not load request: " & ex.Message
            End Try
        End Sub

        ''' <summary>RHU results always carry the RHU lab's name (read-only); external results take the referred facility by default but stay editable.</summary>
        Private Sub UpdateSourceFields()
            If sourceLocked Then Return
            If rbRhu.Checked Then
                If Not txtClinic.ReadOnly Then lastExternalClinic = txtClinic.Text
                txtClinic.Text = RhuLabName
                txtClinic.ReadOnly = True
            ElseIf rbExternal.Checked Then
                txtClinic.ReadOnly = False
                txtClinic.Text = If(lastExternalClinic <> "", lastExternalClinic, externalFacility)
            End If
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If viewOnly Then Return
            lblError.Text = ""
            If Not rbRhu.Checked AndAlso Not rbExternal.Checked Then
                lblError.Text = "Select whether the result is from the RHU laboratory or an external laboratory."
                Return
            End If
            If String.IsNullOrWhiteSpace(txtResult.Text) Then lblError.Text = "Enter a result before releasing." : Return
            If String.IsNullOrWhiteSpace(txtClinic.Text) Then lblError.Text = "Enter the clinic / laboratory name before releasing." : Return
            If String.IsNullOrWhiteSpace(txtDoctor.Text) Then lblError.Text = "Enter the doctor / pathologist before releasing." : Return
            If Not UiTheme.Confirm(Me, "Release this result? It will be marked Released and visible in the patient's record." &
                                   If(sourceLocked, "", vbCrLf & vbCrLf & "The result source and clinic can't be changed after this."),
                                   "Release Result") Then Return
            Try
                Dim nullIfEmpty = Function(v As String) If(String.IsNullOrWhiteSpace(v), CType(DBNull.Value, Object), v.Trim())
                Dim source = If(rbExternal.Checked, SourceExternal, SourceRhu)
                Dim affected = Db.Execute(
                    "UPDATE laboratory SET result = @Result, status = 'Released', result_date = NOW(), verified_by = @VerifiedBy,
                            result_clinic = IF(result_source IS NULL, @Clinic, result_clinic), -- before result_source: MySQL assigns left to right
                            result_source = COALESCE(result_source, @Source),
                            result_doctor = @Doctor
                     WHERE laboratory_id = @Id AND status <> 'Released'",
                    New With {
                        .Result = nullIfEmpty(txtResult.Text), .VerifiedBy = Session.Current.UserId,
                        .Source = source, .Clinic = nullIfEmpty(txtClinic.Text), .Doctor = nullIfEmpty(txtDoctor.Text), .Id = laboratoryId
                    })
                If affected = 0 Then
                    lblError.Text = "This result was already released and can no longer be changed."
                    Return
                End If
                AuditLogger.Log("UPDATE", "lab", $"Lab request #{laboratoryId}", $"Result released ({source}: {txtClinic.Text.Trim()})")
                Saved = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not save result: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
