Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Text
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Read-only view of an issued certificate, with a real Print button (System.Drawing.Printing).</summary>
    Public Class CertificateViewDialog
        Inherits Form

        ''' <summary>Header lines (letterhead + title) are shown/printed centered; everything else is one flowing left-aligned body -- kept as two separate strings instead of one blob with hand-counted leading spaces baked in, which only lines up for one exact font/width and silently drifts the moment either changes.</summary>
        Private headerText As String = ""
        Private bodyText As String = ""
        Private txtPreview As RichTextBox
        Private btnPrint As Button
        Private lblNotice As Label
        Private ReadOnly documentId As Integer
        Private ReadOnly issueMode As Boolean
        Private referenceNo As String = ""
        ''' <summary>True once this dialog issued the certificate (issue mode, printed).</summary>
        Public IssuedNow As Boolean = False

        ''' <param name="issueMode">RHU Management issuing a 'For Approval' certificate: Print issues it then prints; Cancel saves nothing.</param>
        Public Sub New(documentId As Integer, Optional issueMode As Boolean = False)
            Me.documentId = documentId
            Me.issueMode = issueMode
            Me.Text = If(issueMode, "Issue Medical Certificate", "Medical Certificate")
            Me.Size = New Size(560, 620)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = New Font("Consolas", 9.5F)

            txtPreview = New RichTextBox With {
                .Location = New Point(20, 20), .Size = New Size(500, 480), .ReadOnly = True,
                .ScrollBars = RichTextBoxScrollBars.Vertical, .BackColor = Color.White, .BorderStyle = BorderStyle.FixedSingle
            }
            Me.Controls.Add(txtPreview)

            Dim btnClose As New Button With {.Text = If(issueMode, "Cancel", "Close"), .Size = New Size(100, 36), .Location = New Point(300, 520)}
            UiTheme.StyleSecondaryButton(btnClose)
            AddHandler btnClose.Click, Sub() Me.Close()
            Me.Controls.Add(btnClose)

            lblNotice = New Label With {.ForeColor = UiTheme.WarningAmber, .AutoSize = True, .MaximumSize = New Size(270, 0), .Location = New Point(20, 516)}
            Me.Controls.Add(lblNotice)

            btnPrint = New Button With {.Text = "Print", .Size = New Size(120, 36), .Location = New Point(410, 520)}
            UiTheme.StylePrimaryButton(btnPrint)
            AddHandler btnPrint.Click, AddressOf BtnPrint_Click
            Me.Controls.Add(btnPrint)

            LoadCertificate()
        End Sub

        Private Sub LoadCertificate()
            Try
                Dim table = Db.Query(
                    "SELECT doc.reference_no, doc.purpose, doc.remarks, COALESCE(doc.approved_at, doc.created_at) AS issued_at, doc.status,
                            CONCAT(p.last_name, ', ', p.first_name, ' ', COALESCE(p.middle_name, '')) AS patient_name,
                            p.date_of_birth, p.sex, p.address, b.barangay_name,
                            TIMESTAMPDIFF(YEAR, p.date_of_birth, CURDATE()) AS age,
                            u.full_name AS issued_by, ua.full_name AS approved_by
                     FROM other_documents_services doc
                     JOIN encounters e ON e.encounter_id = doc.encounter_id
                     JOIN patients p ON p.patient_id = e.patient_id
                     JOIN barangays b ON b.barangay_id = p.barangay_id
                     LEFT JOIN users_staff u ON u.user_id = doc.created_by
                     LEFT JOIN users_staff ua ON ua.user_id = doc.approved_by
                     WHERE doc.document_id = @Id", New With {.Id = documentId})

                If table.Rows.Count = 0 Then
                    txtPreview.Text = "Certificate not found."
                    Return
                End If

                Dim r = table.Rows(0)

                ' Only certificates issued by RHU Management ("Approved" = rows from before the Issue wording) may be printed,
                ' except in issue mode, where printing is what issues it.
                Dim status = If(IsDBNull(r("status")), "", r("status").ToString())
                referenceNo = r("reference_no").ToString()
                btnPrint.Enabled = True : lblNotice.Text = ""
                If issueMode AndAlso status = "For Approval" Then
                    lblNotice.ForeColor = UiTheme.TextMuted
                    lblNotice.Text = "Review the certificate. Print issues it; Cancel leaves it For Approval."
                ElseIf status <> "Approved" AndAlso status <> "Issued" Then
                    btnPrint.Enabled = False
                    lblNotice.Text = If(status = "Rejected", "This certificate request was rejected.", "Awaiting issuance by RHU Management -- printing is disabled.")
                End If

                Dim hb As New StringBuilder()
                hb.AppendLine("RURAL HEALTH UNIT - VINZONS, CAMARINES NORTE")
                hb.AppendLine("MEDICAL CERTIFICATE")
                headerText = hb.ToString().TrimEnd(vbCr, vbLf)

                Dim sb As New StringBuilder()
                sb.AppendLine($"Certificate No.: {r("reference_no")}")
                sb.AppendLine($"Date Issued:     {Convert.ToDateTime(r("issued_at")):MMMM dd, yyyy}")
                sb.AppendLine()
                sb.AppendLine("TO WHOM IT MAY CONCERN:")
                sb.AppendLine()
                sb.AppendLine($"This is to certify that {r("patient_name").ToString().Trim()}, {r("age")} years")
                sb.AppendLine($"old, {r("sex")}, residing at {r("address")}, {r("barangay_name")},")
                sb.AppendLine($"Vinzons, Camarines Norte was examined/consulted at this Rural")
                sb.AppendLine("Health Unit.")
                sb.AppendLine()
                sb.AppendLine($"Purpose: {r("purpose")}")
                sb.AppendLine()
                If Not IsDBNull(r("remarks")) AndAlso r("remarks").ToString().Trim().Length > 0 Then
                    sb.AppendLine("Findings/Remarks:")
                    sb.AppendLine(r("remarks").ToString())
                    sb.AppendLine()
                End If
                sb.AppendLine("This certificate is issued upon the request of the above-named")
                sb.AppendLine("person for whatever legal purpose it may serve.")
                sb.AppendLine()
                sb.AppendLine()
                ' created_by prepared the request; approved_by is the RHU Management user who issued it.
                sb.AppendLine($"Prepared by: {If(IsDBNull(r("issued_by")), "-", r("issued_by"))}")
                If Not IsDBNull(r("approved_by")) Then sb.AppendLine($"Issued by:   {r("approved_by")}")
                sb.AppendLine("Rural Health Unit, Vinzons, Camarines Norte")
                sb.AppendLine()
                sb.AppendLine("_______________________________")
                sb.AppendLine("Signature over Printed Name")
                bodyText = sb.ToString()

                RenderPreview()
            Catch ex As Exception
                txtPreview.Text = "Error loading certificate: " & ex.Message
            End Try
        End Sub

        ''' <summary>
        ''' Centers the letterhead/title via RichTextBox paragraph alignment
        ''' instead of hand-counted leading spaces -- that trick only lines up
        ''' for one exact font/box width and silently drifts if either ever
        ''' changes, which is exactly the "layout looks off" complaint this
        ''' replaces.
        ''' </summary>
        Private Sub RenderPreview()
            txtPreview.Clear()
            txtPreview.SelectionAlignment = HorizontalAlignment.Center
            txtPreview.SelectionFont = New Font(txtPreview.Font, FontStyle.Bold)
            txtPreview.AppendText(headerText & vbCrLf)
            txtPreview.AppendText(New String("-"c, 50) & vbCrLf & vbCrLf)

            txtPreview.SelectionStart = txtPreview.TextLength
            txtPreview.SelectionAlignment = HorizontalAlignment.Left
            txtPreview.SelectionFont = New Font(txtPreview.Font, FontStyle.Regular)
            txtPreview.AppendText(bodyText)
            txtPreview.SelectionStart = 0
        End Sub

        Private Sub BtnPrint_Click(sender As Object, e As EventArgs)
            Try
                Using pd As New PrintDocument()
                    AddHandler pd.PrintPage, AddressOf PrintPage
                    Using dlg As New PrintDialog With {.Document = pd}
                        If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                        If issueMode AndAlso Not IssuedNow AndAlso Not IssueCertificate() Then Return
                        pd.Print()
                    End Using
                End Using
            Catch ex As Exception
                MessageBox.Show("Could not print: " & ex.Message & vbCrLf & "(No printer configured? You can still copy the text above.)", "Print Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        ''' <summary>Marks the certificate Issued by the current user, then reloads the text so the printout shows who issued it.</summary>
        Private Function IssueCertificate() As Boolean
            Try
                Dim n = Db.Execute(
                    "UPDATE other_documents_services SET status = 'Issued', approved_by = @UserId, approved_at = NOW(), updated_at = NOW()
                     WHERE document_id = @Id AND status = 'For Approval'",
                    New With {.UserId = Session.Current.UserId, .Id = documentId})
                If n = 0 Then
                    MessageBox.Show("This certificate is no longer waiting for issuance (someone else may have issued it).", "Issue", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    LoadCertificate()
                    Return False
                End If
                AuditLogger.Log("UPDATE", "medcerts", referenceNo, "Issued medical certificate")
                IssuedNow = True
                Me.Text = "Medical Certificate"
                LoadCertificate()
                Return True
            Catch ex As Exception
                UiTheme.ShowError(Me, "issue certificate", ex)
                Return False
            End Try
        End Function

        ''' <summary>Mirrors RenderPreview's centered-header / left-aligned-body split, but computed against the actual printed font/page width via MeasureString rather than assumed on-screen character counts.</summary>
        Private Sub PrintPage(sender As Object, e As PrintPageEventArgs)
            Using headerFont As New Font("Consolas", 12, FontStyle.Bold)
                Using bodyFont As New Font("Consolas", 10)
                    Dim y = e.MarginBounds.Top
                    Dim center As New StringFormat With {.Alignment = StringAlignment.Center}
                    For Each line In headerText.Split({vbCrLf, vbLf}, StringSplitOptions.None)
                        e.Graphics.DrawString(line, headerFont, Brushes.Black, New RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, headerFont.Height), center)
                        y += headerFont.Height
                    Next
                    Dim ruleWidth = e.Graphics.MeasureString(New String("-"c, 50), bodyFont).Width
                    e.Graphics.DrawString(New String("-"c, 50), bodyFont, Brushes.Black, e.MarginBounds.Left + (e.MarginBounds.Width - ruleWidth) / 2, y)
                    y += bodyFont.Height * 2

                    e.Graphics.DrawString(bodyText, bodyFont, Brushes.Black, New RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, e.MarginBounds.Bottom - y))
                End Using
            End Using
        End Sub

    End Class

End Namespace
