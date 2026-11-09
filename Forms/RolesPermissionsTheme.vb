Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>
    ''' Visual-only restyle of RolesPermissionsForm: call ApplyRolePermissions(Me) once, right after
    ''' InitializeComponent(). Controls keep their names, text, handlers and data logic; they are only
    ''' re-parented into cards / flow panels and restyled. (Separate from the shared UiTheme module so
    ''' the rest of the app's look is untouched.)
    ''' </summary>
    Friend Module RolesPermissionsTheme
        ' Palette (same values as the clinical design system).
        Public ReadOnly Primary As Color = ColorTranslator.FromHtml("#1F5F3F")
        Public ReadOnly PrimaryHover As Color = ColorTranslator.FromHtml("#174A31")
        Public ReadOnly PrimarySoft As Color = ColorTranslator.FromHtml("#E8F3EC")
        Public ReadOnly Background As Color = ColorTranslator.FromHtml("#F3F7F4")
        Public ReadOnly CardColor As Color = Color.White
        Public ReadOnly Border As Color = ColorTranslator.FromHtml("#E3E8E5")
        Public ReadOnly InputBorder As Color = ColorTranslator.FromHtml("#D9DEDB")
        Public ReadOnly TextColor As Color = ColorTranslator.FromHtml("#1A1F1C")
        Public ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#6B7280")
        Public ReadOnly Placeholder As Color = ColorTranslator.FromHtml("#9CA3AF")
        Private ReadOnly DisabledPrimary As Color = Color.FromArgb(176, 196, 184)

        Private ReadOnly BodyFont As New Font("Segoe UI", 9.75F)
        Private ReadOnly SectionFont As New Font("Segoe UI", 9.75F, FontStyle.Bold)
        Private ReadOnly TitleFont As New Font("Segoe UI", 14.25F, FontStyle.Bold)
        Private ReadOnly HintFont As New Font("Segoe UI", 8.25F)
        Private ReadOnly ChipFont As New Font("Segoe UI", 9.0F)
        Private ReadOnly ChipBoldFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)

        ' ------------------------------------------------------------------ entry point

        Public Sub ApplyRolePermissions(form As Form)
            ' ---- 1. locate the existing controls (nothing is created that wasn't already there) ----
            Dim split = form.Controls.OfType(Of SplitContainer)().First()
            Dim toolbar = form.Controls.OfType(Of Panel)().First(Function(p) p.Controls.OfType(Of Button)().Any())
            Dim btnNew = toolbar.Controls.OfType(Of Button)().First()
            Dim grid = split.Panel1.Controls.OfType(Of DataGridView)().First()
            Dim editor = split.Panel2.Controls.OfType(Of Panel)().First()

            Dim editorControls = editor.Controls.Cast(Of Control)().ToList()
            Dim labels = editorControls.OfType(Of Label)().ToList()
            Dim lblSelected = labels.First()
            Dim lblDescription = labels.First(Function(l) l.Text = "Description")
            Dim lblStatus = labels.First(Function(l) l.Text = "Status")
            Dim lblRecord = labels.First(Function(l) l.Text = "Record Permissions")
            Dim lblModule = labels.First(Function(l) l.Text = "Module Access")
            Dim lblHint = labels.First(Function(l) l.Text.StartsWith("Changes take effect"))
            Dim txtDescription = editorControls.OfType(Of TextBox)().First(Function(t) t.Multiline)
            Dim txtStatus = editorControls.OfType(Of TextBox)().First(Function(t) Not t.Multiline)
            Dim checks = editorControls.OfType(Of CheckBox)().ToList()
            Dim recordChecks = checks.Take(4).ToList()      ' View / Add / Edit / Delete
            Dim moduleChecks = checks.Skip(4).ToList()
            Dim btnSave = editorControls.OfType(Of Button)().First()

            ' "&" in text must display literally (e.g. "Roles & Permissions"): never treat it as a mnemonic.
            DisableMnemonics(form)

            ' ---- 2. form ----
            form.BackColor = Background
            form.Font = BodyFont
            form.Padding = New Padding(20)
            form.MinimumSize = New Size(1000, 620)

            ' ---- 3. two-column layout (40% / 60%) with a 16px gap ----
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Background}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim leftCard As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0)}
            Dim rightCard As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0)}
            MakeCard(leftCard)
            MakeCard(rightCard)
            layout.Controls.Add(leftCard, 0, 0)
            layout.Controls.Add(rightCard, 1, 0)
            form.Controls.Add(layout)

            ' ---- 4. left card: "+ New Role" on top, roles grid below ----
            Dim headerRow As New Panel With {.Dock = DockStyle.Top, .Height = 38, .BackColor = CardColor}
            Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 12, .BackColor = CardColor}
            leftCard.Controls.Add(gap)
            leftCard.Controls.Add(headerRow)     ' later-added Top controls dock higher
            headerRow.Controls.Add(btnNew)
            btnNew.Location = New Point(0, 0)
            StylePrimaryButton(btnNew)
            leftCard.Controls.Add(grid)
            grid.Dock = DockStyle.Fill
            grid.BringToFront()   ' the grid keeps its original table design (UiTheme.StyleGrid)

            ' ---- 5. right card: footer (divider + Save) and the scrolling editor flow ----
            Dim footer As New Panel With {.Dock = DockStyle.Bottom, .Height = 62, .BackColor = CardColor}
            ClinicalUi.SetDoubleBuffered(footer)
            AddHandler footer.Paint,
                Sub(s, e)
                    Using pen As New Pen(Border)
                        e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0)
                    End Using
                End Sub
            rightCard.Controls.Add(footer)
            footer.Controls.Add(btnSave)
            StylePrimaryButton(btnSave)
            btnSave.Width = 140
            UiTheme.PinToRight(btnSave, footer, 0, 14)

            Dim flow As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown, .WrapContents = False,
                .AutoScroll = True, .BackColor = CardColor
            }
            rightCard.Controls.Add(flow)
            flow.BringToFront()

            ' Title
            lblSelected.Font = TitleFont
            lblSelected.ForeColor = TextColor
            lblSelected.AutoSize = True
            lblSelected.Margin = New Padding(0, 0, 0, 14)
            flow.Controls.Add(lblSelected)

            ' Description (bordered box around the text box)
            StyleSectionLabel(lblDescription)
            flow.Controls.Add(lblDescription)
            Dim descBox As Panel = WrapInputBox(txtDescription, 70, True)
            flow.Controls.Add(descBox)

            ' Status (reference only -- the control stays a read-only text box)
            StyleSectionLabel(lblStatus)
            flow.Controls.Add(lblStatus)
            Dim statusBox As Panel = WrapInputBox(txtStatus, 38, False)
            statusBox.Width = 220
            flow.Controls.Add(statusBox)

            ' Record permission chips
            StyleSectionLabel(lblRecord)
            flow.Controls.Add(lblRecord)
            Dim recordFlow As FlowLayoutPanel = NewChipFlow()
            For Each chk In recordChecks
                StyleChip(chk)
                recordFlow.Controls.Add(chk)
            Next
            flow.Controls.Add(recordFlow)

            ' Module access chips (wrap, each chip sized to its own text so long labels never overlap)
            StyleSectionLabel(lblModule)
            flow.Controls.Add(lblModule)
            Dim moduleFlow As FlowLayoutPanel = NewChipFlow()
            For Each chk In moduleChecks
                StyleChip(chk)
                moduleFlow.Controls.Add(chk)
            Next
            flow.Controls.Add(moduleFlow)

            ' Hint
            lblHint.Font = HintFont
            lblHint.ForeColor = TextMuted
            lblHint.AutoSize = True
            lblHint.Margin = New Padding(0, 14, 0, 0)
            flow.Controls.Add(lblHint)

            ' Keep the full-width children matched to the card width (also when a scrollbar appears).
            Dim fit =
                Sub()
                    Dim avail = Math.Max(200, flow.ClientSize.Width - 4)
                    descBox.Width = avail
                    recordFlow.MinimumSize = New Size(avail, 0)
                    recordFlow.MaximumSize = New Size(avail, 0)
                    moduleFlow.MinimumSize = New Size(avail, 0)
                    moduleFlow.MaximumSize = New Size(avail, 0)
                    lblHint.MaximumSize = New Size(avail, 0)
                    lblSelected.MaximumSize = New Size(avail, 0)
                End Sub
            AddHandler flow.SizeChanged, Sub() fit()
            fit()

            ' ---- 6. the old containers are empty now ----
            form.Controls.Remove(split)
            form.Controls.Remove(toolbar)
            editor.Dispose()
            split.Dispose()
            toolbar.Dispose()
        End Sub

        ' ------------------------------------------------------------------ cards / boxes

        ''' <summary>White panel with 18px padding and a 1px rounded (10px) border; the corners outside the curve take the parent's colour.</summary>
        Public Sub MakeCard(card As Panel)
            card.BackColor = CardColor
            card.Padding = New Padding(18)
            ClinicalUi.SetDoubleBuffered(card)
            AddHandler card.Paint,
                Sub(s, e)
                    Dim g = e.Graphics
                    g.SmoothingMode = SmoothingMode.AntiAlias
                    Using path = RoundedPath(New Rectangle(0, 0, card.Width - 1, card.Height - 1), 10)
                        Using region As New Region(New Rectangle(0, 0, card.Width, card.Height))
                            region.Exclude(path)
                            Using b As New SolidBrush(If(card.Parent IsNot Nothing, card.Parent.BackColor, Background))
                                g.FillRegion(b, region)
                            End Using
                        End Using
                        Using pen As New Pen(Border)
                            g.DrawPath(pen, path)
                        End Using
                    End Using
                End Sub
        End Sub

        ''' <summary>Puts an existing TextBox inside a white box with a 1px rounded InputBorder (the TextBox itself becomes borderless).</summary>
        Private Function WrapInputBox(tb As TextBox, height As Integer, multiline As Boolean) As Panel
            Dim box As New Panel With {.Height = height, .BackColor = Color.White, .Margin = New Padding(0), .Padding = New Padding(10, If(multiline, 10, 9), 10, 0)}
            ClinicalUi.SetDoubleBuffered(box)
            tb.BorderStyle = BorderStyle.None
            tb.BackColor = Color.White
            tb.Font = BodyFont
            tb.ForeColor = TextColor
            tb.Dock = If(multiline, DockStyle.Fill, DockStyle.Top)
            box.Controls.Add(tb)
            AddHandler box.Paint,
                Sub(s, e)
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                    Using path = RoundedPath(New Rectangle(0, 0, box.Width - 1, box.Height - 1), 8)
                        Using pen As New Pen(If(tb.Focused, Primary, InputBorder))
                            e.Graphics.DrawPath(pen, path)
                        End Using
                    End Using
                End Sub
            AddHandler tb.Enter, Sub() box.Invalidate()
            AddHandler tb.Leave, Sub() box.Invalidate()
            Return box
        End Function

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

        ' ------------------------------------------------------------------ labels / buttons / chips

        Private Sub DisableMnemonics(root As Control)
            For Each c As Control In root.Controls
                If TypeOf c Is Label Then DirectCast(c, Label).UseMnemonic = False
                If TypeOf c Is CheckBox Then DirectCast(c, CheckBox).UseMnemonic = False
                DisableMnemonics(c)
            Next
            Dim split = TryCast(root, SplitContainer)
            If split IsNot Nothing Then
                DisableMnemonics(split.Panel1)
                DisableMnemonics(split.Panel2)
            End If
        End Sub

        Private Sub StyleSectionLabel(lbl As Label)
            lbl.Font = SectionFont
            lbl.ForeColor = TextColor
            lbl.AutoSize = True
            lbl.Margin = New Padding(0, 14, 0, 6)
        End Sub

        Private Sub StylePrimaryButton(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.UseVisualStyleBackColor = False
            btn.Cursor = Cursors.Hand
            btn.Font = SectionFont
            btn.Height = 38
            btn.Padding = New Padding(12, 0, 12, 0)
            btn.BackColor = If(btn.Enabled, Primary, DisabledPrimary)
            btn.ForeColor = Color.White
            btn.FlatAppearance.BorderSize = 0
            btn.FlatAppearance.MouseOverBackColor = PrimaryHover
            btn.FlatAppearance.MouseDownBackColor = PrimaryHover
            ' Save Role starts disabled until a role is picked: show that with a muted fill.
            AddHandler btn.EnabledChanged, Sub() btn.BackColor = If(btn.Enabled, Primary, DisabledPrimary)
        End Sub

        Private Function NewChipFlow() As FlowLayoutPanel
            Dim f As New FlowLayoutPanel With {
                .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = True,
                .FlowDirection = FlowDirection.LeftToRight, .Margin = New Padding(0), .BackColor = CardColor
            }
            Return f
        End Function

        ''' <summary>Toggle chip: the checkbox stays a CheckBox (state/events untouched); the look follows its Checked state.</summary>
        Private Sub StyleChip(chk As CheckBox)
            chk.Appearance = Appearance.Button
            chk.FlatStyle = FlatStyle.Flat
            ' AutoSize ignores Padding for flat button-style checkboxes (the text then gets clipped), so each chip
            ' is sized from its own text instead -- measured in the bold face so ticking it never changes its width.
            Dim textSize = TextRenderer.MeasureText(chk.Text, ChipBoldFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
            chk.AutoSize = False
            chk.Size = New Size(textSize.Width + 48, textSize.Height + 14)
            chk.Padding = New Padding(10, 0, 10, 0)
            chk.Margin = New Padding(0, 0, 8, 8)
            chk.Cursor = Cursors.Hand
            chk.TextAlign = ContentAlignment.MiddleLeft
            chk.UseVisualStyleBackColor = False
            chk.BackColor = Color.White
            chk.FlatAppearance.BorderSize = 1
            chk.FlatAppearance.CheckedBackColor = PrimarySoft
            chk.FlatAppearance.MouseOverBackColor = PrimarySoft
            chk.FlatAppearance.MouseDownBackColor = PrimarySoft
            Dim refresh =
                Sub()
                    If chk.Checked Then
                        chk.ForeColor = Primary
                        chk.FlatAppearance.BorderColor = Primary
                        chk.Font = ChipBoldFont
                    Else
                        chk.ForeColor = TextColor
                        chk.FlatAppearance.BorderColor = InputBorder
                        chk.Font = ChipFont
                    End If
                End Sub
            AddHandler chk.CheckedChanged, Sub() refresh()
            refresh()
        End Sub

    End Module

End Namespace
