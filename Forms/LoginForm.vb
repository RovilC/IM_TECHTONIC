Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models
Imports TechTonic_IM_RHU.Security

Namespace Forms

    ''' <summary>Sign-in screen (Figures 1-3): role-branded split layout, credential validation against users_staff/roles.</summary>
    Public Class LoginForm
        Inherits Form

        Private cboRole As ComboBox
        Private txtUsername As TextBox
        Private txtPassword As TextBox
        Private lblError As Label
        Private btnSignIn As Button
        Private chkShowPassword As CheckBox

        Public Sub New()
            InitializeComponent()
        End Sub

        Private Sub InitializeComponent()
            Me.Text = "RHU Vinzons - Sign In"
            Me.ClientSize = New Size(1000, 620)
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.FormBorderStyle = FormBorderStyle.FixedSingle
            Me.MaximizeBox = False
            Me.Font = UiTheme.BaseFont
            Me.BackColor = Color.White

            ' ---- Left branding panel ----
            Dim leftPanel As New Panel With {
                .Dock = DockStyle.Left,
                .Width = 460,
                .BackColor = UiTheme.SidebarDark
            }
            Me.Controls.Add(leftPanel)

            Dim lblBadge As New Label With {
                .Text = "* REPUBLIC OF THE PHILIPPINES",
                .ForeColor = Color.FromArgb(180, 220, 200),
                .Font = New Font("Segoe UI", 8.5F),
                .AutoSize = True,
                .Location = New Point(48, 60)
            }
            leftPanel.Controls.Add(lblBadge)

            Dim lblTitle As New Label With {
                .Text = "Rural Health Unit" & vbCrLf & "Vinzons",
                .ForeColor = Color.White,
                .Font = New Font("Segoe UI", 24.0F, FontStyle.Bold),
                .AutoSize = True,
                .Location = New Point(46, 90)
            }
            leftPanel.Controls.Add(lblTitle)

            Dim lblSubtitle As New Label With {
                .Text = "Camarines Norte",
                .ForeColor = Color.FromArgb(180, 220, 200),
                .Font = New Font("Segoe UI", 10.0F),
                .AutoSize = True,
                .Location = New Point(48, 165)
            }
            leftPanel.Controls.Add(lblSubtitle)

            Dim pnlDesc As New Panel With {
                .Location = New Point(48, 230),
                .Size = New Size(360, 110),
                .BackColor = Color.FromArgb(30, 255, 255, 255)
            }
            leftPanel.Controls.Add(pnlDesc)
            pnlDesc.Controls.Add(New Label With {
                .Text = "Centralized Patient Health & Medical Records Management System",
                .ForeColor = Color.White,
                .Font = UiTheme.BoldFont,
                .Location = New Point(16, 12),
                .Size = New Size(330, 40)
            })
            pnlDesc.Controls.Add(New Label With {
                .Text = "A secure, centralized digital platform for organizing, retrieving, and managing patient health records for the community of Vinzons.",
                .ForeColor = Color.FromArgb(210, 235, 220),
                .Font = New Font("Segoe UI", 8.5F),
                .Location = New Point(16, 52),
                .Size = New Size(330, 50)
            })

            Dim lblFooter As New Label With {
                .Text = "Project C.A.R.E - Centralized Access to Records and Entries",
                .ForeColor = Color.FromArgb(150, 200, 180),
                .Font = New Font("Segoe UI", 8.0F),
                .AutoSize = True,
                .Location = New Point(48, 560)
            }
            leftPanel.Controls.Add(lblFooter)

            ' ---- Right sign-in panel ----
            Dim rightPanel As New Panel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.White,
                .Padding = New Padding(80, 70, 80, 40)
            }
            Me.Controls.Add(rightPanel)
            rightPanel.BringToFront()

            Dim lblSignIn As New Label With {
                .Text = "Sign in",
                .Font = UiTheme.HeaderFont,
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(0, 0)
            }
            rightPanel.Controls.Add(lblSignIn)

            Dim lblSignInSub As New Label With {
                .Text = "Access the Health Management Records System",
                .Font = UiTheme.BaseFont,
                .ForeColor = UiTheme.TextMuted,
                .AutoSize = True,
                .Location = New Point(0, 34)
            }
            rightPanel.Controls.Add(lblSignInSub)

            Dim y As Integer = 80

            rightPanel.Controls.Add(New Label With {.Text = "Role (demo)", .Font = UiTheme.BoldFont, .Location = New Point(0, y), .AutoSize = True})
            y += 22
            cboRole = New ComboBox With {
                .Location = New Point(0, y),
                .Size = New Size(360, 30),
                .DropDownStyle = ComboBoxStyle.DropDownList,
                .Font = UiTheme.BaseFont
            }
            AddHandler cboRole.SelectedIndexChanged, AddressOf CboRole_SelectedIndexChanged
            rightPanel.Controls.Add(cboRole)
            y += 46

            rightPanel.Controls.Add(New Label With {.Text = "Username", .Font = UiTheme.BoldFont, .Location = New Point(0, y), .AutoSize = True})
            y += 22
            txtUsername = New TextBox With {
                .Location = New Point(0, y),
                .Size = New Size(360, 30),
                .Font = UiTheme.BaseFont,
                .PlaceholderText = "e.g. healthcare"
            }
            rightPanel.Controls.Add(txtUsername)
            y += 46

            rightPanel.Controls.Add(New Label With {.Text = "Password", .Font = UiTheme.BoldFont, .Location = New Point(0, y), .AutoSize = True})
            y += 22
            txtPassword = New TextBox With {
                .Location = New Point(0, y),
                .Size = New Size(360, 30),
                .Font = UiTheme.BaseFont,
                .UseSystemPasswordChar = True
            }
            rightPanel.Controls.Add(txtPassword)

            chkShowPassword = New CheckBox With {
                .Text = "Show password",
                .Location = New Point(0, y + 34),
                .AutoSize = True,
                .Font = New Font("Segoe UI", 8.5F)
            }
            AddHandler chkShowPassword.CheckedChanged, Sub() txtPassword.UseSystemPasswordChar = Not chkShowPassword.Checked
            rightPanel.Controls.Add(chkShowPassword)
            y += 66

            lblError = New Label With {
                .Text = "",
                .ForeColor = UiTheme.DangerRed,
                .Font = New Font("Segoe UI", 8.75F),
                .Location = New Point(0, y),
                .Size = New Size(360, 36)
            }
            rightPanel.Controls.Add(lblError)
            y += 40

            btnSignIn = New Button With {
                .Text = "Sign In",
                .Location = New Point(0, y),
                .Size = New Size(360, 40)
            }
            UiTheme.StylePrimaryButton(btnSignIn)
            AddHandler btnSignIn.Click, AddressOf BtnSignIn_Click
            rightPanel.Controls.Add(btnSignIn)
            Me.AcceptButton = btnSignIn
            y += 56

            rightPanel.Controls.Add(New Label With {
                .Text = "Secured system - Authorized RHU personnel only",
                .ForeColor = UiTheme.TextMuted,
                .Font = New Font("Segoe UI", 8.0F),
                .Location = New Point(0, y),
                .AutoSize = True
            })

            AddHandler Me.Shown, AddressOf LoginForm_Shown
        End Sub

        Private Sub LoginForm_Shown(sender As Object, e As EventArgs)
            LoadRoles()
            txtUsername.Focus()
        End Sub

        Private Sub LoadRoles()
            Try
                cboRole.Items.Clear()
                cboRole.Items.Add("-- Select a role to preview --")
                Dim table = Db.Query("SELECT role_name FROM roles WHERE status = 'Active' ORDER BY role_id")
                For Each row As DataRow In table.Rows
                    cboRole.Items.Add(row("role_name").ToString())
                Next
                cboRole.SelectedIndex = 0
            Catch ex As Exception
                lblError.Text = "Cannot reach the database. Is MySQL/XAMPP running?" & vbCrLf & ex.Message
            End Try
        End Sub

        ''' <summary>Convenience only: fills in that role's demo username so a reviewer can pick a role instead of remembering usernames. Password is never auto-filled.</summary>
        Private Sub CboRole_SelectedIndexChanged(sender As Object, e As EventArgs)
            If cboRole.SelectedIndex <= 0 Then Return
            Try
                Dim username = Db.QueryScalar(
                    "SELECT u.username FROM users_staff u JOIN roles r ON r.role_id = u.role_id
                     WHERE r.role_name = @RoleName AND u.status = 'Active' ORDER BY u.user_id LIMIT 1",
                    New With {.RoleName = cboRole.Text})
                If username IsNot Nothing Then
                    txtUsername.Text = username.ToString()
                    txtPassword.Clear()
                    txtPassword.Focus()
                End If
            Catch
                ' non-fatal -- the dropdown is just a convenience, not required to sign in
            End Try
        End Sub

        Private Sub BtnSignIn_Click(sender As Object, e As EventArgs)
            lblError.Text = ""

            If String.IsNullOrWhiteSpace(txtUsername.Text) Then
                lblError.Text = "Username is required."
                Return
            End If
            If String.IsNullOrWhiteSpace(txtPassword.Text) Then
                lblError.Text = "Password is required."
                Return
            End If

            Dim connError As String = Nothing
            If Not Db.CanConnect(connError) Then
                lblError.Text = "Cannot connect to the database. Start MySQL in XAMPP and try again." & vbCrLf & connError
                Return
            End If

            Try
                Dim table = Db.Query(
                    "SELECT u.user_id, u.full_name, u.username, u.password_hash, u.status,
                            r.role_id, r.role_name, r.module_access, r.can_add, r.can_edit, r.can_delete, r.can_view
                     FROM users_staff u
                     JOIN roles r ON r.role_id = u.role_id
                     WHERE u.username = @Username",
                    New With {.Username = txtUsername.Text.Trim()})

                If table.Rows.Count = 0 Then
                    lblError.Text = "Invalid username or password."
                    AuditLogger.Log("LOGIN_FAILED", "auth", txtUsername.Text.Trim(), "Unknown username", "Failed")
                    Return
                End If

                Dim row = table.Rows(0)
                If row("status").ToString() <> "Active" Then
                    lblError.Text = "This account is inactive. Contact the System Administrator."
                    Return
                End If

                If Not PasswordHasher.VerifyPassword(txtPassword.Text, row("password_hash").ToString()) Then
                    lblError.Text = "Invalid username or password."
                    AuditLogger.Log("LOGIN_FAILED", "auth", txtUsername.Text.Trim(), "Bad password", "Failed")
                    Return
                End If

                Session.Current = New CurrentUser With {
                    .UserId = Convert.ToInt32(row("user_id")),
                    .FullName = row("full_name").ToString(),
                    .Username = row("username").ToString(),
                    .RoleId = Convert.ToInt32(row("role_id")),
                    .RoleName = row("role_name").ToString(),
                    .ModuleAccess = row("module_access").ToString(),
                    .CanAdd = Convert.ToBoolean(row("can_add")),
                    .CanEdit = Convert.ToBoolean(row("can_edit")),
                    .CanDelete = Convert.ToBoolean(row("can_delete")),
                    .CanView = Convert.ToBoolean(row("can_view"))
                }

                Db.Execute("UPDATE users_staff SET last_login = NOW() WHERE user_id = @UserId", New With {.UserId = Session.Current.UserId})
                AuditLogger.Log("LOGIN", "auth", Session.Current.Username, $"{Session.Current.FullName} signed in")

                Me.Hide()
                Using dash As New MainDashboardForm()
                    dash.ShowDialog()
                End Using

                Session.Current = Nothing
                txtPassword.Clear()
                lblError.Text = ""
                Me.Show()
                txtUsername.Focus()

            Catch ex As Exception
                lblError.Text = "Sign-in failed: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
