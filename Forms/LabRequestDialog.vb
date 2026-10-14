Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Laboratory request form (Figure 30): pick tests by category, one row per test is inserted into `laboratory`.</summary>
    Public Class LabRequestDialog
        Inherits Form

        Private ReadOnly encounterId As Integer

        Private cboPriority, cboSpecimen As ComboBox
        Private txtNotes As TextBox
        Private lblError, lblSelectedCount As Label
        Private checkBoxes As New List(Of CheckBox)
        Public RequestsSubmitted As Boolean = False

        Public Sub New(encounterId As Integer, patientDisplayName As String)
            Me.encounterId = encounterId
            Me.Text = "Laboratory Request"
            ' Height was 640 with content laid out down to y=606 -- once the title bar/borders
            ' (non-client, ~40-45px) are subtracted from that, the buttons landed right at or
            ' past the bottom of the visible client area with no breathing room below them.
            ' Width was 620 while Submit Request sits at X=480 with Width=150 (right edge 630) --
            ' past the window's own outer edge, let alone its client area, so it rendered clipped.
            Me.Size = New Size(660, 700)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = "Laboratory Request", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})
            Me.Controls.Add(New Label With {.Text = patientDisplayName, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Me.Controls.Add(New Label With {.Text = "Priority Level", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 78)})
            cboPriority = New ComboBox With {.Location = New Point(20, 98), .Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboPriority.Items.AddRange(LabTestCatalog.PriorityLevels)
            cboPriority.SelectedIndex = 0
            Me.Controls.Add(cboPriority)

            Me.Controls.Add(New Label With {.Text = "Specimen Type", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(260, 78)})
            cboSpecimen = New ComboBox With {.Location = New Point(260, 98), .Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboSpecimen.Items.AddRange(LabTestCatalog.SpecimenTypes)
            cboSpecimen.SelectedIndex = 0
            Me.Controls.Add(cboSpecimen)

            Me.Controls.Add(New Label With {.Text = "Select Laboratory Tests", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 138)})
            lblSelectedCount = New Label With {.Text = "0 selected", .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(220, 140)}
            Me.Controls.Add(lblSelectedCount)

            Dim testsPanel As New Panel With {.Location = New Point(20, 162), .Size = New Size(560, 300), .AutoScroll = True, .BorderStyle = BorderStyle.FixedSingle}
            Me.Controls.Add(testsPanel)

            Dim y As Integer = 8
            For Each cat In LabTestCatalog.Categories
                testsPanel.Controls.Add(New Label With {.Text = cat.Category, .Font = UiTheme.BoldFont, .ForeColor = UiTheme.PrimaryGreen, .AutoSize = True, .Location = New Point(8, y)})
                y += 22
                For Each testName In cat.Tests
                    Dim chk As New CheckBox With {.Text = testName, .Tag = cat.Category, .AutoSize = True, .Location = New Point(20, y)}
                    AddHandler chk.CheckedChanged, Sub() UpdateSelectedCount()
                    testsPanel.Controls.Add(chk)
                    checkBoxes.Add(chk)
                    y += 22
                Next
                y += 8
            Next

            Me.Controls.Add(New Label With {.Text = "Clinical Notes", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 478)})
            txtNotes = New TextBox With {.Location = New Point(20, 498), .Width = 560, .Height = 50, .Multiline = True}
            Me.Controls.Add(txtNotes)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .MaximumSize = New Size(400, 0), .Location = New Point(20, 558)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(370, 594)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSubmit As New Button With {.Text = "Submit Request", .Size = New Size(150, 36), .Location = New Point(480, 594)}
            UiTheme.StylePrimaryButton(btnSubmit)
            AddHandler btnSubmit.Click, AddressOf BtnSubmit_Click
            Me.Controls.Add(btnSubmit)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub UpdateSelectedCount()
            lblSelectedCount.Text = $"{checkBoxes.Where(Function(c) c.Checked).Count()} selected"
        End Sub

        Private Sub BtnSubmit_Click(sender As Object, e As EventArgs)
            Dim selected = checkBoxes.Where(Function(c) c.Checked).ToList()
            If selected.Count = 0 Then
                lblError.Text = "Select at least one test."
                Return
            End If
            If Not UiTheme.Confirm(Me, $"Submit this request for {selected.Count} test(s) to RHU Management?", "Submit Lab Request") Then Return
            Try
                ' All selected tests are one request from the patient's point of view -- if a
                ' crash mid-loop left only 2 of 4 checked tests actually inserted, the lab would
                ' process an incomplete, silently-truncated order with no indication anything
                ' was missed.
                ' Every test in this submit shares one request_no so RHU Management approves/refers
                ' them together (Lab Requests tab) and they print on one external request form.
                ' New requests start "For Approval" and only reach the lab queue once approved.
                Dim requestNo = $"LR-{DateTime.Now:yyyyMMdd-HHmmssfff}"
                Using tx As New DbTransaction()
                    For Each chk In selected
                        tx.Execute(
                            "INSERT INTO laboratory (encounter_id, requested_by, test_name, test_category, specimen_type, priority_level, clinical_notes, request_date, status, request_no)
                             VALUES (@EncounterId, @RequestedBy, @TestName, @TestCategory, @Specimen, @Priority, @Notes, NOW(), 'For Approval', @RequestNo)",
                            New With {
                                .EncounterId = encounterId, .RequestedBy = Session.Current.UserId, .TestName = chk.Text, .TestCategory = chk.Tag.ToString(), .RequestNo = requestNo,
                                .Specimen = cboSpecimen.Text, .Priority = cboPriority.Text,
                                .Notes = If(String.IsNullOrWhiteSpace(txtNotes.Text), CType(DBNull.Value, Object), txtNotes.Text.Trim())
                            })
                    Next
                    tx.Commit()
                End Using
                AuditLogger.Log("CREATE", "lab", requestNo, $"Requested {selected.Count} lab test(s) for encounter #{encounterId} (for approval)")
                RequestsSubmitted = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not submit request: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
