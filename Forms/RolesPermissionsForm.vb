Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Role catalog + permission editor (System Administrator only): module access checklist + add/edit/delete/view flags per role.</summary>
    Public Class RolesPermissionsForm
        Inherits Form

        Private gridRoles As DataGridView
        Private lblSelected, lblHint As Label
        Private txtDescription As TextBox
        Private cboStatus As ComboBox
        Private chkCanAdd, chkCanEdit, chkCanDelete, chkCanView As CheckBox
        Private moduleChecks As New Dictionary(Of String, CheckBox)
        Private btnSave As Button
        Private selectedRoleId As Integer? = Nothing
        Private selectedRoleName As String = ""

        Public Sub New()
            InitializeComponent()
            LoadRoles()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)
            Dim btnNew As New Button With {.Text = "+ New Role", .Size = New Size(130, 34), .Location = New Point(0, 8)}
            UiTheme.StylePrimaryButton(btnNew)
            AddHandler btnNew.Click, AddressOf BtnNewRole_Click
            toolbar.Controls.Add(btnNew)

            Dim split As New SplitContainer With {.Dock = DockStyle.Fill}
            Me.Controls.Add(split)
            split.BringToFront()
            UiTheme.ConfigureSplitter(split, 380, 420, 460)

            gridRoles = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridRoles)
            AddHandler gridRoles.SelectionChanged, AddressOf GridRoles_SelectionChanged
            split.Panel1.Controls.Add(gridRoles)

            Dim editorPanel As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(16)}
            split.Panel2.Controls.Add(editorPanel)

            lblSelected = New Label With {.Text = "Select a role to edit its permissions", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(16, 0)}
            editorPanel.Controls.Add(lblSelected)

            editorPanel.Controls.Add(New Label With {.Text = "Description", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(16, 36)})
            txtDescription = New TextBox With {.Location = New Point(16, 56), .Width = 380, .Height = 50, .Multiline = True}
            editorPanel.Controls.Add(txtDescription)

            editorPanel.Controls.Add(New Label With {.Text = "Status", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(16, 116)})
            cboStatus = New ComboBox With {.Location = New Point(16, 136), .Width = 160, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"Active", "Inactive"})
            editorPanel.Controls.Add(cboStatus)

            editorPanel.Controls.Add(New Label With {.Text = "Record Permissions", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(16, 176)})
            chkCanView = New CheckBox With {.Text = "Can View", .Location = New Point(16, 198), .AutoSize = True}
            chkCanAdd = New CheckBox With {.Text = "Can Add", .Location = New Point(130, 198), .AutoSize = True}
            chkCanEdit = New CheckBox With {.Text = "Can Edit", .Location = New Point(240, 198), .AutoSize = True}
            chkCanDelete = New CheckBox With {.Text = "Can Delete", .Location = New Point(350, 198), .AutoSize = True}
            editorPanel.Controls.AddRange({chkCanView, chkCanAdd, chkCanEdit, chkCanDelete})

            editorPanel.Controls.Add(New Label With {.Text = "Module Access", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(16, 232)})
            Dim y As Integer = 254
            Dim col As Integer = 0
            For Each m In ModuleCatalog.Modules
                Dim chk As New CheckBox With {.Text = m.Label, .AutoSize = True, .Location = New Point(16 + (col * 200), y)}
                moduleChecks(m.Key) = chk
                editorPanel.Controls.Add(chk)
                col += 1
                If col = 2 Then
                    col = 0
                    y += 26
                End If
            Next
            y += 40

            lblHint = New Label With {
                .Text = "Changes take effect the next time an affected staff member signs in.",
                .ForeColor = UiTheme.TextMuted, .AutoSize = True, .MaximumSize = New Size(400, 0), .Location = New Point(16, y)
            }
            editorPanel.Controls.Add(lblHint)
            y += 40

            btnSave = New Button With {.Text = "Save Role", .Size = New Size(140, 36), .Location = New Point(16, y), .Enabled = False}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            editorPanel.Controls.Add(btnSave)
        End Sub

        Private Sub LoadRoles()
            Try
                Dim table = Db.Query(
                    "SELECT role_id AS Id, role_name AS 'Role Name', status AS Status,
                            (SELECT COUNT(*) FROM users_staff u WHERE u.role_id = roles.role_id) AS 'Staff Count'
                     FROM roles ORDER BY role_id")
                gridRoles.DataSource = table
                If gridRoles.Columns.Contains("Id") Then gridRoles.Columns("Id").Visible = False
            Catch ex As Exception
                MessageBox.Show("Could not load roles: " & ex.Message)
            End Try
        End Sub

        Private Sub GridRoles_SelectionChanged(sender As Object, e As EventArgs)
            If gridRoles.CurrentRow Is Nothing Then Return
            selectedRoleId = Convert.ToInt32(gridRoles.CurrentRow.Cells("Id").Value)
            selectedRoleName = gridRoles.CurrentRow.Cells("Role Name").Value.ToString()
            lblSelected.Text = "Editing: " & selectedRoleName
            btnSave.Enabled = True
            LoadRoleDetails()
        End Sub

        Private Sub LoadRoleDetails()
            If Not selectedRoleId.HasValue Then Return
            Try
                Dim table = Db.Query("SELECT * FROM roles WHERE role_id = @Id", New With {.Id = selectedRoleId.Value})
                If table.Rows.Count = 0 Then Return
                Dim r = table.Rows(0)

                txtDescription.Text = If(IsDBNull(r("description")), "", r("description").ToString())
                cboStatus.SelectedItem = r("status").ToString()
                chkCanAdd.Checked = Convert.ToBoolean(r("can_add"))
                chkCanEdit.Checked = Convert.ToBoolean(r("can_edit"))
                chkCanDelete.Checked = Convert.ToBoolean(r("can_delete"))
                chkCanView.Checked = Convert.ToBoolean(r("can_view"))

                Dim modules = If(IsDBNull(r("module_access")), "", r("module_access").ToString())
                Dim enabledModules As New HashSet(Of String)(modules.Split(","c).Select(Function(s) s.Trim()))
                For Each kvp In moduleChecks
                    kvp.Value.Checked = enabledModules.Contains(kvp.Key)
                Next
            Catch ex As Exception
                MessageBox.Show("Could not load role details: " & ex.Message)
            End Try
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If Not selectedRoleId.HasValue Then Return

            If selectedRoleId.Value = 1 AndAlso Not chkCanView.Checked Then
                MessageBox.Show("The System Administrator role must keep at least view access.", "Not Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Try
                Dim selectedModules = moduleChecks.Where(Function(kvp) kvp.Value.Checked).Select(Function(kvp) kvp.Key).ToList()
                Dim moduleAccess = String.Join(",", selectedModules)

                Db.Execute(
                    "UPDATE roles SET description=@Description, module_access=@ModuleAccess,
                        can_add=@CanAdd, can_edit=@CanEdit, can_delete=@CanDelete, can_view=@CanView, status=@Status
                     WHERE role_id=@Id",
                    New With {
                        .Description = If(String.IsNullOrWhiteSpace(txtDescription.Text), CType(DBNull.Value, Object), txtDescription.Text.Trim()),
                        .ModuleAccess = moduleAccess,
                        .CanAdd = chkCanAdd.Checked, .CanEdit = chkCanEdit.Checked, .CanDelete = chkCanDelete.Checked, .CanView = chkCanView.Checked,
                        .Status = cboStatus.Text, .Id = selectedRoleId.Value
                    })

                AuditLogger.Log("UPDATE", "roles", selectedRoleName, "Updated role permissions")

                If Session.IsLoggedIn AndAlso Session.Current.RoleId = selectedRoleId.Value Then
                    MessageBox.Show("Saved. Since this is your own role, sign out and back in for the new permissions to take effect in this session.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Else
                    MessageBox.Show("Role permissions saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
                LoadRoles()
            Catch ex As Exception
                MessageBox.Show("Could not save role: " & ex.Message)
            End Try
        End Sub

        Private Sub BtnNewRole_Click(sender As Object, e As EventArgs)
            Using dlg As New NewRoleDialog()
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadRoles()
            End Using
        End Sub

    End Class

    ''' <summary>Small modal to create a new role (name + description); permissions are edited afterward in RolesPermissionsForm.</summary>
    Public Class NewRoleDialog
        Inherits Form

        Private txtName, txtDescription As TextBox
        Private lblError As Label

        Public Sub New()
            Me.Text = "New Role"
            Me.Size = New Size(440, 300)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = "New Role", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})

            Me.Controls.Add(New Label With {.Text = "Role Name *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 56)})
            txtName = New TextBox With {.Location = New Point(20, 76), .Width = 380}
            Me.Controls.Add(txtName)

            Me.Controls.Add(New Label With {.Text = "Description", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 112)})
            txtDescription = New TextBox With {.Location = New Point(20, 132), .Width = 380, .Height = 60, .Multiline = True}
            Me.Controls.Add(txtDescription)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 200), .MaximumSize = New Size(380, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(190, 220)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Create", .Size = New Size(110, 36), .Location = New Point(300, 220)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtName.Text) Then
                lblError.Text = "Role name is required."
                Return
            End If
            Try
                Db.Execute(
                    "INSERT INTO roles (role_name, description, module_access, can_add, can_edit, can_delete, can_view, status)
                     VALUES (@Name, @Description, 'dashboard', FALSE, FALSE, FALSE, TRUE, 'Active')",
                    New With {.Name = txtName.Text.Trim(), .Description = If(String.IsNullOrWhiteSpace(txtDescription.Text), CType(DBNull.Value, Object), txtDescription.Text.Trim())})
                AuditLogger.Log("CREATE", "roles", txtName.Text.Trim(), "Created new role")
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = If(ex.Message.Contains("Duplicate"), "A role with that name already exists.", "Could not save: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
