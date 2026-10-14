Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' RHU Management "Lab Requests" tab: every lab request submitted by
    ''' healthcare staff lands here "For Issuance" (stored status 'For Approval'; one row per request, all
    ''' its tests together). Management either issues it to the RHU lab
    ''' (it then shows in the Laboratory queue as Pending) or refers it to an
    ''' external laboratory -- the request form is shown for review first and
    ''' the referral is only saved when it is printed.
    ''' </summary>
    Public Class LabRequestApprovalsForm
        Inherits Form

        Private Const StatusForApproval As String = "For Issuance"   ' display label; the stored status stays 'For Approval'
        Private Const StatusApproved As String = "Issued - RHU Lab"
        Private Const StatusReferred As String = "Referred - External Lab"

        Private grid As DataGridView
        Private txtSearch As TextBox
        Private cboView As ComboBox
        Private lblCount As Label
        Private btnApprove, btnRefer As Button

        Public Sub New()
            InitializeComponent()
            LoadData()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {.Location = New Point(0, 12), .Width = 260, .PlaceholderText = "Search patient, test or request no..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            cboView = New ComboBox With {.Location = New Point(270, 12), .Width = 190, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboView.Items.AddRange({StatusForApproval, StatusApproved, StatusReferred, "All Requests"})
            cboView.SelectedIndex = 0
            AddHandler cboView.SelectedIndexChanged, Sub() LoadData()
            toolbar.Controls.Add(cboView)

            btnRefer = New Button With {.Text = "Refer to External Lab", .Size = New Size(180, 34)}
            UiTheme.StyleSecondaryButton(btnRefer)
            AddHandler btnRefer.Click, AddressOf BtnRefer_Click

            btnApprove = New Button With {.Text = "Issue to RHU Lab", .Size = New Size(160, 34)}
            UiTheme.StylePrimaryButton(btnApprove)
            AddHandler btnApprove.Click, AddressOf BtnApprove_Click

            toolbar.Controls.AddRange({btnRefer, btnApprove})
            UiTheme.PinRowToRight(toolbar, 8, 0, 8, btnRefer, btnApprove)

            Dim hint As New Label With {
                .Text = "Issue to RHU Lab sends the request to the RHU laboratory queue. Refer shows the request form for review before it is printed. Double-click a referred request to reprint its form.",
                .Dock = DockStyle.Top, .Height = 22, .ForeColor = UiTheme.TextMuted
            }
            Me.Controls.Add(hint)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.SelectionChanged, Sub() UpdateButtons()
            AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso SelectedStatus() = StatusReferred Then LabRequestFormPrinter.ShowPreview(Me, SelectedKey())
            Me.Controls.Add(grid)
            grid.BringToFront()

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(lblCount)
        End Sub

        Private Sub LoadData()
            Try
                Dim key = LabRequestFormPrinter.RequestKeySql
                Dim search = txtSearch.Text.Trim()
                Dim havingSql = If(cboView.SelectedIndex < 3, "HAVING Status = @View", "")
                Dim table = Db.Query(
                    $"SELECT {key} AS 'Request No.', MIN(l.request_date) AS Requested,
                             MAX(CONCAT(p.last_name, ', ', p.first_name)) AS Patient, MAX(e.encounter_no) AS Encounter,
                             GROUP_CONCAT(l.test_name ORDER BY l.laboratory_id SEPARATOR ', ') AS Tests,
                             MAX(l.priority_level) AS Priority, MAX(l.specimen_type) AS Specimen,
                             MAX(ur.full_name) AS 'Requested By',
                             CASE WHEN SUM(l.status = 'For Approval') > 0 THEN '{StatusForApproval}'
                                  WHEN SUM(l.status = 'Referred') > 0 THEN '{StatusReferred}'
                                  ELSE '{StatusApproved}' END AS Status,
                             MAX(l.external_facility) AS 'Referred To', MAX(ua.full_name) AS 'Issued By', MAX(l.approved_at) AS 'Issued At'
                      FROM laboratory l
                      JOIN encounters e ON e.encounter_id = l.encounter_id
                      JOIN patients p ON p.patient_id = e.patient_id
                      LEFT JOIN users_staff ur ON ur.user_id = l.requested_by
                      LEFT JOIN users_staff ua ON ua.user_id = l.approved_by
                      WHERE (@Search = '' OR p.first_name LIKE @SearchLike OR p.last_name LIKE @SearchLike
                             OR l.test_name LIKE @SearchLike OR l.request_no LIKE @SearchLike)
                      GROUP BY {key}
                      {havingSql}
                      ORDER BY Requested {If(cboView.SelectedIndex = 0, "ASC", "DESC")}
                      LIMIT {UiTheme.ResultCap}",
                    New With {.Search = search, .SearchLike = "%" & search & "%", .View = cboView.Text})
                grid.DataSource = table
                lblCount.Text = "  " & UiTheme.FormatCountLabel(table.Rows.Count, "request")
            Catch ex As Exception
                lblCount.Text = "Error loading lab requests: " & ex.Message
            End Try
            UpdateButtons()
        End Sub

        Private Function SelectedKey() As String
            If grid.CurrentRow Is Nothing Then Return Nothing
            Return grid.CurrentRow.Cells("Request No.").Value?.ToString()
        End Function

        Private Function SelectedStatus() As String
            If grid.CurrentRow Is Nothing Then Return ""
            Return If(grid.CurrentRow.Cells("Status").Value?.ToString(), "")
        End Function

        Private Sub UpdateButtons()
            Dim status = SelectedStatus()
            btnApprove.Enabled = status = StatusForApproval
            btnRefer.Enabled = status = StatusForApproval
        End Sub

        Private Function SelectedSummary() As String
            Dim row = grid.CurrentRow
            Return $"{row.Cells("Patient").Value} -- {row.Cells("Tests").Value}"
        End Function

        Private Sub BtnApprove_Click(sender As Object, e As EventArgs)
            Dim key = SelectedKey()
            If key Is Nothing Then Return
            If MessageBox.Show($"Issue this request to the RHU laboratory?{vbCrLf}{vbCrLf}{SelectedSummary()}", "Issue Lab Request",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Try
                Dim n = Db.Execute(
                    $"UPDATE laboratory l SET l.status = 'Pending', l.lab_destination = 'In-House', l.approved_by = @UserId, l.approved_at = NOW()
                      WHERE {LabRequestFormPrinter.RequestKeySql} = @Key AND l.status = 'For Approval'",
                    New With {.UserId = Session.Current.UserId, .Key = key})
                AuditLogger.Log("UPDATE", "lab_requests", key, $"Issued {n} lab test(s) to the RHU laboratory")
                LoadData()
            Catch ex As Exception
                UiTheme.ShowError(Me, "issue request", ex)
            End Try
        End Sub

        Private Sub BtnRefer_Click(sender As Object, e As EventArgs)
            Dim key = SelectedKey()
            If key Is Nothing Then Return
            Dim summary = SelectedSummary()

            Dim facilities As New List(Of String)
            Try
                For Each r As DataRow In Db.Query("SELECT DISTINCT external_facility FROM laboratory WHERE external_facility IS NOT NULL ORDER BY external_facility").Rows
                    facilities.Add(r(0).ToString())
                Next
            Catch
            End Try

            Dim tests = grid.CurrentRow.Cells("Tests").Value?.ToString()
            Dim facility, remarks As String
            Using dlg As New PromptDialog("Refer to External Laboratory", "Refer to External Laboratory", "External Laboratory / Facility *", "Review Form",
                                          facilities, required:=True, remarksLabel:="Remarks (e.g. test not available at RHU)")
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                facility = dlg.Value : remarks = dlg.Remarks
            End Using

            ' Nothing is saved until the form has been reviewed and Refer & Print confirmed.
            Dim saveReferral As Func(Of Boolean) =
                Function()
                    Try
                        Using tx As New DbTransaction()
                            Dim encounterId = tx.QueryScalar(
                                $"SELECT MAX(l.encounter_id) FROM laboratory l WHERE {LabRequestFormPrinter.RequestKeySql} = @Key", New With {.Key = key})
                            Dim n = tx.Execute(
                                $"UPDATE laboratory l SET l.status = 'Referred', l.lab_destination = 'External', l.external_facility = @Facility,
                                         l.approval_remarks = @Remarks, l.approved_by = @UserId, l.approved_at = NOW()
                                  WHERE {LabRequestFormPrinter.RequestKeySql} = @Key AND l.status = 'For Approval'",
                                New With {.Facility = facility, .Remarks = If(remarks.Length = 0, CType(DBNull.Value, Object), remarks),
                                          .UserId = Session.Current.UserId, .Key = key})
                            If n = 0 Then
                                MessageBox.Show("This request is no longer waiting to be issued (someone else may have handled it).", "Refer",
                                                MessageBoxButtons.OK, MessageBoxIcon.Information)
                                Return False
                            End If
                            tx.Execute(
                                "INSERT INTO referrals (encounter_id, referred_to_facility, reason_for_referral, referral_date, referred_by, referral_type)
                                 VALUES (@EncounterId, @Facility, @Reason, NOW(), @UserId, 'External Laboratory')",
                                New With {.EncounterId = encounterId, .Facility = facility, .UserId = Session.Current.UserId,
                                          .Reason = $"Laboratory request {key}: {tests}" & If(remarks.Length > 0, $" ({remarks})", "")})
                            tx.Commit()
                        End Using
                        AuditLogger.Log("UPDATE", "lab_requests", key, $"Referred lab request to external laboratory: {facility}")
                        Return True
                    Catch ex As Exception
                        UiTheme.ShowError(Me, "refer request", ex)
                        Return False
                    End Try
                End Function

            LabRequestFormPrinter.ShowReferralOverview(Me, key, facility, remarks, saveReferral)
            LoadData()
        End Sub

    End Class

End Namespace
