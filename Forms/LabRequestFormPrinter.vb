Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' Printable laboratory request form for a request referred to an
    ''' external laboratory. Laid out like the healthcare staff's
    ''' LabRequestDialog (priority, specimen, tests by category as
    ''' checkboxes, clinical notes) so the receiving lab sees the same form,
    ''' with the selected options ticked.
    ''' </summary>
    Public Class LabRequestFormPrinter

        ''' <summary>
        ''' Groups the per-test `laboratory` rows of one LabRequestDialog submit. Rows created
        ''' before request_no existed fall back to encounter + request timestamp.
        ''' </summary>
        Public Const RequestKeySql As String = "COALESCE(l.request_no, CONCAT('ENC', l.encounter_id, '-', DATE_FORMAT(l.request_date, '%Y%m%d%H%i%s')))"

        Private header As DataRow
        Private ReadOnly selectedTests As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly uncataloguedTests As New List(Of String)

        Private Sub New()
        End Sub

        ''' <summary>Opens a print preview of the request form; the preview's toolbar prints it.</summary>
        Public Shared Sub ShowPreview(owner As IWin32Window, requestKey As String)
            Dim printer As New LabRequestFormPrinter()
            Try
                If Not printer.LoadRequest(requestKey) Then
                    MessageBox.Show("Lab request not found.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                Using pd As New PrintDocument()
                    pd.DocumentName = "Laboratory Request " & printer.header("request_key").ToString()
                    pd.DefaultPageSettings.Margins = New Margins(50, 50, 50, 50)
                    AddHandler pd.PrintPage, AddressOf printer.PrintPage
                    Using preview As New PrintPreviewDialog With {.Document = pd, .Width = 900, .Height = 1000, .StartPosition = FormStartPosition.CenterParent}
                        preview.PrintPreviewControl.Zoom = 1.0
                        preview.ShowDialog(owner)
                    End Using
                End Using
            Catch ex As Exception
                MessageBox.Show("Could not prepare the lab request form: " & ex.Message, "Print Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        ''' <summary>
        ''' Shows the request form as it will print -- with the not-yet-saved facility, remarks and
        ''' approver filled in -- before a referral is recorded. "Refer &amp; Print" opens the printer
        ''' dialog; only when the user picks a printer is <paramref name="confirmReferral"/> run (it
        ''' saves the referral and returns False on failure) and the form printed. Cancel saves nothing.
        ''' Returns True when the referral was saved.
        ''' </summary>
        Public Shared Function ShowReferralOverview(owner As IWin32Window, requestKey As String, facility As String, remarks As String,
                                                   confirmReferral As Func(Of Boolean)) As Boolean
            Dim printer As New LabRequestFormPrinter()
            Dim referred = False
            Try
                If Not printer.LoadRequest(requestKey) Then
                    MessageBox.Show("Lab request not found.", "Refer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return False
                End If
                printer.SetHeader("external_facility", facility)
                printer.SetHeader("approval_remarks", If(remarks.Length = 0, CType(DBNull.Value, Object), remarks))
                printer.SetHeader("approved_by", Session.Current.FullName)
                printer.SetHeader("approved_at", DateTime.Now)

                Using pd As New PrintDocument(),
                      dlg As New Form With {.Text = "Referral Form Overview", .Width = 900, .Height = 1000, .StartPosition = FormStartPosition.CenterParent,
                                            .MinimizeBox = False, .Font = UiTheme.BaseFont}
                    pd.DocumentName = "Laboratory Request " & printer.header("request_key").ToString()
                    pd.DefaultPageSettings.Margins = New Margins(50, 50, 50, 50)
                    AddHandler pd.PrintPage, AddressOf printer.PrintPage

                    Dim bar As New Panel With {.Dock = DockStyle.Bottom, .Height = 56, .BackColor = Color.White}
                    Dim note As New Label With {.Text = "Review the form. Refer & Print records the referral and prints it; Cancel saves nothing.",
                                                .AutoSize = True, .ForeColor = UiTheme.TextMuted, .Location = New Point(12, 20), .UseMnemonic = False}
                    Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36)}
                    UiTheme.StyleSecondaryButton(btnCancel)
                    AddHandler btnCancel.Click, Sub() dlg.Close()
                    Dim btnPrint As New Button With {.Text = "Refer && Print", .Size = New Size(140, 36)}
                    UiTheme.StylePrimaryButton(btnPrint)
                    AddHandler btnPrint.Click,
                        Sub()
                            Try
                                Using pdlg As New PrintDialog With {.Document = pd}
                                    If pdlg.ShowDialog(dlg) <> DialogResult.OK Then Return
                                End Using
                                If Not confirmReferral() Then Return
                                referred = True
                                pd.Print()
                                dlg.Close()
                            Catch ex As Exception
                                MessageBox.Show(dlg, "Could not print: " & ex.Message &
                                                If(referred, vbCrLf & "The referral was saved; double-click it in the list to print again.", ""),
                                                "Print Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                If referred Then dlg.Close()
                            End Try
                        End Sub
                    bar.Controls.AddRange({note, btnCancel, btnPrint})
                    UiTheme.PinRowToRight(bar, 10, 12, 8, btnPrint, btnCancel)

                    Dim previewControl As New PrintPreviewControl With {.Dock = DockStyle.Fill, .Document = pd, .Zoom = 1.0}
                    dlg.Controls.Add(previewControl)
                    dlg.Controls.Add(bar)
                    dlg.CancelButton = btnCancel
                    dlg.ShowDialog(owner)
                End Using
            Catch ex As Exception
                MessageBox.Show("Could not prepare the lab request form: " & ex.Message, "Refer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
            Return referred
        End Function

        Private Sub SetHeader(column As String, value As Object)
            header.Table.Columns(column).ReadOnly = False
            header(column) = value
        End Sub

        Private Function LoadRequest(requestKey As String) As Boolean
            Dim table = Db.Query(
                $"SELECT {RequestKeySql} AS request_key, l.test_name, l.request_date, l.priority_level, l.specimen_type, l.clinical_notes,
                         l.external_facility, l.approval_remarks, l.approved_at,
                         CONCAT(p.last_name, ', ', p.first_name, ' ', COALESCE(p.middle_name, '')) AS patient_name,
                         p.patient_no, p.date_of_birth, p.sex, p.address, p.contact_number, b.barangay_name,
                         TIMESTAMPDIFF(YEAR, p.date_of_birth, CURDATE()) AS age, e.encounter_no,
                         ur.full_name AS requested_by, ua.full_name AS approved_by
                  FROM laboratory l
                  JOIN encounters e ON e.encounter_id = l.encounter_id
                  JOIN patients p ON p.patient_id = e.patient_id
                  LEFT JOIN barangays b ON b.barangay_id = p.barangay_id
                  LEFT JOIN users_staff ur ON ur.user_id = l.requested_by
                  LEFT JOIN users_staff ua ON ua.user_id = l.approved_by
                  WHERE {RequestKeySql} = @Key
                  ORDER BY l.laboratory_id",
                New With {.Key = requestKey})
            If table.Rows.Count = 0 Then Return False

            header = table.Rows(0)
            Dim catalogued = LabTestCatalog.Categories.SelectMany(Function(c) c.Tests).ToList()
            For Each row As DataRow In table.Rows
                Dim test = row("test_name").ToString()
                selectedTests.Add(test)
                If Not catalogued.Contains(test, StringComparer.OrdinalIgnoreCase) Then uncataloguedTests.Add(test)
            Next
            Return True
        End Function

        Private Shared Function Text(row As DataRow, column As String, Optional fallback As String = "") As String
            If IsDBNull(row(column)) Then Return fallback
            Dim s = row(column).ToString().Trim()
            Return If(s.Length = 0, fallback, s)
        End Function

        Private Sub PrintPage(sender As Object, e As PrintPageEventArgs)
            Dim g = e.Graphics
            Dim left As Single = e.MarginBounds.Left
            Dim width As Single = e.MarginBounds.Width
            Dim right As Single = left + width
            Dim y As Single = e.MarginBounds.Top

            Using fSmall As New Font("Segoe UI", 8.5F),
                  fBody As New Font("Segoe UI", 9.5F),
                  fBold As New Font("Segoe UI", 9.5F, FontStyle.Bold),
                  fSection As New Font("Segoe UI", 10.5F, FontStyle.Bold),
                  fOrg As New Font("Segoe UI", 12.0F, FontStyle.Bold),
                  fTitle As New Font("Segoe UI", 15.0F, FontStyle.Bold),
                  fSub As New Font("Segoe UI", 9.5F, FontStyle.Italic),
                  green As New SolidBrush(UiTheme.PrimaryGreen),
                  pen As New Pen(Color.Black, 1),
                  thick As New Pen(UiTheme.PrimaryGreen, 2)

                Dim center As New StringFormat With {.Alignment = StringAlignment.Center}

                ' ---- letterhead ----
                g.DrawString("Republic of the Philippines  •  Province of Camarines Norte", fSmall, Brushes.Black, New RectangleF(left, y, width, 16), center) : y += 16
                g.DrawString("RURAL HEALTH UNIT - VINZONS, CAMARINES NORTE", fOrg, Brushes.Black, New RectangleF(left, y, width, 22), center) : y += 26
                g.DrawString("LABORATORY REQUEST FORM", fTitle, green, New RectangleF(left, y, width, 28), center) : y += 28
                g.DrawString("Referral to External Laboratory", fSub, Brushes.Black, New RectangleF(left, y, width, 18), center) : y += 24
                g.DrawLine(thick, left, y, right, y) : y += 10

                ' ---- request + patient details (two columns) ----
                Dim half = width / 2
                Dim requestDate = Convert.ToDateTime(header("request_date"))
                Dim dob = If(IsDBNull(header("date_of_birth")), "", Convert.ToDateTime(header("date_of_birth")).ToString("MMM dd, yyyy"))
                Dim address = String.Join(", ", {Text(header, "address"), Text(header, "barangay_name"), "Vinzons, Camarines Norte"}.Where(Function(s) s.Length > 0))

                Dim fieldRows = New(String, String, String, String)() {
                    ("Request No.:", Text(header, "request_key"), "Date Requested:", requestDate.ToString("MMMM dd, yyyy  h:mm tt")),
                    ("Patient Name:", Text(header, "patient_name"), "Patient No.:", Text(header, "patient_no")),
                    ("Age / Sex:", $"{Text(header, "age", "-")} / {Text(header, "sex", "-")}", "Date of Birth:", dob),
                    ("Encounter No.:", Text(header, "encounter_no"), "Contact No.:", Text(header, "contact_number", "-"))
                }
                For Each f In fieldRows
                    DrawField(g, f.Item1, f.Item2, left, y, half - 10, fBold, fBody)
                    DrawField(g, f.Item3, f.Item4, left + half, y, half, fBold, fBody)
                    y += 20
                Next
                DrawField(g, "Address:", address, left, y, width, fBold, fBody) : y += 26

                ' ---- refer to ----
                Dim referBox As New RectangleF(left, y, width, 30)
                g.FillRectangle(New SolidBrush(Color.FromArgb(235, 245, 238)), referBox)
                g.DrawRectangle(pen, referBox.X, referBox.Y, referBox.Width, referBox.Height)
                g.DrawString("Referred To:", fBold, Brushes.Black, left + 8, y + 7)
                g.DrawString(Text(header, "external_facility", "________________________________"), fSection, Brushes.Black, left + 100, y + 6)
                y += 42

                ' ---- priority + specimen (same choices as the request screen) ----
                g.DrawString("Priority Level", fSection, green, left, y) : y += 20
                Dim x = left + 6
                For Each p In LabTestCatalog.PriorityLevels
                    x += DrawCheckbox(g, p, x, y, Text(header, "priority_level") = p, fBody) + 24
                Next
                y += 26

                g.DrawString("Specimen Type", fSection, green, left, y) : y += 20
                x = left + 6
                For Each s In LabTestCatalog.SpecimenTypes
                    Dim w = g.MeasureString(s, fBody).Width + 20
                    If x + w > right Then x = left + 6 : y += 20
                    x += DrawCheckbox(g, s, x, y, Text(header, "specimen_type") = s, fBody) + 18
                Next
                y += 30

                ' ---- tests by category, two columns, in a bordered panel ----
                g.DrawString($"Laboratory Tests  ({selectedTests.Count} selected)", fSection, green, left, y) : y += 22
                Dim panelTop = y
                Dim colY = {panelTop + 10, panelTop + 10}
                Dim colX = {left + 12, left + half + 12}
                Dim categories = LabTestCatalog.Categories.Select(Function(c) (c.Category, Tests:=c.Tests.ToList())).ToList()
                If uncataloguedTests.Count > 0 Then categories.Add(("Other Requested Tests", uncataloguedTests))
                For Each cat In categories
                    Dim col = If(colY(0) <= colY(1), 0, 1)
                    g.DrawString(cat.Category, fBold, green, colX(col), colY(col))
                    colY(col) += 20
                    For Each test In cat.Tests
                        DrawCheckbox(g, test, colX(col) + 12, colY(col), selectedTests.Contains(test), fBody)
                        colY(col) += 19
                    Next
                    colY(col) += 8
                Next
                y = Math.Max(colY(0), colY(1)) + 4
                g.DrawRectangle(pen, left, panelTop, width, y - panelTop)
                y += 14

                ' ---- clinical notes / remarks ----
                g.DrawString("Clinical Notes", fSection, green, left, y) : y += 20
                Dim notesBox As New RectangleF(left, y, width, 70)
                g.DrawRectangle(pen, notesBox.X, notesBox.Y, notesBox.Width, notesBox.Height)
                g.DrawString(Text(header, "clinical_notes"), fBody, Brushes.Black, New RectangleF(left + 6, y + 5, width - 12, 62))
                y += 80
                Dim remarks = Text(header, "approval_remarks")
                If remarks.Length > 0 Then
                    DrawField(g, "Remarks:", remarks, left, y, width, fBold, fBody) : y += 24
                End If

                ' ---- signatures ----
                Dim sigY = Math.Max(y + 40, e.MarginBounds.Bottom - 80)
                Dim sigW = half - 40
                DrawSignature(g, Text(header, "requested_by"), "Requesting Physician", left, sigY, sigW, fBold, fSmall, pen)
                Dim approvedOn = If(IsDBNull(header("approved_at")), "", " on " & Convert.ToDateTime(header("approved_at")).ToString("MMM dd, yyyy"))
                DrawSignature(g, Text(header, "approved_by"), "Issued by (RHU Management)" & approvedOn, left + half + 40, sigY, sigW, fBold, fSmall, pen)

                g.DrawString($"Printed {DateTime.Now:MMM dd, yyyy h:mm tt}  •  Project C.A.R.E", fSmall, Brushes.Gray, New RectangleF(left, e.MarginBounds.Bottom - 14, width, 14), center)
            End Using
        End Sub

        Private Shared Sub DrawField(g As Graphics, label As String, value As String, x As Single, y As Single, width As Single, fLabel As Font, fValue As Font)
            Dim lw = g.MeasureString(label, fLabel).Width
            g.DrawString(label, fLabel, Brushes.Black, x, y)
            g.DrawString(value, fValue, Brushes.Black, New RectangleF(x + lw + 2, y, Math.Max(width - lw - 2, 10), 18))
        End Sub

        ''' <summary>Draws a ticked/unticked box with its label; returns the total width used.</summary>
        Private Shared Function DrawCheckbox(g As Graphics, label As String, x As Single, y As Single, checked As Boolean, font As Font) As Single
            Const box As Single = 12
            g.DrawRectangle(Pens.Black, x, y + 2, box, box)
            If checked Then
                Using p As New Pen(Color.Black, 2)
                    g.DrawLines(p, {New PointF(x + 2.5F, y + 8), New PointF(x + 5, y + 11.5F), New PointF(x + 10, y + 4)})
                End Using
            End If
            Dim f = If(checked, New Font(font, FontStyle.Bold), font)
            g.DrawString(label, f, Brushes.Black, x + box + 4, y)
            Dim w = box + 4 + g.MeasureString(label, f).Width
            If checked Then f.Dispose()
            Return w
        End Function

        Private Shared Sub DrawSignature(g As Graphics, name As String, caption As String, x As Single, y As Single, width As Single, fName As Font, fCaption As Font, pen As Pen)
            Dim center As New StringFormat With {.Alignment = StringAlignment.Center}
            g.DrawString(name, fName, Brushes.Black, New RectangleF(x, y, width, 18), center)
            g.DrawLine(pen, x, y + 20, x + width, y + 20)
            g.DrawString(caption, fCaption, Brushes.Black, New RectangleF(x, y + 23, width, 16), center)
        End Sub

    End Class

End Namespace
