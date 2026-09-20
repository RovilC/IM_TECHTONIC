Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Namespace Data

    ''' <summary>Exports a DataTable (or a DataGridView's bound data) to a CSV file the user picks via a Save dialog. This is the "View / Export Report" step from the proposal's business-process diagram.</summary>
    Public Module CsvExporter

        Public Sub ExportToCsv(table As DataTable, suggestedFileName As String)
            If table Is Nothing OrElse table.Rows.Count = 0 Then
                MessageBox.Show("There is nothing to export.", "Nothing to Export", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using sfd As New SaveFileDialog With {
                .Filter = "CSV file (*.csv)|*.csv",
                .FileName = suggestedFileName,
                .Title = "Export Report"
            }
                If sfd.ShowDialog() <> DialogResult.OK Then Return

                Try
                    Dim sb As New StringBuilder()
                    sb.AppendLine(String.Join(",", table.Columns.Cast(Of DataColumn)().Select(Function(c) EscapeCsv(c.ColumnName))))
                    For Each row As DataRow In table.Rows
                        sb.AppendLine(String.Join(",", row.ItemArray.Select(Function(v) EscapeCsv(If(v Is DBNull.Value, "", v?.ToString())))))
                    Next
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8)
                    MessageBox.Show("Report exported.", "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Could not export: " & ex.Message, "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Sub

        Private Function EscapeCsv(value As String) As String
            If value Is Nothing Then Return ""
            If value.Contains(",") OrElse value.Contains(""""c) OrElse value.Contains(vbLf) OrElse value.Contains(vbCr) Then
                Return """" & value.Replace("""", """""") & """"
            End If
            Return value
        End Function

    End Module

End Namespace
