Imports System.Drawing
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>Small modal prompt: one (optionally required) value with optional suggestions, plus an optional free-text remarks box.</summary>
    Public Class PromptDialog
        Inherits Form

        Private ReadOnly cboValue As ComboBox
        Private ReadOnly txtRemarks As TextBox
        Private ReadOnly lblError As Label
        Private ReadOnly required As Boolean

        Public ReadOnly Property Value As String
            Get
                Return cboValue.Text.Trim()
            End Get
        End Property

        Public ReadOnly Property Remarks As String
            Get
                Return If(txtRemarks Is Nothing, "", txtRemarks.Text.Trim())
            End Get
        End Property

        Public Sub New(title As String, heading As String, fieldLabel As String, okText As String,
                       Optional suggestions As IEnumerable(Of String) = Nothing,
                       Optional required As Boolean = True,
                       Optional remarksLabel As String = Nothing)
            Me.required = required
            Me.Text = title
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = heading, .Font = UiTheme.SubHeaderFont, .AutoSize = True, .MaximumSize = New Size(440, 0), .Location = New Point(20, 16)})

            Dim y = 56
            Me.Controls.Add(New Label With {.Text = fieldLabel, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
            cboValue = New ComboBox With {.Location = New Point(20, y + 20), .Width = 440, .DropDownStyle = ComboBoxStyle.DropDown}
            If suggestions IsNot Nothing Then
                For Each s In suggestions
                    cboValue.Items.Add(s)
                Next
                cboValue.AutoCompleteMode = AutoCompleteMode.SuggestAppend
                cboValue.AutoCompleteSource = AutoCompleteSource.ListItems
            End If
            Me.Controls.Add(cboValue)
            y += 60

            If remarksLabel IsNot Nothing Then
                Me.Controls.Add(New Label With {.Text = remarksLabel, .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, y)})
                txtRemarks = New TextBox With {.Location = New Point(20, y + 20), .Width = 440, .Height = 60, .Multiline = True}
                Me.Controls.Add(txtRemarks)
                y += 92
            End If

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, y)}
            Me.Controls.Add(lblError)
            y += 24

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(220, y)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnOk As New Button With {.Text = okText, .Size = New Size(130, 36), .Location = New Point(330, y)}
            UiTheme.StylePrimaryButton(btnOk)
            AddHandler btnOk.Click, AddressOf BtnOk_Click
            Me.Controls.Add(btnOk)

            Me.AcceptButton = btnOk
            Me.CancelButton = btnCancel
            Me.ClientSize = New Size(484, y + 56)
        End Sub

        Private Sub BtnOk_Click(sender As Object, e As EventArgs)
            If required AndAlso Value.Length = 0 Then
                lblError.Text = "This field is required."
                Return
            End If
            Me.DialogResult = DialogResult.OK
        End Sub

    End Class

End Namespace
