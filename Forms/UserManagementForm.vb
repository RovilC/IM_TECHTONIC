Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Staff account administration: create logins (active on creation), edit them by double-clicking, and permanently delete them (System Administrator only).</summary>
    Public Class UserManagementForm
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

            txtSearch = New TextBox With {.Location = New Point(0, 10), .Width = 280, .PlaceholderText = "Search name or username..."}
            UiTheme.AttachDebouncedSearch(txtSearch, AddressOf LoadData)
            toolbar.Controls.Add(txtSearch)

            Dim btnAdd As New Button With {.Text = "+ Add Staff Account", .Size = New Size(170, 34)}
            UiTheme.StylePrimaryButton(btnAdd)
            AddHandler btnAdd.Click, AddressOf BtnAdd_Click
            toolbar.Controls.Add(btnAdd)

            Dim btnDelete As New Button With {.Text = "Delete Account", .Size = New Size(150, 34)}
            UiTheme.StyleSecondaryButton(btnDelete)
            btnDelete.ForeColor = UiTheme.DangerRed
            AddHandler btnDelete.Click, AddressOf BtnDelete_Click
            toolbar.Controls.Add(btnDelete)

            UiTheme.PinRowToRight(toolbar, 8, 0, 10, btnDelete, btnAdd)

            Dim hint As New Label With {.Text = "Double-click a row to edit.", .Dock = DockStyle.Top, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(hint)

            grid = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(grid)
            AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then OpenEdit()
            Me.Controls.Add(grid)
            grid.BringToFront()

            lblCount = New Label With {.Dock = DockStyle.Bottom, .Height = 22, .ForeColor = UiTheme.TextMuted}
            Me.Controls.Add(lblCount)
        End Sub

        Private Sub LoadData()
            Try
                Dim search = txtSearch.Text.Trim()
                Dim table = Db.Query(
                    "SELECT u.user_id AS Id, u.full_name AS 'Full Name', u.username AS Username, r.role_name AS Role,
                            u.contact_number AS Contact, u.email AS Email, u.status AS Status, u.last_login AS 'Last Login'
                     FROM users_staff u JOIN roles r ON r.role_id = u.role_id
                     WHERE (@Search = '' OR u.full_name LIKE @SearchLike OR u.username LIKE @SearchLike)
                     ORDER BY u.full_name",
                    New With {.Search = search, .SearchLike = "%" & search & "%"})
                grid.DataSource = table
                If grid.Columns.Contains("Id") Then grid.Columns("Id").Visible = False
                lblCount.Text = $"  {table.Rows.Count} account(s)"
            Catch ex As Exception
                lblCount.Text = "Error loading accounts: " & ex.Message
            End Try
        End Sub

        Private Function SelectedUserId() As Integer?
            If grid.CurrentRow Is Nothing Then Return Nothing
            Return Convert.ToInt32(grid.CurrentRow.Cells("Id").Value)
        End Function

        Private Sub BtnAdd_Click(sender As Object, e As EventArgs)
            Using dlg As New AddEditUserDialog(Nothing)
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadData()
            End Using
        End Sub

        Private Sub OpenEdit()
            Dim id = SelectedUserId()
            If Not id.HasValue Then Return
            Using dlg As New AddEditUserDialog(id)
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadData()
            End Using
        End Sub

        ''' <summary>
        ''' Permanently removes the selected login. Audit log entries keep the username snapshot;
        ''' accounts that authored clinical records (encounters, consultations, prescriptions,
        ''' dispensing, lab requests, referrals, certificates) are protected by the database's
        ''' foreign keys so patient records never lose their author.
        ''' </summary>
        Private Sub BtnDelete_Click(sender As Object, e As EventArgs)
            Dim id = SelectedUserId()
            If Not id.HasValue Then
                MessageBox.Show("Select an account first.")
                Return
            End If
            If Session.IsLoggedIn AndAlso id.Value = Session.Current.UserId Then
                MessageBox.Show("You cannot delete the account you're currently signed in with.", "Not Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim fullName = grid.CurrentRow.Cells("Full Name").Value.ToString()
            Dim username = grid.CurrentRow.Cells("Username").Value.ToString()
            Dim confirm = MessageBox.Show(
                $"Permanently delete the account of {fullName} ({username})?{Environment.NewLine}{Environment.NewLine}This cannot be undone.",
                "Delete Account", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
            If confirm <> DialogResult.Yes Then Return

            Try
                Db.Execute("DELETE FROM users_staff WHERE user_id = @Id", New With {.Id = id.Value})
                AuditLogger.Log("DELETE", "users", username, $"Permanently deleted staff account {fullName}")
                LoadData()
            Catch ex As MySqlConnector.MySqlException When ex.ErrorCode = MySqlConnector.MySqlErrorCode.RowIsReferenced2
                MessageBox.Show(
                    $"{fullName} cannot be deleted because this account is recorded on patient records (encounters, consultations, prescriptions, lab requests, etc.).",
                    "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                UiTheme.ShowError(Me, "delete account", ex)
            End Try
        End Sub

    End Class

End Namespace
