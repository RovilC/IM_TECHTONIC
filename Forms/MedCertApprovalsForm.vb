Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>
    ''' RHU Management "Med Certs" tab: medical certificates requested by
    ''' healthcare staff arrive "For Approval". Issue opens the certificate
    ''' for review; printing it from there issues it (status 'Issued'),
    ''' Cancel leaves it waiting. Issued certificates can be reprinted by
    ''' double-clicking them.
    ''' </summary>
    Public Class MedCertApprovalsForm
        Inherits Form

        Private grid As DataGridView
        Private txtSearch As TextBox
        Private cboView As ComboBox
        Private lblCount As Label
        Private btnIssue As Button

        Public Sub New()
            InitializeComponent()
            LoadData()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {.Location = New Point(0, 12), .Width = 260, .PlaceholderText = "Search patient, purpose or certificate no..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            cboView = New ComboBox With {.Location = New Point(270, 12), .Width = 160, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboView.Items.AddRange({"For Approval", "Issued", "All Certificates"})
            cboView.SelectedIndex = 0
            AddHandler cboView.SelectedIndexChanged, Sub() LoadData()
            toolbar.Controls.Add(cboView)

            btnIssue = New Button With {.Text = "Issue", .Size = New Size(120, 34)}
            UiTheme.StylePrimaryButton(btnIssue)
            AddHandler btnIssue.Click, Sub() OpenIssue()
            toolbar.Controls.Add(btnIssue)
            UiTheme.PinRowToRight(toolbar, 8, 0, 8, btnIssue)

            Dim hint As New Label With {.Text = "Issue opens the certificate for review -- Print issues it, Cancel leaves it waiting. Double-click an issued certificate to reprint it.", .Dock = DockStyle.Top, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(hint)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.SelectionChanged, Sub() UpdateButtons()
            AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then OpenIssue()
            Me.Controls.Add(grid)
            grid.BringToFront()

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(lblCount)
        End Sub

        Private Sub LoadData()
            Try
                ' 'Approved' = certificates approved before the Issue wording; treat them as issued.
                Dim statusSql As String
                Select Case cboView.SelectedIndex
                    Case 0 : statusSql = "AND doc.status = 'For Approval'"
                    Case 1 : statusSql = "AND doc.status IN ('Issued', 'Approved')"
                    Case Else : statusSql = ""
                End Select
                Dim search = txtSearch.Text.Trim()
                Dim table = Db.Query(
                    $"SELECT doc.document_id AS Id, doc.reference_no AS 'Certificate No.', doc.created_at AS Requested,
                             CONCAT(p.last_name, ', ', p.first_name) AS Patient, doc.purpose AS Purpose,
                             u.full_name AS 'Requested By', doc.status AS Status, ua.full_name AS 'Issued By', doc.approved_at AS 'Issued At'
                      FROM other_documents_services doc
                      JOIN encounters e ON e.encounter_id = doc.encounter_id
                      JOIN patients p ON p.patient_id = e.patient_id
                      LEFT JOIN users_staff u ON u.user_id = doc.created_by
                      LEFT JOIN users_staff ua ON ua.user_id = doc.approved_by
                      WHERE doc.document_type = 'Medical Certificate' {statusSql}
                        AND (@Search = '' OR p.first_name LIKE @SearchLike OR p.last_name LIKE @SearchLike
                             OR doc.purpose LIKE @SearchLike OR doc.reference_no LIKE @SearchLike)
                      ORDER BY doc.created_at {If(cboView.SelectedIndex = 0, "ASC", "DESC")}
                      LIMIT {UiTheme.ResultCap}",
                    New With {.Search = search, .SearchLike = "%" & search & "%"})
                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                lblCount.Text = "  " & UiTheme.FormatCountLabel(table.Rows.Count, "certificate")
            Catch ex As Exception
                lblCount.Text = "Error loading certificates: " & ex.Message
            End Try
            UpdateButtons()
        End Sub

        Private Function SelectedStatus() As String
            If grid.CurrentRow Is Nothing Then Return ""
            Return If(grid.CurrentRow.Cells("Status").Value?.ToString(), "")
        End Function

        Private Sub UpdateButtons()
            btnIssue.Enabled = SelectedStatus() = "For Approval"
        End Sub

        ''' <summary>For Approval rows open in issue mode (Print = issue + print, Cancel = nothing saved); issued rows open for reprinting.</summary>
        Private Sub OpenIssue()
            If grid.CurrentRow Is Nothing Then Return
            Dim id = Convert.ToInt32(grid.CurrentRow.Cells("Id").Value)
            Using view As New CertificateViewDialog(id, issueMode:=SelectedStatus() = "For Approval")
                view.ShowDialog(Me)
                If view.IssuedNow Then LoadData()
            End Using
        End Sub

    End Class

End Namespace
