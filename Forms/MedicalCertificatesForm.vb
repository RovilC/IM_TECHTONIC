Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Medical Certificates module (Figure 20): browse issued certificates, issue new ones, view/print any of them.</summary>
    Public Class MedicalCertificatesForm
        Inherits Form

        Private grid As DataGridView
        Private txtSearch As TextBox
        Private lblCount As Label

        Public Sub New()
            InitializeComponent()
            LoadData()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)

            txtSearch = New TextBox With {.Location = New Point(0, 10), .Width = 300, .PlaceholderText = "Search patient or purpose..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            Dim btnIssue As New Button With {.Text = "+ Issue Certificate", .Size = New Size(170, 34)}
            UiTheme.StylePrimaryButton(btnIssue)
            UiTheme.PinToRight(btnIssue, toolbar, 0, 8)
            AddHandler btnIssue.Click, AddressOf BtnIssue_Click
            toolbar.Controls.Add(btnIssue)

            Dim hint As New Label With {.Text = "Double-click a certificate to view/print it.", .Dock = DockStyle.Top, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(hint)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then OpenView()
            Me.Controls.Add(grid)
            grid.BringToFront()

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(lblCount)
        End Sub

        Private Sub LoadData()
            Try
                Dim search = txtSearch.Text.Trim()
                Dim table = Db.Query(
                    $"SELECT doc.document_id AS Id, doc.reference_no AS 'Certificate No.', doc.created_at AS 'Date Issued',
                            CONCAT(p.last_name, ', ', p.first_name) AS Patient, doc.purpose AS Purpose, u.full_name AS 'Issued By', doc.status AS Status
                     FROM other_documents_services doc
                     JOIN encounters e ON e.encounter_id = doc.encounter_id
                     JOIN patients p ON p.patient_id = e.patient_id
                     LEFT JOIN users_staff u ON u.user_id = doc.created_by
                     WHERE doc.document_type = 'Medical Certificate'
                       AND (@Search = '' OR p.first_name LIKE @SearchLike OR p.last_name LIKE @SearchLike OR doc.purpose LIKE @SearchLike)
                     ORDER BY doc.created_at DESC
                     LIMIT {UiTheme.ResultCap}",
                    New With {.Search = search, .SearchLike = "%" & search & "%"})
                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                lblCount.Text = "  " & UiTheme.FormatCountLabel(table.Rows.Count, "certificate")
            Catch ex As Exception
                lblCount.Text = "Error loading certificates: " & ex.Message
            End Try
        End Sub

        Private Sub BtnIssue_Click(sender As Object, e As EventArgs)
            Using dlg As New IssueCertificateDialog()
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    LoadData()
                    MessageBox.Show("Medical certificate submitted. It can be printed once RHU Management approves it.", "Submitted", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End Using
        End Sub

        Private Sub OpenView()
            If grid.CurrentRow Is Nothing Then Return
            Dim id = Convert.ToInt32(grid.CurrentRow.Cells("Id").Value)
            Using view As New CertificateViewDialog(id)
                view.ShowDialog(Me)
            End Using
        End Sub

    End Class

End Namespace
