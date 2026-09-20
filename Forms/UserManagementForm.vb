Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Staff account administration: create, edit, and activate/deactivate logins (System Administrator only).</summary>
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

            Dim btnEdit As New Button With {.Text = "Edit", .Size = New Size(100, 34)}
            UiTheme.StyleSecondaryButton(btnEdit)
            AddHandler btnEdit.Click, AddressOf BtnEdit_Click
            toolbar.Controls.Add(btnEdit)

            Dim btnToggleStatus As New Button With {.Text = "Activate / Deactivate", .Size = New Size(170, 34)}
            UiTheme.StyleSecondaryButton(btnToggleStatus)
            AddHandler btnToggleStatus.Click, AddressOf BtnToggleStatus_Click
            toolbar.Controls.Add(btnToggleStatus)

            UiTheme.PinRowToRight(toolbar, 8, 0, 10, btnToggleStatus, btnEdit, btnAdd)

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

        Private Sub BtnEdit_Click(sender As Object, e As EventArgs)
            OpenEdit()
        End Sub

        Private Sub OpenEdit()
            Dim id = SelectedUserId()
            If Not id.HasValue Then
                MessageBox.Show("Select an account first.")
                Return
            End If
            Using dlg As New AddEditUserDialog(id)
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadData()
            End Using
        End Sub

        Private Sub BtnToggleStatus_Click(sender As Object, e As EventArgs)
            Dim id = SelectedUserId()
            If Not id.HasValue Then
                MessageBox.Show("Select an account first.")
                Return
            End If
            If Session.IsLoggedIn AndAlso id.Value = Session.Current.UserId Then
                MessageBox.Show("You cannot deactivate the account you're currently signed in with.", "Not Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Try
                Dim currentStatus = grid.CurrentRow.Cells("Status").Value.ToString()
                Dim newStatus = If(currentStatus = "Active", "Inactive", "Active")
                Dim confirm = MessageBox.Show($"Set this account to '{newStatus}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If confirm <> DialogResult.Yes Then Return

                Db.Execute("UPDATE users_staff SET status = @Status WHERE user_id = @Id", New With {.Status = newStatus, .Id = id.Value})
                AuditLogger.Log("UPDATE", "users", $"User #{id.Value}", $"Status changed to {newStatus}")
                LoadData()
            Catch ex As Exception
                MessageBox.Show("Could not update status: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
