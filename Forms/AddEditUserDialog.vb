Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Security

Namespace Forms

    ''' <summary>Create or edit a staff account. Pass Nothing for userId to create; password fields are required on create, optional ("leave blank to keep current") on edit. New accounts are always created Active -- creating an account is its activation.</summary>
    Public Class AddEditUserDialog
        Inherits Form

        Private ReadOnly editingUserId As Integer?

        Private txtFullName, txtUsername, txtPassword, txtConfirmPassword, txtContact, txtEmail As TextBox
        Private cboRole As ComboBox
        Private lblError As Label
        Public Saved As Boolean = False

        Private Class RoleItem
            Public ReadOnly Id As Integer
            Public ReadOnly Name As String
            Public Sub New(id As Integer, name As String)
                Me.Id = id : Me.Name = name
            End Sub
            Public Overrides Function ToString() As String
                Return Name
            End Function
        End Class

        Public Sub New(userId As Integer?)
            Me.editingUserId = userId
            Me.Text = If(userId.HasValue, "Edit Staff Account", "Add Staff Account")
            Me.Size = New Size(480, 504)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = Me.Text, .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})

            Dim y = 56
            Me.Controls.Add(New Label With {.Text = "Full Name *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtFullName = New TextBox With {.Location = New Point(20, y + 20), .Width = 400}
            Me.Controls.Add(txtFullName)

            y += 56
            Me.Controls.Add(New Label With {.Text = "Username *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtUsername = New TextBox With {.Location = New Point(20, y + 20), .Width = 400}
            If userId.HasValue Then txtUsername.Enabled = False ' username is the login key; keep stable once created
            Me.Controls.Add(txtUsername)

            y += 56
            Dim pwLabel = If(userId.HasValue, "Password (leave blank to keep current)", "Password *")
            Me.Controls.Add(New Label With {.Text = pwLabel, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtPassword = New TextBox With {.Location = New Point(20, y + 20), .Width = 190, .UseSystemPasswordChar = True}
            Me.Controls.Add(txtPassword)
            txtConfirmPassword = New TextBox With {.Location = New Point(230, y + 20), .Width = 190, .UseSystemPasswordChar = True, .PlaceholderText = "Confirm"}
            Me.Controls.Add(txtConfirmPassword)

            y += 56
            Me.Controls.Add(New Label With {.Text = "Role *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            cboRole = New ComboBox With {.Location = New Point(20, y + 20), .Width = 260, .DropDownStyle = ComboBoxStyle.DropDownList}
            Me.Controls.Add(cboRole)

            y += 56
            Me.Controls.Add(New Label With {.Text = "Contact Number", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtContact = New TextBox With {.Location = New Point(20, y + 20), .Width = 190}
            Me.Controls.Add(txtContact)

            y += 56
            Me.Controls.Add(New Label With {.Text = "Email", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            txtEmail = New TextBox With {.Location = New Point(20, y + 20), .Width = 400}
            Me.Controls.Add(txtEmail)

            y += 56
            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, y), .MaximumSize = New Size(400, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(190, 424)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Save", .Size = New Size(110, 36), .Location = New Point(300, 424)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)

            LoadRoles()
            If editingUserId.HasValue Then LoadExisting(editingUserId.Value)
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        Private Sub LoadRoles()
            Try
                cboRole.Items.Clear()
                Dim table = Db.Query("SELECT role_id, role_name FROM roles WHERE status = 'Active' ORDER BY role_id")
                For Each row As DataRow In table.Rows
                    cboRole.Items.Add(New RoleItem(Convert.ToInt32(row("role_id")), row("role_name").ToString()))
                Next
                If cboRole.Items.Count > 0 Then cboRole.SelectedIndex = 0
            Catch ex As Exception
                lblError.Text = "Could not load roles: " & ex.Message
            End Try
        End Sub

        Private Sub LoadExisting(userId As Integer)
            Dim table = Db.Query("SELECT * FROM users_staff WHERE user_id = @Id", New With {.Id = userId})
            If table.Rows.Count = 0 Then Return
            Dim r = table.Rows(0)
            txtFullName.Text = r("full_name").ToString()
            txtUsername.Text = r("username").ToString()
            txtContact.Text = If(IsDBNull(r("contact_number")), "", r("contact_number").ToString())
            txtEmail.Text = If(IsDBNull(r("email")), "", r("email").ToString())
            Dim roleId = Convert.ToInt32(r("role_id"))
            For Each item As RoleItem In cboRole.Items
                If item.Id = roleId Then cboRole.SelectedItem = item
            Next
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            lblError.Text = ""
            If String.IsNullOrWhiteSpace(txtFullName.Text) OrElse String.IsNullOrWhiteSpace(txtUsername.Text) Then
                lblError.Text = "Full name and username are required."
                Return
            End If
            If cboRole.SelectedItem Is Nothing Then
                lblError.Text = "Select a role."
                Return
            End If
            If Not editingUserId.HasValue AndAlso String.IsNullOrWhiteSpace(txtPassword.Text) Then
                lblError.Text = "Password is required for a new account."
                Return
            End If
            If txtPassword.Text.Length > 0 OrElse txtConfirmPassword.Text.Length > 0 Then
                If txtPassword.Text <> txtConfirmPassword.Text Then
                    lblError.Text = "Password and confirmation do not match."
                    Return
                End If
                If txtPassword.Text.Length > 0 AndAlso txtPassword.Text.Length < 4 Then
                    lblError.Text = "Password must be at least 4 characters."
                    Return
                End If
            End If
            If Not UiTheme.Confirm(Me, If(editingUserId.HasValue,
                                          $"Save the changes to {txtFullName.Text.Trim()}'s account?",
                                          $"Create the account ""{txtUsername.Text.Trim()}"" for {txtFullName.Text.Trim()} ({cboRole.Text})?"),
                                   If(editingUserId.HasValue, "Save Account", "Create Account")) Then Return

            Try
                Dim roleId = CType(cboRole.SelectedItem, RoleItem).Id
                If editingUserId.HasValue Then
                    If txtPassword.Text.Length > 0 Then
                        Db.Execute("UPDATE users_staff SET full_name=@FullName, role_id=@RoleId, contact_number=@Contact, email=@Email, password_hash=@PasswordHash WHERE user_id=@Id",
                            New With {
                                .FullName = txtFullName.Text.Trim(), .RoleId = roleId,
                                .Contact = EmptyToNull(txtContact.Text), .Email = EmptyToNull(txtEmail.Text),
                                .PasswordHash = PasswordHasher.HashPassword(txtPassword.Text), .Id = editingUserId.Value
                            })
                    Else
                        Db.Execute("UPDATE users_staff SET full_name=@FullName, role_id=@RoleId, contact_number=@Contact, email=@Email WHERE user_id=@Id",
                            New With {
                                .FullName = txtFullName.Text.Trim(), .RoleId = roleId,
                                .Contact = EmptyToNull(txtContact.Text), .Email = EmptyToNull(txtEmail.Text), .Id = editingUserId.Value
                            })
                    End If
                    AuditLogger.Log("UPDATE", "users", txtUsername.Text.Trim(), $"Updated staff account {txtFullName.Text.Trim()}")
                Else
                    Db.Execute(
                        "INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status, date_created)
                         VALUES (@FullName, @Username, @PasswordHash, @RoleId, @Contact, @Email, 'Active', NOW())",
                        New With {
                            .FullName = txtFullName.Text.Trim(), .Username = txtUsername.Text.Trim(),
                            .PasswordHash = PasswordHasher.HashPassword(txtPassword.Text), .RoleId = roleId,
                            .Contact = EmptyToNull(txtContact.Text), .Email = EmptyToNull(txtEmail.Text)
                        })
                    AuditLogger.Log("CREATE", "users", txtUsername.Text.Trim(), $"Created staff account {txtFullName.Text.Trim()}")
                End If
                Saved = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                If ex.Message.Contains("Duplicate") Then
                    lblError.Text = "That username is already taken."
                Else
                    lblError.Text = "Could not save: " & ex.Message
                End If
            End Try
        End Sub

        Private Function EmptyToNull(s As String) As Object
            Return If(String.IsNullOrWhiteSpace(s), CType(DBNull.Value, Object), s.Trim())
        End Function

    End Class

End Namespace
