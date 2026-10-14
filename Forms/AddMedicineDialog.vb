Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Adds a new medicine to the catalog (medicines table).</summary>
    Public Class AddMedicineDialog
        Inherits Form

        Private txtName, txtBrand As TextBox
        Private cboCategory, cboUnit As ComboBox
        Private numMinStock As NumericUpDown
        Private lblError As Label
        Public Saved As Boolean = False

        Public Sub New()
            Me.Text = "Add Medicine"
            Me.Size = New Size(440, 340)
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False : Me.MinimizeBox = False
            Me.Font = UiTheme.BaseFont

            Me.Controls.Add(New Label With {.Text = "Add Medicine to Catalog", .Font = UiTheme.SubHeaderFont, .AutoSize = True, .Location = New Point(20, 16)})

            Me.Controls.Add(New Label With {.Text = "Medicine Name *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 56)})
            txtName = New TextBox With {.Location = New Point(20, 76), .Width = 380}
            Me.Controls.Add(txtName)

            Me.Controls.Add(New Label With {.Text = "Category", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 112)})
            cboCategory = New ComboBox With {.Location = New Point(20, 132), .Width = 180, .DropDownStyle = ComboBoxStyle.DropDown}
            Me.Controls.Add(cboCategory)

            Me.Controls.Add(New Label With {.Text = "Brand", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(220, 112)})
            txtBrand = New TextBox With {.Location = New Point(220, 132), .Width = 180}
            Me.Controls.Add(txtBrand)

            Me.Controls.Add(New Label With {.Text = "Unit *", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(20, 168)})
            cboUnit = New ComboBox With {.Location = New Point(20, 188), .Width = 180, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboUnit.Items.AddRange(MedicineCatalog.Units)
            cboUnit.SelectedItem = "tablet"
            Me.Controls.Add(cboUnit)

            Me.Controls.Add(New Label With {.Text = "Minimum Stock Level", .Font = UiTheme.BoldFont, .AutoSize = True, .Location = New Point(220, 168)})
            numMinStock = New NumericUpDown With {.Location = New Point(220, 188), .Width = 120, .Minimum = 10, .Maximum = 1000000, .Increment = 10, .Value = 100}
            Me.Controls.Add(numMinStock)

            lblError = New Label With {.ForeColor = UiTheme.DangerRed, .AutoSize = True, .Location = New Point(20, 224), .MaximumSize = New Size(380, 0)}
            Me.Controls.Add(lblError)

            Dim btnCancel As New Button With {.Text = "Cancel", .Size = New Size(100, 36), .Location = New Point(190, 254)}
            UiTheme.StyleSecondaryButton(btnCancel)
            AddHandler btnCancel.Click, Sub() Me.DialogResult = DialogResult.Cancel
            Me.Controls.Add(btnCancel)

            Dim btnSave As New Button With {.Text = "Save", .Size = New Size(110, 36), .Location = New Point(300, 254)}
            UiTheme.StylePrimaryButton(btnSave)
            AddHandler btnSave.Click, AddressOf BtnSave_Click
            Me.Controls.Add(btnSave)

            LoadSuggestions()
            AddHandler txtName.Leave, AddressOf TxtName_Leave
            UiTheme.GuardUnsavedClose(Me)
        End Sub

        ''' <summary>
        ''' Seeds name/category/brand autocomplete from the static
        ''' MedicineCatalog plus whatever's already in the medicines table,
        ''' so suggestions cover both the common RHU formulary and anything
        ''' staff previously typed in. Falls back to the static catalog
        ''' alone if the database isn't reachable yet -- the dialog should
        ''' still be usable, just without the extra DB-sourced suggestions.
        ''' </summary>
        Private Sub LoadSuggestions()
            Dim names = MedicineCatalog.Entries.Select(Function(e) e.Name).ToList()
            Dim categories = MedicineCatalog.Entries.Select(Function(e) e.Category).ToList()
            Dim brands = MedicineCatalog.Entries.SelectMany(Function(e) e.Brands).ToList()

            Try
                For Each row As DataRow In Db.Query("SELECT DISTINCT medicine_name FROM medicines ORDER BY medicine_name").Rows
                    names.Add(row(0).ToString())
                Next
                For Each row As DataRow In Db.Query("SELECT DISTINCT category FROM medicines WHERE category IS NOT NULL AND category <> '' ORDER BY category").Rows
                    categories.Add(row(0).ToString())
                Next
                For Each row As DataRow In Db.Query("SELECT DISTINCT brand FROM medicines WHERE brand IS NOT NULL AND brand <> '' ORDER BY brand").Rows
                    brands.Add(row(0).ToString())
                Next
            Catch
                ' database not reachable yet -- suggestions just fall back to the static catalog
            End Try

            names = names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(n) n).ToList()
            categories = categories.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(c) c).ToList()
            brands = brands.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(b) b).ToList()

            txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            txtName.AutoCompleteSource = AutoCompleteSource.CustomSource
            Dim nameSource As New AutoCompleteStringCollection()
            nameSource.AddRange(names.ToArray())
            txtName.AutoCompleteCustomSource = nameSource

            cboCategory.Items.AddRange(categories.ToArray())
            cboCategory.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            cboCategory.AutoCompleteSource = AutoCompleteSource.ListItems

            txtBrand.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            txtBrand.AutoCompleteSource = AutoCompleteSource.CustomSource
            Dim brandSource As New AutoCompleteStringCollection()
            brandSource.AddRange(brands.ToArray())
            txtBrand.AutoCompleteCustomSource = brandSource
        End Sub

        ''' <summary>When the typed name matches a known catalog entry, narrow the brand suggestions to that medicine's own brands and fill in category/unit if the user hasn't already set them.</summary>
        Private Sub TxtName_Leave(sender As Object, e As EventArgs)
            Dim entry = MedicineCatalog.Entries.FirstOrDefault(
                Function(en) en.Name.Equals(txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            If entry Is Nothing Then Return

            If String.IsNullOrWhiteSpace(cboCategory.Text) Then cboCategory.Text = entry.Category
            If cboUnit.Items.Contains(entry.Unit) Then cboUnit.SelectedItem = entry.Unit
            If entry.Brands.Length > 0 Then
                Dim src As New AutoCompleteStringCollection()
                src.AddRange(entry.Brands)
                txtBrand.AutoCompleteCustomSource = src
            End If
        End Sub

        Private Sub BtnSave_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtName.Text) Then
                lblError.Text = "Medicine name is required."
                Return
            End If
            If Not UiTheme.Confirm(Me, $"Add ""{txtName.Text.Trim()}"" to the medicine list?", "Add Medicine") Then Return
            Try
                Db.Execute(
                    "INSERT INTO medicines (medicine_name, category, brand, unit, minimum_stock_level) VALUES (@Name, @Category, @Brand, @Unit, @MinStock)",
                    New With {
                        .Name = txtName.Text.Trim(),
                        .Category = If(String.IsNullOrWhiteSpace(cboCategory.Text), CType(DBNull.Value, Object), cboCategory.Text.Trim()),
                        .Brand = If(String.IsNullOrWhiteSpace(txtBrand.Text), CType(DBNull.Value, Object), txtBrand.Text.Trim()),
                        .Unit = If(cboUnit.SelectedItem Is Nothing, CType(DBNull.Value, Object), cboUnit.SelectedItem.ToString()),
                        .MinStock = CInt(numMinStock.Value)
                    })
                AuditLogger.Log("CREATE", "inventory", txtName.Text.Trim(), "Added medicine to catalog")
                Saved = True
                Me.DialogResult = DialogResult.OK
            Catch ex As Exception
                lblError.Text = "Could not save: " & ex.Message & If(ex.Message.Contains("Duplicate"), " (this medicine may already exist)", "")
            End Try
        End Sub

    End Class

End Namespace
