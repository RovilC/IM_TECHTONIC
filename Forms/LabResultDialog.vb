Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data
Imports TechTonic_IM_RHU.Models

Namespace Forms

    ''' <summary>Enter/release a laboratory result for one request row.</summary>
    Public Class LabResultDialog
        Inherits Form

        Private ReadOnly laboratoryId As Integer
        Private txtResult As TextBox
        Private cboStatus As ComboBox
        Private lblError As Label
        Public Saved As Boolean = False

        Public Sub New(laboratoryId As Integer, patientName As String, testName As String, existingResult As String, existingStatus As String)
            Me.laboratoryId = laboratoryId
            Me.Text = "Laboratory Result"
            Me.Size = New Size(520, 400)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = testName, .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})
            Me.Controls.Add(New Label With {.Text = patientName, .ForeColor = UiTheme.TextMuted, .AutoSize = True, .Location = New Point(20, 44)})

            Me.Controls.Add(New Label With {.Text = "Result / Findings", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 80)})
            txtResult = New TextBox With {.Location = New Point(20, 100), .Width = 460, .Height = 140, .Multiline = True, .Text = existingResult}
            Me.Controls.Add(txtResult)

            Me.Controls.Add(New Label With {.Text = "Status", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 254)})
            cboStatus = New ComboBox With {.Location = New Point(20, 274), .Width = 200, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboStatus.Items.AddRange({"Pending", "In Progress", "Released"})
            cboStatus.SelectedItem = If(String.IsNullOrWhiteSpace(existingStatus), "In Progress", existingStatus)
            If cboStatus.SelectedIndex < 0 Then cboStatus.SelectedIndex = 1
            Me.Controls.Add(cboStatus)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 310), .MaximumSize = New Size(400, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(270, 340)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Save Result", .Size = New Size(150, 36), .Location = New Point(380, 340)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If cboStatus.Text = "Released" AndAlso String.IsNullOrWhiteSpace(txtResult.Text) Then
                lblError.Text = "Enter a result before releasing."
                Return
            End If
            Try
                Dim resultDate As Object = If(cboStatus.Text = "Released", CType(DateTime.Now, Object), DBNull.Value)
                Dim verifiedBy As Object = If(cboStatus.Text = "Released", CType(Session.Current.UserId, Object), DBNull.Value)
                Db.Execute(
                    "UPDATE laboratory SET result = @Result, status = @Status, result_date = @ResultDate, verified_by = @VerifiedBy WHERE laboratory_id = @Id",
                    New With {
                        .Result = If(String.IsNullOrWhiteSpace(txtResult.Text), CType(DBNull.Value, Object), txtResult.Text.Trim()),
                        .Status = cboStatus.Text, .ResultDate = resultDate, .VerifiedBy = verifiedBy, .Id = laboratoryId
                    })
                AuditLogger.Log("UPDATE", "lab", $"Lab request #{laboratoryId}", $"Result set to {cboStatus.Text}")
                Saved = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not save result: " & ex.Message
            End Try
        End Sub

    End Class

End Namespace
