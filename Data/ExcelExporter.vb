Imports System.Linq
Imports System.Windows.Forms
Imports ClosedXML.Excel

Namespace Data

    ''' <summary>Writes one or more DataTables to an .xlsx workbook (one worksheet per section) via ClosedXML.</summary>
    Public Module ExcelExporter

        Public Sub ExportToExcel(sections As List(Of (Label As String, Table As DataTable)), suggestedFileName As String)
            If sections Is Nothing OrElse sections.Count = 0 OrElse sections.All(Function(s) s.Table Is Nothing OrElse s.Table.Rows.Count = 0) Then
                MessageBox.Show("There is nothing to export.", "Nothing to Export", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using sfd As New SaveFileDialog With {.Filter = "Excel Workbook (*.xlsx)|*.xlsx", .FileName = suggestedFileName, .Title = "Export Report as Excel"}
                If sfd.ShowDialog() <> DialogResult.OK Then Return

                Try
                    Using workbook As New XLWorkbook()
                        Dim usedNames As New HashSet(Of String)
                        For Each section In sections
                            If section.Table Is Nothing OrElse section.Table.Rows.Count = 0 Then Continue For
                            Dim sheetName = SafeSheetName(If(String.IsNullOrWhiteSpace(section.Label), "Report", section.Label), usedNames)
                            Dim sheet = workbook.Worksheets.Add(sheetName)

                            For colIndex = 0 To section.Table.Columns.Count - 1
                                Dim cell = sheet.Cell(1, colIndex + 1)
                                cell.Value = section.Table.Columns(colIndex).ColumnName
                                cell.Style.Font.Bold = True
                                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240)
                            Next

                            For rowIndex = 0 To section.Table.Rows.Count - 1
                                For colIndex = 0 To section.Table.Columns.Count - 1
                                    Dim value = section.Table.Rows(rowIndex)(colIndex)
                                    Dim cell = sheet.Cell(rowIndex + 2, colIndex + 1)
                                    SetCellValue(cell, value)
                                Next
                            Next

                            sheet.SheetView.FreezeRows(1)
                            sheet.Columns().AdjustToContents()
                        Next
                        workbook.SaveAs(sfd.FileName)
                    End Using
                    MessageBox.Show("Report exported.", "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Could not export: " & ex.Message, "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Sub

        Private Sub SetCellValue(cell As IXLCell, value As Object)
            If value Is Nothing OrElse value Is DBNull.Value Then
                cell.Value = ""
            ElseIf TypeOf value Is DateTime Then
                cell.Value = CType(value, DateTime)
                cell.Style.DateFormat.Format = "mmm dd, yyyy"
            ElseIf IsNumeric(value) Then
                cell.Value = Convert.ToDouble(value)
            Else
                cell.Value = value.ToString()
            End If
        End Sub

        ''' <summary>
        ''' Covers every numeric CLR type ADO.NET/MySqlConnector can hand back
        ''' for a DataRow value (a raw TINYINT surfaces as SByte/Byte, a
        ''' BIGINT as Int64, a DECIMAL column as Decimal, etc.) -- checking
        ''' only Integer/Long/Double/Decimal missed several of these, which
        ''' would have written a number into the sheet as text instead.
        ''' </summary>
        Private Function IsNumeric(value As Object) As Boolean
            Select Case Type.GetTypeCode(value.GetType())
                Case TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16,
                     TypeCode.Int32, TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64,
                     TypeCode.Single, TypeCode.Double, TypeCode.Decimal
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Function SafeSheetName(label As String, usedNames As HashSet(Of String)) As String
            Dim invalidChars = New Char() {"\"c, "/"c, "*"c, "["c, "]"c, ":"c, "?"c}
            Dim cleaned = New String(label.Where(Function(c) Not invalidChars.Contains(c)).ToArray())
            If cleaned.Length > 31 Then cleaned = cleaned.Substring(0, 31)
            If cleaned.Trim().Length = 0 Then cleaned = "Sheet"

            Dim candidate = cleaned
            Dim suffix = 2
            While usedNames.Contains(candidate.ToLowerInvariant())
                Dim tag = $" ({suffix})"
                candidate = cleaned.Substring(0, Math.Min(cleaned.Length, 31 - tag.Length)) & tag
                suffix += 1
            End While
            usedNames.Add(candidate.ToLowerInvariant())
            Return candidate
        End Function

    End Module

End Namespace
