Imports System.Linq
Imports System.Windows.Forms
Imports QuestPDF.Fluent
Imports QuestPDF.Helpers
Imports QuestPDF.Infrastructure

Namespace Data

    ''' <summary>
    ''' Renders one or more DataTables into a printable PDF (RHU letterhead +
    ''' report title + generated-date, one section per table) via QuestPDF.
    ''' Community license (free for this kind of use) is set once at
    ''' startup in ApplicationEvents.vb.
    ''' </summary>
    Public Module PdfExporter

        Public Sub ExportToPdf(reportTitle As String, sections As List(Of (Label As String, Table As DataTable)), suggestedFileName As String)
            If sections Is Nothing OrElse sections.Count = 0 OrElse sections.All(Function(s) s.Table Is Nothing OrElse s.Table.Rows.Count = 0) Then
                MessageBox.Show("There is nothing to export.", "Nothing to Export", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using sfd As New SaveFileDialog With {.Filter = "PDF file (*.pdf)|*.pdf", .FileName = suggestedFileName, .Title = "Export Report as PDF"}
                If sfd.ShowDialog() <> DialogResult.OK Then Return

                Try
                    Dim generatedAt = DateTime.Now
                    Document.Create(
                        Sub(container)
                            container.Page(
                                Sub(page)
                                    page.Size(PageSizes.A4.Landscape())
                                    page.Margin(28)
                                    page.DefaultTextStyle(Function(t) t.FontSize(9))

                                    page.Header().Column(
                                        Sub(col)
                                            col.Item().Text("RURAL HEALTH UNIT - VINZONS, CAMARINES NORTE").FontSize(11).Bold()
                                            col.Item().Text(reportTitle).FontSize(15).Bold()
                                            col.Item().PaddingTop(2).Text($"Generated: {generatedAt:MMMM dd, yyyy hh:mm tt}").FontSize(8).FontColor(Colors.Grey.Darken1)
                                            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1)
                                        End Sub)

                                    page.Content().PaddingTop(10).Column(
                                        Sub(col)
                                            col.Spacing(14)
                                            For Each section In sections
                                                If section.Table Is Nothing OrElse section.Table.Rows.Count = 0 Then Continue For
                                                col.Item().Element(Sub(e) RenderSection(e, section.Label, section.Table))
                                            Next
                                        End Sub)

                                    page.Footer().AlignCenter().Text(
                                        Sub(t)
                                            t.DefaultTextStyle(Function(s) s.FontSize(8).FontColor(Colors.Grey.Darken1))
                                            t.CurrentPageNumber()
                                            t.Span(" / ")
                                            t.TotalPages()
                                        End Sub)
                                End Sub)
                        End Sub).GeneratePdf(sfd.FileName)

                    MessageBox.Show("Report exported.", "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Could not export: " & ex.Message, "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Sub

        Private Sub RenderSection(container As IContainer, label As String, table As DataTable)
            container.Column(
                Sub(col)
                    If Not String.IsNullOrWhiteSpace(label) Then
                        col.Item().PaddingBottom(4).Text(label).FontSize(11).Bold()
                    End If
                    col.Item().Element(Sub(e) RenderTable(e, table))
                End Sub)
        End Sub

        Private Sub RenderTable(container As IContainer, table As DataTable)
            container.Table(
                Sub(t)
                    t.ColumnsDefinition(
                        Sub(columns)
                            For Each c As DataColumn In table.Columns
                                columns.RelativeColumn()
                            Next
                        End Sub)

                    t.Header(
                        Sub(header)
                            For Each c As DataColumn In table.Columns
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(c.ColumnName).Bold().FontSize(8.5F)
                            Next
                            header.Cell().ColumnSpan(CUInt(table.Columns.Count)).PaddingTop(1).LineHorizontal(0.75F).LineColor(Colors.Grey.Darken1)
                        End Sub)

                    For Each row As DataRow In table.Rows
                        For Each c As DataColumn In table.Columns
                            Dim value = row(c)
                            Dim text = If(value Is DBNull.Value OrElse value Is Nothing, "-", FormatCell(value))
                            t.Cell().BorderBottom(0.5F).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(text).FontSize(8)
                        Next
                    Next
                End Sub)
        End Sub

        Private Function FormatCell(value As Object) As String
            If TypeOf value Is DateTime Then
                Dim dt = CType(value, DateTime)
                Return If(dt.TimeOfDay = TimeSpan.Zero, dt.ToString("MMM dd, yyyy"), dt.ToString("MMM dd, yyyy hh:mm tt"))
            End If
            Return value.ToString()
        End Function

    End Module

End Namespace
