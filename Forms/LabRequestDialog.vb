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

            Dim card = ClinicalUi.NewDialogCard(Me, 612)
            Const x As Integer = 24
            Const w As Integer = 564
            Const colW As Integer = 275

            card.Controls.Add(New Label With {.Text = "Laboratory Request", .Font = New Font("Segoe UI Semibold", 11.5F), .ForeColor = ClinicalUi.TextMain, .AutoSize = True, .Location = New Point(x, 20)})
            card.Controls.Add(New Label With {.Text = patientDisplayName, .ForeColor = ClinicalUi.TextSoft, .AutoSize = True, .UseMnemonic = False, .Location = New Point(x, 50)})

            ClinicalUi.AddLabelAt(card, "Priority Level", x, 90)
            cboPriority = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboPriority.Items.AddRange(LabTestCatalog.PriorityLevels)
            cboPriority.SelectedIndex = 0
            ClinicalUi.PlaceCombo(card, cboPriority, x, 112, colW)

            ClinicalUi.AddLabelAt(card, "Specimen Type", x + colW + 14, 90)
            cboSpecimen = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
            cboSpecimen.Items.AddRange(LabTestCatalog.SpecimenTypes)
            cboSpecimen.SelectedIndex = 0
            ClinicalUi.PlaceCombo(card, cboSpecimen, x + colW + 14, 112, colW)

            ClinicalUi.AddLabelAt(card, "Select Laboratory Tests", x, 166)
            lblSelectedCount = New Label With {.Text = "0 selected", .ForeColor = ClinicalUi.TextSoft, .AutoSize = True, .Location = New Point(x + 200, 166)}
            card.Controls.Add(lblSelectedCount)

            ' Bordered host for the scrolling test list (categories + checkboxes are unchanged).
            Dim testsHost As New Panel With {.Location = New Point(x, 190), .Size = New Size(w, 250), .BackColor = Color.White, .Padding = New Padding(1)}
            AddHandler testsHost.Paint, Sub(s, e)
                                            Using pen As New Pen(ClinicalUi.InputBorder)
                                                e.Graphics.DrawRectangle(pen, 0, 0, testsHost.Width - 1, testsHost.Height - 1)
                                            End Using
                                        End Sub
            card.Controls.Add(testsHost)
            Dim testsPanel As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = Color.White}
            testsHost.Controls.Add(testsPanel)

            Dim y As Integer = 8
            For Each cat In LabTestCatalog.Categories
                testsPanel.Controls.Add(New Label With {.Text = cat.Category, .Font = New Font("Segoe UI Semibold", 9.5F), .ForeColor = ClinicalUi.Green, .AutoSize = True, .UseMnemonic = False, .Location = New Point(8, y)})
                y += 24
                For Each testName In cat.Tests
                    Dim chk As New CheckBox With {.Text = testName, .Tag = cat.Category, .AutoSize = True, .UseMnemonic = False, .ForeColor = ClinicalUi.TextMain, .Location = New Point(22, y)}
                    AddHandler chk.CheckedChanged, Sub() UpdateSelectedCount()
                    testsPanel.Controls.Add(chk)
                    checkBoxes.Add(chk)
                    y += 24
                Next
                y += 8
            Next

            ClinicalUi.AddLabelAt(card, "Clinical Notes", x, 458)
            txtNotes = New TextBox With {.Multiline = True}
            ClinicalUi.PlaceInput(card, txtNotes, x, 480, w, 72)

            lblError = New Label With {.ForeColor = ClinicalUi.DangerText, .AutoSize = True, .MaximumSize = New Size(w, 0), .Location = New Point(x, 562)}
            card.Controls.Add(lblError)

            ' Footer: divider, Cancel on the left, the primary action on the right.
            card.Controls.Add(New Panel With {.Location = New Point(x, 602), .Size = New Size(w, 1), .BackColor = ClinicalUi.CardBorder})

            Dim btnCancel As New Button With {.Text = "Cancel", .Location = New Point(x, 624)}
            ClinicalUi.StyleCancel(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            card.Controls.Add(btnCancel)

            Dim btnSubmit As New Button With {.Text = "Submit Request", .Size = New Size(160, 38)}
            ClinicalUi.StylePrimary(btnSubmit)
            btnSubmit.Location = New Point(x + w - 160, 620)
            AddHandler btnSubmit.Click, AddressOf BtnSubmit_Click
            card.Controls.Add(btnSubmit)

            card.Height = 620 + 38 + 22
            Me.ClientSize = New Size(644, card.Bottom + 16)
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
