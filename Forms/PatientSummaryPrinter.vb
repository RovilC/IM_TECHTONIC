Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>
    ''' Printable patient record summary (from PatientSummaryData):
    ''' letterhead + patient information, then every history section as a
    ''' table, paginated across as many pages as needed with each table's
    ''' column header repeated at the top of a continued page.
    ''' </summary>
    Public Class PatientSummaryPrinter

        Private ReadOnly info As List(Of (Label As String, Value As String))
        Private ReadOnly sections As List(Of (Title As String, Table As DataTable))
        Private ReadOnly patientName As String

        ' pagination state -- reset in BeginPrint because the preview renders the document
        ' once and printing from the preview renders it again
        Private sectionIndex As Integer
        Private rowIndex As Integer
        Private pageNo As Integer

        Private Sub New(info As List(Of (String, String)), sections As List(Of (String, DataTable)), patientName As String)
            Me.info = info
            Me.sections = sections
            Me.patientName = patientName
        End Sub

        Public Shared Sub ShowPreview(owner As IWin32Window, patientId As Integer)
            Try
                Dim r = PatientSummaryData.LoadPatient(patientId)
                If r Is Nothing Then
                    MessageBox.Show("Patient not found.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                Dim printer As New PatientSummaryPrinter(PatientSummaryData.PatientInfoFields(r), PatientSummaryData.LoadSections(patientId),
                                                         $"{r("last_name")}, {r("first_name")} ({r("patient_no")})")
                Using pd As New PrintDocument()
                    pd.DocumentName = "Patient Summary " & r("patient_no").ToString()
                    pd.DefaultPageSettings.Margins = New Margins(45, 45, 45, 45)
                    AddHandler pd.BeginPrint, Sub() printer.ResetState()
                    AddHandler pd.PrintPage, AddressOf printer.PrintPage
                    Using preview As New PrintPreviewDialog With {.Document = pd, .Width = 900, .Height = 1000, .StartPosition = FormStartPosition.CenterParent}
                        preview.PrintPreviewControl.Zoom = 1.0
                        preview.ShowDialog(owner)
                    End Using
                End Using
                AuditLogger.Log("VIEW", "patients", r("patient_no").ToString(), "Printed/previewed patient record summary")
            Catch ex As Exception
                MessageBox.Show("Could not prepare the patient summary: " & ex.Message, "Print Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Sub ResetState()
            sectionIndex = -1   ' -1 = patient information block not printed yet
            rowIndex = 0
            pageNo = 0
        End Sub

        Private Shared Function CellText(value As Object) As String
            If value Is Nothing OrElse IsDBNull(value) Then Return ""
            If TypeOf value Is DateTime Then
                Dim d = CDate(value)
                Return If(d.TimeOfDay = TimeSpan.Zero, d.ToString("MMM dd, yyyy"), d.ToString("MMM dd, yyyy h:mm tt"))
            End If
            Return value.ToString().Replace(vbCrLf, " ").Replace(vbLf, " ").Trim()
        End Function

        Private Shared Function VisibleColumns(table As DataTable) As List(Of DataColumn)
            Return table.Columns.Cast(Of DataColumn)().Where(Function(c) c.ColumnName <> "Id").ToList()
        End Function

        ''' <summary>Column widths proportional to typical content length (header vs. average cell), clamped so no column starves or hogs the page.</summary>
        Private Shared Function ColumnWidths(table As DataTable, cols As List(Of DataColumn), totalWidth As Single) As Single()
            Dim weights = cols.Select(
                Function(c)
                    Dim avg = If(table.Rows.Count = 0, 0, table.Rows.Cast(Of DataRow)().Take(50).Average(Function(r) CellText(r(c)).Length))
                    Return Math.Min(Math.Max(Math.Max(c.ColumnName.Length, avg), 6), 40)
                End Function).ToArray()
            Dim sum = weights.Sum()
            Return weights.Select(Function(w) CSng(totalWidth * w / sum)).ToArray()
        End Function

        Private Sub PrintPage(sender As Object, e As PrintPageEventArgs)
            pageNo += 1
            Dim g = e.Graphics
            Dim left As Single = e.MarginBounds.Left
            Dim width As Single = e.MarginBounds.Width
            Dim bottom As Single = e.MarginBounds.Bottom - 18
            Dim y As Single = e.MarginBounds.Top

            Using fSmall As New Font("Segoe UI", 8.0F),
                  fCell As New Font("Segoe UI", 8.0F),
                  fCellBold As New Font("Segoe UI", 8.0F, FontStyle.Bold),
                  fBody As New Font("Segoe UI", 9.0F),
                  fBold As New Font("Segoe UI", 9.0F, FontStyle.Bold),
                  fSection As New Font("Segoe UI", 10.5F, FontStyle.Bold),
                  fOrg As New Font("Segoe UI", 12.0F, FontStyle.Bold),
                  fTitle As New Font("Segoe UI", 15.0F, FontStyle.Bold),
                  green As New SolidBrush(UiTheme.PrimaryGreen),
                  headerFill As New SolidBrush(Color.FromArgb(225, 240, 230)),
                  thick As New Pen(UiTheme.PrimaryGreen, 2),
                  grid As New Pen(Color.FromArgb(190, 190, 190))

                Dim center As New StringFormat With {.Alignment = StringAlignment.Center}

                If pageNo = 1 Then
                    g.DrawString("Republic of the Philippines  •  Province of Camarines Norte", fSmall, Brushes.Black, New RectangleF(left, y, width, 14), center) : y += 15
                    g.DrawString("RURAL HEALTH UNIT - VINZONS, CAMARINES NORTE", fOrg, Brushes.Black, New RectangleF(left, y, width, 22), center) : y += 24
                    g.DrawString("PATIENT RECORD SUMMARY", fTitle, green, New RectangleF(left, y, width, 28), center) : y += 32
                Else
                    g.DrawString($"Patient Record Summary - {patientName} (continued)", fBold, Brushes.Black, left, y) : y += 20
                End If
                g.DrawLine(thick, left, y, left + width, y) : y += 10

                ' ---- patient information (two label/value columns) ----
                If sectionIndex = -1 Then
                    g.DrawString("Patient Information", fSection, green, left, y) : y += 22
                    Dim labelW As Single = 120
                    For Each f In info
                        Dim h = Math.Max(g.MeasureString(f.Value, fBody, CInt(width - labelW)).Height, fBody.Height)
                        g.DrawString(f.Label & ":", fBold, Brushes.Black, left, y)
                        g.DrawString(f.Value, fBody, Brushes.Black, New RectangleF(left + labelW, y, width - labelW, h))
                        y += h + 2
                    Next
                    y += 12
                    sectionIndex = 0
                    rowIndex = 0
                End If

                ' ---- history sections ----
                While sectionIndex < sections.Count
                    Dim sec = sections(sectionIndex)
                    Dim cols = VisibleColumns(sec.Table)
                    Dim widths = ColumnWidths(sec.Table, cols, width)

                    ' need room for the title + column header + at least one row, else start a new page
                    If y + 70 > bottom Then
                        e.HasMorePages = True
                        Exit While
                    End If

                    Dim title = sec.Title & If(rowIndex > 0, " (continued)", $"  ({sec.Table.Rows.Count})")
                    g.DrawString(title, fSection, green, left, y) : y += 20

                    If sec.Table.Rows.Count = 0 Then
                        g.DrawString("No records.", fBody, Brushes.Gray, left + 4, y) : y += 26
                        sectionIndex += 1 : rowIndex = 0
                        Continue While
                    End If

                    ' column header
                    Dim headH = cols.Select(Function(c, i) g.MeasureString(c.ColumnName, fCellBold, CInt(widths(i) - 6)).Height).Max() + 4
                    g.FillRectangle(headerFill, left, y, width, headH)
                    Dim x = left
                    For i = 0 To cols.Count - 1
                        g.DrawString(cols(i).ColumnName, fCellBold, Brushes.Black, New RectangleF(x + 3, y + 2, widths(i) - 6, headH))
                        g.DrawRectangle(grid, x, y, widths(i), headH)
                        x += widths(i)
                    Next
                    y += headH

                    Dim pageFull = False
                    While rowIndex < sec.Table.Rows.Count
                        Dim row = sec.Table.Rows(rowIndex)
                        Dim texts = cols.Select(Function(c) CellText(row(c))).ToList()
                        Dim rowH = texts.Select(Function(t, i) g.MeasureString(t, fCell, CInt(widths(i) - 6)).Height).Max() + 4
                        If y + rowH > bottom Then
                            pageFull = True
                            Exit While
                        End If
                        x = left
                        For i = 0 To cols.Count - 1
                            g.DrawString(texts(i), fCell, Brushes.Black, New RectangleF(x + 3, y + 2, widths(i) - 6, rowH))
                            g.DrawRectangle(grid, x, y, widths(i), rowH)
                            x += widths(i)
                        Next
                        y += rowH
                        rowIndex += 1
                    End While

                    If pageFull Then
                        e.HasMorePages = True
                        Exit While
                    End If
                    y += 16
                    sectionIndex += 1 : rowIndex = 0
                End While

                g.DrawString($"Printed {DateTime.Now:MMM dd, yyyy h:mm tt}  •  Page {pageNo}  •  CONFIDENTIAL PATIENT INFORMATION", fSmall, Brushes.Gray,
                             New RectangleF(left, e.MarginBounds.Bottom - 12, width, 14), center)
            End Using
        End Sub

    End Class

End Namespace
