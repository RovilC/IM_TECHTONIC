Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>
    ''' Visual-only restyle of MedicineInventoryForm. Called once, right after InitializeComponent(). Nothing moves:
    ''' the toolbar, the split (and its distances), the labels, the column order/widths and the buttons keep their
    ''' positions; they are only wrapped in one card and restyled. The shared UiTheme.StyleGrid is left alone.
    ''' </summary>
    Friend Module InventoryTheme

        Private ReadOnly DangerSoft As Color = ColorTranslator.FromHtml("#FDECEC")
        Private ReadOnly LabelBold As New Font("Segoe UI", 11.25F, FontStyle.Bold)
        Private ReadOnly BodyFont As New Font("Segoe UI", 9.5F)
        Private ReadOnly BoldBody As New Font("Segoe UI", 9.5F, FontStyle.Bold)
        Private ReadOnly HeaderFont As New Font("Segoe UI", 8.25F, FontStyle.Bold)

        Public Sub ApplyMedicineInventory(form As Form, gridMedicines As DataGridView, gridBatches As DataGridView,
                                          lblSelected As Label, btnReceiveStock As Button)
            Dim split = form.Controls.OfType(Of SplitContainer)().First()
            Dim toolbar = form.Controls.OfType(Of Panel)().First(Function(p) p.Controls.OfType(Of Button)().Any())
            Dim lblMedicines = split.Panel1.Controls.OfType(Of Label)().First()

            ' ---- one white card on the mint background around the existing content ----
            form.BackColor = RolesPermissionsTheme.Background
            form.Padding = New Padding(20)
            Dim card As New Panel With {.Dock = DockStyle.Fill}
            RolesPermissionsTheme.MakeCard(card)
            card.Padding = New Padding(16)
            form.Controls.Add(card)
            card.Controls.Add(split)          ' Fill control added first, then the Top toolbar above it
            card.Controls.Add(toolbar)
            split.BringToFront()

            ' ---- the two title labels (position/dock unchanged) ----
            For Each l In {lblMedicines, lblSelected}
                l.Font = LabelBold
                l.ForeColor = ClinicalUi.TextMain
                l.UseMnemonic = False
            Next

            ' ---- stock batches grid: rounded 1px outline wrapper + the card table style. The Medicine Catalog grid keeps its
            '      original design (UiTheme.StyleGrid, no wrapper); only its Status text is painted as a coloured pill. ----
            WrapGrid(split.Panel2, gridBatches)
            StyleGrid(gridBatches)
            AddHandler gridBatches.DataBindingComplete, Sub() BoldColumn(gridBatches, "Remaining")
            AddHandler gridMedicines.CellPainting, AddressOf PaintStatusPill

            ' ---- "+ Receive Stock": stays full-width under the batches grid, now with a 10px gap above it and 6px corners ----
            Dim gap As New Panel With {.Dock = DockStyle.Bottom, .Height = 10, .BackColor = Color.White}
            split.Panel2.Controls.Add(gap)
            split.Panel2.Controls.SetChildIndex(gap, split.Panel2.Controls.GetChildIndex(btnReceiveStock))   ' docks just above the button
            RoundCorners(btnReceiveStock, 6)
        End Sub

        ' ------------------------------------------------------------------ wrappers

        Private Sub WrapGrid(parent As Control, grid As DataGridView)
            Dim wrapper As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(2)}
            ClinicalUi.SetDoubleBuffered(wrapper)
            parent.Controls.Add(wrapper)
            wrapper.Controls.Add(grid)
            grid.Dock = DockStyle.Fill
            wrapper.BringToFront()            ' the Fill control must dock last
            AddHandler wrapper.Paint,
                Sub(s, e)
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                    Using path = RoundedPath(New Rectangle(0, 0, wrapper.Width - 1, wrapper.Height - 1), 8)
                        Using pen As New Pen(RolesPermissionsTheme.Border)
                            e.Graphics.DrawPath(pen, path)
                        End Using
                    End Using
                End Sub
        End Sub

        Private Sub RoundCorners(btn As Button, radius As Integer)
            Dim apply =
                Sub()
                    If btn.Width <= 0 OrElse btn.Height <= 0 Then Return
                    Dim old = btn.Region
                    Using path = RoundedPath(New Rectangle(0, 0, btn.Width, btn.Height), radius)
                        btn.Region = New Region(path)
                    End Using
                    If old IsNot Nothing Then old.Dispose()
                End Sub
            AddHandler btn.SizeChanged, Sub() apply()
            apply()
        End Sub

        Private Function RoundedPath(r As Rectangle, radius As Integer) As GraphicsPath
            Dim d = radius * 2
            Dim path As New GraphicsPath()
            path.AddArc(r.X, r.Y, d, d, 180, 90)
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function

        ' ------------------------------------------------------------------ grids

        ''' <summary>White header, no blue header cell, horizontal row lines only, soft-green selection, 36px rows. Widths/sizing modes untouched.</summary>
        Private Sub StyleGrid(grid As DataGridView)
            grid.BackgroundColor = Color.White
            grid.BorderStyle = BorderStyle.None
            grid.EnableHeadersVisualStyles = False
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            grid.ColumnHeadersHeight = 40
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            With grid.ColumnHeadersDefaultCellStyle
                .BackColor = Color.White
                .ForeColor = RolesPermissionsTheme.TextMuted
                .Font = HeaderFont
                .SelectionBackColor = Color.White      ' the header never highlights
                .SelectionForeColor = RolesPermissionsTheme.TextMuted
                .Padding = New Padding(8, 0, 0, 0)
            End With
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            grid.GridColor = RolesPermissionsTheme.Border
            grid.RowTemplate.Height = 36
            With grid.DefaultCellStyle
                .Font = BodyFont
                .ForeColor = RolesPermissionsTheme.TextColor
                .Padding = New Padding(8, 0, 0, 0)
                .SelectionBackColor = RolesPermissionsTheme.PrimarySoft
                .SelectionForeColor = RolesPermissionsTheme.Primary
            End With
            ClinicalUi.SetDoubleBuffered(grid)
        End Sub

        Private Sub BoldColumn(grid As DataGridView, name As String)
            If grid.Columns.Contains(name) Then grid.Columns(name).DefaultCellStyle.Font = BoldBody
        End Sub

        ''' <summary>Status drawn as a pill from the cell's existing text (Sufficient / Low Stock / Out of Stock); the value itself is untouched.</summary>
        Private Sub PaintStatusPill(sender As Object, e As DataGridViewCellPaintingEventArgs)
            Dim grid = DirectCast(sender, DataGridView)
            If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
            If grid.Columns(e.ColumnIndex).Name <> "Status" Then Return

            Dim text = Convert.ToString(e.FormattedValue)
            e.Paint(e.CellBounds, e.PaintParts And Not DataGridViewPaintParts.ContentForeground)
            If text.Length > 0 Then
                Dim fill As Color, ink As Color
                Select Case text
                    Case "Out of Stock" : fill = DangerSoft : ink = ClinicalUi.DangerText
                    Case "Low Stock" : fill = ClinicalUi.WarnBg : ink = ClinicalUi.WarnText
                    Case Else : fill = RolesPermissionsTheme.PrimarySoft : ink = RolesPermissionsTheme.Primary
                End Select
                ' On the selected (soft-green) row a green pill would vanish, so it turns white there.
                If (e.State And DataGridViewElementStates.Selected) <> 0 AndAlso fill = RolesPermissionsTheme.PrimarySoft Then fill = Color.White

                Dim textSize = TextRenderer.MeasureText(e.Graphics, text, HeaderFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding)
                Dim pillWidth = Math.Min(textSize.Width + 20, e.CellBounds.Width - 10)   ' the catalog keeps its original column width, so the pill shrinks to fit
                Dim pill As New Rectangle(e.CellBounds.X + 4, e.CellBounds.Y + (e.CellBounds.Height - 22) \ 2, pillWidth, 22)
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                Using path = RoundedPath(pill, 10)
                    Using b As New SolidBrush(fill)
                        e.Graphics.FillPath(b, path)
                    End Using
                End Using
                TextRenderer.DrawText(e.Graphics, text, HeaderFont, pill, ink,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            End If
            e.Handled = True
        End Sub

    End Module

End Namespace
