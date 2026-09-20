Imports System.Drawing
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>Generic "not built in this phase yet" screen for sidebar items outside the current build scope.</summary>
    Public Class PlaceholderForm
        Inherits Form

        Public Sub New(title As String, message As String)
            Me.Text = title
            Me.BackColor = UiTheme.BackgroundLight
            Me.Font = UiTheme.BaseFont

            Dim lblIcon As New Label With {
                .Text = "()",
                .Font = New Font("Segoe UI", 28.0F),
                .ForeColor = UiTheme.TextMuted,
                .AutoSize = True,
                .Location = New Point(48, 60)
            }
            Me.Controls.Add(lblIcon)

            Dim lblTitle As New Label With {
                .Text = title,
                .Font = UiTheme.HeaderFont,
                .ForeColor = UiTheme.TextDark,
                .AutoSize = True,
                .Location = New Point(48, 110)
            }
            Me.Controls.Add(lblTitle)

            Dim lblMessage As New Label With {
                .Text = message,
                .Font = UiTheme.BaseFont,
                .ForeColor = UiTheme.TextMuted,
                .AutoSize = True,
                .MaximumSize = New Size(520, 0),
                .Location = New Point(48, 150)
            }
            Me.Controls.Add(lblMessage)
        End Sub

    End Class

End Namespace
