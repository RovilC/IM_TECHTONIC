Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    Partial Public Class ReportsForm

        ' ---------------- 9. Audit Logs (Figure 36) ----------------
        Private Function BuildAuditLogReport() As Panel
            Dim p As New Panel()
            Dim btnExport = ReportHeader(p, "System Activity & Audit Logs Report")

            Dim range = BuildDateRangeFilter(p)
            Dim dtpFrom = range.dtpFrom
            Dim dtpTo = range.dtpTo

            p.Controls.Add(New Label With {.Text = "Action", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(340, 44)})
            Dim cboAction As New ComboBox With {.Location = New Point(340, 64), .Width = 160, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboAction.Items.AddRange({"All Actions", "CREATE", "UPDATE", "DELETE", "LOGIN", "LOGIN_FAILED", "LOGOUT"})
            cboAction.SelectedIndex = 0
            p.Controls.Add(cboAction)

            p.Controls.Add(New Label With {.Text = "User", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(520, 44)})
            Dim cboUser As New ComboBox With {.Location = New Point(520, 64), .Width = 180, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboUser.Items.Add("All Users")
            Try
                For Each row As DataRow In Db.Query("SELECT DISTINCT username FROM users_staff ORDER BY username").Rows
                    cboUser.Items.Add(row("username").ToString())
                Next
            Catch
            End Try
            cboUser.SelectedIndex = 0
            p.Controls.Add(cboUser)

            Dim lblSummary = MakeSummaryLabel(0, 100)
            p.Controls.Add(lblSummary)

            Dim grid = MakeGrid(126)
            p.Controls.Add(grid)

            Dim refresh As Action =
                Sub()
                    Try
                        Dim fromD = dtpFrom.Value.Date
                        Dim toD = dtpTo.Value.Date.AddDays(1).AddSeconds(-1)
                        Dim actionFilter = If(cboAction.SelectedIndex > 0, cboAction.Text, "")
                        Dim userFilter = If(cboUser.SelectedIndex > 0, cboUser.Text, "")

                        Dim table = Db.Query(
                            "SELECT created_at AS Timestamp, username_snapshot AS 'User Account', role_snapshot AS Role,
                                    action AS Action, module AS Module, target_record AS 'Target Record', description AS Description, status AS Status
                             FROM audit_logs
                             WHERE created_at BETWEEN @From AND @To
                               AND (@Action = '' OR action = @Action)
                               AND (@User = '' OR username_snapshot = @User)
                             ORDER BY created_at DESC
                             LIMIT 1000",
                            New With {.From = fromD, .To = toD, .Action = actionFilter, .User = userFilter})
                        grid.DataSource = table

                        Dim totalEvents = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM audit_logs WHERE created_at BETWEEN @From AND @To", New With {.From = fromD, .To = toD}))
                        Dim failedLogins = Convert.ToInt32(Db.QueryScalar(
                            "SELECT COUNT(*) FROM audit_logs WHERE created_at BETWEEN @From AND @To AND action = 'LOGIN_FAILED'", New With {.From = fromD, .To = toD}))
                        lblSummary.Text = $"Total Events: {totalEvents}     Failed Login Attempts: {failedLogins}     (showing latest {Math.Min(totalEvents, 1000)})"
                    Catch ex As Exception
                        lblSummary.Text = "Error: " & ex.Message
                    End Try
                End Sub
            AddHandler dtpFrom.ValueChanged, Sub() refresh()
            AddHandler dtpTo.ValueChanged, Sub() refresh()
            AddHandler cboAction.SelectedIndexChanged, Sub() refresh()
            AddHandler cboUser.SelectedIndexChanged, Sub() refresh()
            WireExportMenu(btnExport, "System Activity & Audit Logs Report", "AuditLogs",
                Function() New List(Of (String, DataTable)) From {("Audit Logs", CType(grid.DataSource, DataTable))})
            refresh()
            Return p
        End Function

    End Class

End Namespace
