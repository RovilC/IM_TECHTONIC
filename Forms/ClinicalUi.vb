Imports System.Drawing
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>
    ''' Shared "clinical UI" look used by the Triage / Consultation workspace (and the profile tab bar):
    ''' card panels, captioned fields, bordered inputs with unit suffixes, outlined/primary buttons and a
    ''' pill-style tab bar. This is the WinForms counterpart of a shared clinical-ui stylesheet.
    ''' </summary>
    Friend Module ClinicalUi
        Public ReadOnly Green As Color = ColorTranslator.FromHtml("#1F5F3F")
        Public ReadOnly GreenHover As Color = ColorTranslator.FromHtml("#174A31")
        Public ReadOnly GreenSoft As Color = ColorTranslator.FromHtml("#E8F3EC")
        Public ReadOnly PageBg As Color = ColorTranslator.FromHtml("#F3F7F4")
        Public ReadOnly CardBorder As Color = ColorTranslator.FromHtml("#E3E8E5")
        Public ReadOnly InputBorder As Color = ColorTranslator.FromHtml("#D9DEDB")
        Public ReadOnly TextMain As Color = ColorTranslator.FromHtml("#1A1F1C")
        Public ReadOnly TextSoft As Color = ColorTranslator.FromHtml("#6B7280")
        Public ReadOnly LabelGray As Color = ColorTranslator.FromHtml("#4B5563")
        Public ReadOnly Placeholder As Color = ColorTranslator.FromHtml("#9CA3AF")
        Public ReadOnly WarnBg As Color = ColorTranslator.FromHtml("#FEF6E4")
        Public ReadOnly WarnBorder As Color = ColorTranslator.FromHtml("#F5D28A")
        Public ReadOnly WarnText As Color = ColorTranslator.FromHtml("#B45309")
        Public ReadOnly DangerText As Color = ColorTranslator.FromHtml("#DC2626")

        Public ReadOnly CaptionFont As New Font("Segoe UI Semibold", 8.25F)
        Public ReadOnly InputFont As New Font("Segoe UI", 10.5F)
        Public ReadOnly CardTitleFont As New Font("Segoe UI Semibold", 11.0F)

        ''' <summary>
        ''' Double-buffers a control and makes it repaint fully when resized. Without this, borders drawn in a
        ''' Paint handler leave smeared / stretched remnants when the layout changes (scrollbar appearing,
        ''' window resize, tab switch) and flicker while an AutoScroll page is scrolled.
        ''' </summary>
        Public Sub SetDoubleBuffered(c As Control)
            GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(c, True)
            GetType(Control).GetMethod("SetStyle", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).Invoke(c, New Object() {ControlStyles.ResizeRedraw Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True})
        End Sub

        ' ---------------- Buttons ----------------

        Public Sub StylePrimary(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.UseVisualStyleBackColor = False
            btn.BackColor = Green
            btn.ForeColor = Color.White
            btn.Font = New Font("Segoe UI Semibold", 10.0F)
            btn.Cursor = Cursors.Hand
            btn.Height = 38
            btn.FlatAppearance.BorderSize = 0
            btn.FlatAppearance.MouseOverBackColor = GreenHover
            btn.FlatAppearance.MouseDownBackColor = GreenHover
        End Sub

        Public Sub StyleSecondary(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.UseVisualStyleBackColor = False
            btn.BackColor = Color.White
            btn.ForeColor = Green
            btn.Font = New Font("Segoe UI Semibold", 10.0F)
            btn.Cursor = Cursors.Hand
            btn.Height = 38
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.BorderColor = Green
            btn.FlatAppearance.MouseOverBackColor = GreenSoft
            btn.FlatAppearance.MouseDownBackColor = GreenSoft
        End Sub

        ''' <summary>Small gray outlined button (Cancel / Back).</summary>
        Public Sub StyleCancel(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.UseVisualStyleBackColor = False
            btn.BackColor = Color.White
            btn.ForeColor = TextSoft
            btn.Font = New Font("Segoe UI", 9.0F)
            btn.Cursor = Cursors.Hand
            btn.Size = New Size(84, 30)
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.BorderColor = InputBorder
            btn.FlatAppearance.MouseOverBackColor = PageBg
        End Sub

        ' ---------------- Absolute-position helpers (dialogs laid out by x/y) ----------------

        ''' <summary>Bordered white card placed on a dialog (light mint page colour behind it). Caller sets its Height when content is laid out.</summary>
        Public Function NewDialogCard(dlg As Form, width As Integer) As Panel
            dlg.BackColor = PageBg
            Dim card As New Panel With {.BackColor = Color.White, .Location = New Point(16, 16), .Width = width}
            AddHandler card.Paint, Sub(s, e)
                                       Using pen As New Pen(CardBorder)
                                           e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
                                       End Using
                                   End Sub
            dlg.Controls.Add(card)
            Return card
        End Function

        ''' <summary>Caption above a field at x/y. Every "*" in the text is drawn red; the wording is left exactly as given.</summary>
        Public Sub AddLabelAt(parent As Control, text As String, x As Integer, y As Integer)
            Dim parts = text.Split("*"c)
            Dim cx = x
            For i = 0 To parts.Length - 1
                If parts(i).Length > 0 Then
                    parent.Controls.Add(New Label With {.Text = parts(i), .Font = CaptionFont, .ForeColor = LabelGray, .AutoSize = True, .UseMnemonic = False, .Location = New Point(cx, y), .Margin = New Padding(0)})
                    cx += TextRenderer.MeasureText(parts(i), CaptionFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix).Width
                End If
                If i < parts.Length - 1 Then
                    parent.Controls.Add(New Label With {.Text = "*", .Font = CaptionFont, .ForeColor = DangerText, .AutoSize = True, .Location = New Point(cx, y), .Margin = New Padding(0)})
                    cx += TextRenderer.MeasureText("*", CaptionFont, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                End If
            Next
        End Sub

        ''' <summary>Bordered input (see InputWrap) placed at an absolute position and width.</summary>
        Public Function PlaceInput(parent As Control, tb As TextBox, x As Integer, y As Integer, width As Integer, Optional height As Integer = 38) As Panel
            Dim wrap = InputWrap(tb, Nothing, height)
            wrap.Dock = DockStyle.None
            wrap.Location = New Point(x, y)
            wrap.Size = New Size(width, height)
            parent.Controls.Add(wrap)
            Return wrap
        End Function

        ''' <summary>Combo box at an absolute position with the standard input font.</summary>
        Public Sub PlaceCombo(parent As Control, cbo As ComboBox, x As Integer, y As Integer, width As Integer)
            cbo.Font = InputFont
            cbo.Location = New Point(x, y)
            cbo.Width = width
            parent.Controls.Add(cbo)
        End Sub

        ' ---------------- Cards ----------------

        ''' <summary>Scrollable page holding two equal columns (18px gap). Cards are added to the returned column tables.</summary>
        Public Function TwoColumnPage(ByRef left As TableLayoutPanel, ByRef right As TableLayoutPanel) As Panel
            Dim page As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = PageBg, .Padding = New Padding(0, 18, 0, 8)}
            SetDoubleBuffered(page)
            Dim outer As New TableLayoutPanel With {.ColumnCount = 2, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .Margin = New Padding(0)}
            outer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            outer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            SetDoubleBuffered(outer)
            left = NewColumn(New Padding(0, 0, 9, 0))
            right = NewColumn(New Padding(9, 0, 0, 0))
            outer.Controls.Add(left, 0, 0)
            outer.Controls.Add(right, 1, 0)
            page.Controls.Add(outer)
            Return page
        End Function

        Private Function NewColumn(margin As Padding) As TableLayoutPanel
            Return FullWidth(New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .Margin = margin})
        End Function

        ''' <summary>One-column tables must use a Percent column; the default AutoSize column grows to the content's preferred width and spills out of the card.</summary>
        Private Function FullWidth(t As TableLayoutPanel) As TableLayoutPanel
            t.ColumnStyles.Clear()
            t.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            SetDoubleBuffered(t)
            Return t
        End Function

        ''' <summary>Adds a white card (1px border, titled) to a column and returns its body table for fields.</summary>
        Public Function AddCard(column As TableLayoutPanel, title As String, Optional warn As Boolean = False, Optional stepNumber As Integer = 0) As TableLayoutPanel
            Dim card As New TableLayoutPanel With {
                .ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top,
                .BackColor = If(warn, WarnBg, Color.White), .Padding = New Padding(20, 16, 20, 4), .Margin = New Padding(0, 0, 0, 16)
            }
            FullWidth(card)
            Dim border = If(warn, WarnBorder, CardBorder)
            AddHandler card.Paint, Sub(s, e)
                                       Using pen As New Pen(border)
                                           e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1)
                                       End Using
                                   End Sub

            Dim titlePanel As New Panel With {.Dock = DockStyle.Fill, .Height = 30, .Margin = New Padding(0)}
            Dim required = title.EndsWith("*")
            Dim text = title.TrimEnd("*"c).TrimEnd()
            Dim font As Font = If(warn, New Font("Segoe UI", 8.0F, FontStyle.Bold), CardTitleFont)
            Dim shown = If(warn, text.ToUpperInvariant(), text)
            ' Optional step badge (green circle with the card's number) shows the order to work through the form.
            Dim titleX = 0
            If stepNumber > 0 Then
                Dim badge As New Panel With {.Size = New Size(24, 24), .Location = New Point(0, 0), .BackColor = Color.White}
                Dim numText = stepNumber.ToString()
                AddHandler badge.Paint, Sub(s, e)
                                            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                            Using b As New SolidBrush(Green)
                                                e.Graphics.FillEllipse(b, 0, 0, 23, 23)
                                            End Using
                                            TextRenderer.DrawText(e.Graphics, numText, New Font("Segoe UI Semibold", 9.0F), New Rectangle(0, 0, 24, 24), Color.White,
                                                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                                        End Sub
                titlePanel.Controls.Add(badge)
                titleX = 34
            End If
            titlePanel.Controls.Add(New Label With {.Text = shown, .Font = font, .ForeColor = If(warn, WarnText, TextMain), .AutoSize = True, .Location = New Point(titleX, 2)})
            If required Then
                Dim w = TextRenderer.MeasureText(shown, font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding).Width
                titlePanel.Controls.Add(New Label With {.Text = "*", .Font = font, .ForeColor = DangerText, .AutoSize = True, .Location = New Point(titleX + w + 3, 2)})
            End If
            card.Controls.Add(titlePanel)

            Dim body As New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .Margin = New Padding(0)}
            FullWidth(body)
            card.Controls.Add(body)
            column.Controls.Add(card)
            Return body
        End Function

        ''' <summary>A grid of captioned fields inside a card body (equal percent columns, 14px spacing).</summary>
        Public Function NewGrid(body As TableLayoutPanel, cols As Integer) As TableLayoutPanel
            Dim g As New TableLayoutPanel With {.ColumnCount = cols, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .Margin = New Padding(0, 0, -14, 0)}  ' -14: cancels the last column's 14px cell margin so inputs reach the card padding edge
            For i = 1 To cols
                g.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / cols))
            Next
            SetDoubleBuffered(g)
            body.Controls.Add(g)
            Return g
        End Function

        Private Function CaptionPanel(text As String) As Control
            Dim required = text.EndsWith("*")
            Dim caption = text.TrimEnd("*"c).TrimEnd()
            Dim flow As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(0, 0, 0, 6)}
            flow.Controls.Add(New Label With {.Text = caption, .Font = CaptionFont, .ForeColor = LabelGray, .AutoSize = True, .Margin = New Padding(0)})
            If required Then flow.Controls.Add(New Label With {.Text = "*", .Font = CaptionFont, .ForeColor = DangerText, .AutoSize = True, .Margin = New Padding(2, 0, 0, 0)})
            Return flow
        End Function

        Private Function NewCell() As TableLayoutPanel
            Dim cell As New TableLayoutPanel With {.ColumnCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 14, 14)}
            Return FullWidth(cell)
        End Function

        ''' <summary>Adds a caption + control as one cell of a field grid (optionally spanning columns).</summary>
        Public Sub AddField(grid As TableLayoutPanel, caption As String, ctrl As Control, Optional colSpan As Integer = 1)
            Dim cell = NewCell()
            cell.Controls.Add(CaptionPanel(caption))
            ctrl.Dock = DockStyle.Fill
            ctrl.Margin = New Padding(0)
            If TypeOf ctrl Is ComboBox OrElse TypeOf ctrl Is NumericUpDown OrElse TypeOf ctrl Is DateTimePicker Then ctrl.Font = InputFont
            cell.Controls.Add(ctrl)
            grid.Controls.Add(cell)
            If colSpan > 1 Then grid.SetColumnSpan(cell, colSpan)
        End Sub

        ''' <summary>Blood pressure: systolic / diastolic side by side with a "/" between, text centred.</summary>
        Public Sub AddBloodPressure(grid As TableLayoutPanel, sysCaption As String, sys As TextBox, diaCaption As String, dia As TextBox)
            Dim cell As New TableLayoutPanel With {.ColumnCount = 3, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 14, 14)}
            cell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            cell.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 24))
            cell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            cell.Controls.Add(CaptionPanel(sysCaption), 0, 0)
            cell.Controls.Add(CaptionPanel(diaCaption), 2, 0)
            cell.Controls.Add(InputWrap(sys, Nothing, 38, True), 0, 1)
            cell.Controls.Add(New Label With {.Text = "/", .Font = InputFont, .ForeColor = TextSoft, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .Margin = New Padding(0)}, 1, 1)
            cell.Controls.Add(InputWrap(dia, Nothing, 38, True), 2, 1)
            grid.Controls.Add(cell)
        End Sub

        ''' <summary>Adds a full-width control (checkbox, grid host, ...) as its own row of a card body.</summary>
        Public Sub AddRow(body As TableLayoutPanel, ctrl As Control)
            ctrl.Dock = DockStyle.Fill
            ctrl.Margin = New Padding(0, 0, 0, 14)
            body.Controls.Add(ctrl)
        End Sub

        ' ---------------- Inputs ----------------

        ''' <summary>
        ''' Wraps a TextBox in a bordered white box: fixed height, text vertically centred, an optional unit
        ''' suffix inside the right edge, and a green border while focused. Multiline boxes fill the wrapper.
        ''' </summary>
        Public Function InputWrap(tb As TextBox, Optional unit As String = Nothing, Optional height As Integer = 38, Optional centerText As Boolean = False) As Panel
            Dim wrap As New Panel With {.BackColor = Color.White, .Height = height, .MinimumSize = New Size(0, height), .Dock = DockStyle.Fill, .Margin = New Padding(0)}
            SetDoubleBuffered(wrap)
            tb.BorderStyle = BorderStyle.None
            tb.Font = InputFont
            tb.BackColor = Color.White
            tb.ForeColor = TextMain

            If tb.Multiline Then
                wrap.Padding = New Padding(12, 9, 8, 8)
                tb.Dock = DockStyle.Fill
                tb.ScrollBars = ScrollBars.None
            Else
                Dim unitW = If(String.IsNullOrEmpty(unit), 0, 46)
                wrap.Padding = New Padding(1) ' keeps the unit label from painting over the border
                If unitW > 0 Then
                    wrap.Controls.Add(New Label With {.Text = unit, .Font = New Font("Segoe UI", 8.25F), .ForeColor = Placeholder, .Dock = DockStyle.Right, .Width = unitW, .TextAlign = ContentAlignment.MiddleLeft, .BackColor = Color.White})
                End If
                tb.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
                If centerText Then tb.TextAlign = HorizontalAlignment.Center
                Dim layout = Sub()
                                 tb.Left = 12
                                 tb.Width = Math.Max(10, wrap.ClientSize.Width - 24 - unitW)
                                 tb.Top = Math.Max(0, (wrap.Height - tb.Height) \ 2)
                             End Sub
                AddHandler wrap.Resize, Sub() layout()
                layout()
            End If

            AddHandler tb.Enter, Sub() wrap.Invalidate()
            AddHandler tb.Leave, Sub() wrap.Invalidate()
            AddHandler wrap.Click, Sub() tb.Focus()
            AddHandler wrap.Paint, Sub(s, e)
                                       Dim focused = tb.Focused
                                       Using pen As New Pen(If(focused, Green, InputBorder))
                                           e.Graphics.DrawRectangle(pen, 0, 0, wrap.Width - 1, wrap.Height - 1)
                                       End Using
                                       If focused Then
                                           Using halo As New Pen(Color.FromArgb(70, Green))
                                               e.Graphics.DrawRectangle(halo, 1, 1, wrap.Width - 3, wrap.Height - 3)
                                           End Using
                                       End If
                                   End Sub
            wrap.Controls.Add(tb)
            Return wrap
        End Function

        ''' <summary>Read-only value (e.g. BMI) shown in the same bordered white box as an input, value in medium weight.</summary>
        Public Function ReadOnlyWrap(lbl As Label, Optional height As Integer = 38) As Panel
            Dim wrap As New Panel With {.BackColor = Color.White, .Height = height, .MinimumSize = New Size(0, height), .Dock = DockStyle.Fill, .Margin = New Padding(0), .Padding = New Padding(12, 1, 8, 1)}
            SetDoubleBuffered(wrap)
            lbl.AutoSize = False
            lbl.Dock = DockStyle.Fill
            lbl.Font = New Font("Segoe UI Semibold", 10.5F)
            lbl.TextAlign = ContentAlignment.MiddleLeft
            lbl.BackColor = Color.White
            wrap.Controls.Add(lbl)
            AddHandler wrap.Paint, Sub(s, e)
                                       Using pen As New Pen(InputBorder)
                                           e.Graphics.DrawRectangle(pen, 0, 0, wrap.Width - 1, wrap.Height - 1)
                                       End Using
                                   End Sub
            Return wrap
        End Function

        ''' <summary>Bordered white host for a DataGridView with a fixed height.</summary>
        Public Function GridHost(grid As DataGridView, height As Integer) As Panel
            Dim host As New Panel With {.Height = height, .MinimumSize = New Size(0, height), .Padding = New Padding(1), .BackColor = Color.White}
            SetDoubleBuffered(host)
            grid.Dock = DockStyle.Fill
            host.Controls.Add(grid)
            AddHandler host.Paint, Sub(s, e)
                                       Using pen As New Pen(CardBorder)
                                           e.Graphics.DrawRectangle(pen, 0, 0, host.Width - 1, host.Height - 1)
                                       End Using
                                   End Sub
            Return host
        End Function

        ''' <summary>Table look inside a card: no vertical lines, small uppercase grey header, light row separators.</summary>
        Public Sub StyleClinicalGrid(grid As DataGridView)
            grid.BackgroundColor = Color.White
            grid.BorderStyle = BorderStyle.None
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            grid.GridColor = CardBorder
            grid.EnableHeadersVisualStyles = False
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Placeholder
            grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 7.5F, FontStyle.Bold)
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Placeholder
            grid.ColumnHeadersHeight = 32
            grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.5F)
            grid.DefaultCellStyle.ForeColor = TextMain
            grid.DefaultCellStyle.SelectionBackColor = GreenSoft
            grid.DefaultCellStyle.SelectionForeColor = TextMain
            grid.DefaultCellStyle.Padding = New Padding(4, 2, 4, 2)
            grid.RowTemplate.Height = 36
            ' Header captions read as small uppercase labels.
            For Each col As DataGridViewColumn In grid.Columns
                If Not String.IsNullOrEmpty(col.HeaderText) Then col.HeaderText = col.HeaderText.ToUpperInvariant()
            Next
        End Sub
    End Module

    ''' <summary>Owner-painted tab strip: white bar with hairlines, muted tab text, and a solid green rounded pill behind the selected tab.</summary>
    Friend Class PillTabControl
        Inherits TabControl

        Private hoverIndex As Integer = -1

        ''' <summary>Colour shown around the tab pages (white for table-like pages, light mint when the pages hold cards).</summary>
        <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public Property PageBackColor As Color = Color.White

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            ItemSize = New Size(0, 34)
            Padding = New Point(14, 4)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim idx = -1
            For i = 0 To TabCount - 1
                If GetTabRect(i).Contains(e.Location) Then idx = i : Exit For
            Next
            If idx <> hoverIndex Then hoverIndex = idx : Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If hoverIndex <> -1 Then hoverIndex = -1 : Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.Clear(PageBackColor)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            If TabCount = 0 Then Return

            Dim stripBottom = GetTabRect(0).Bottom + 2
            g.FillRectangle(Brushes.White, 0, 0, Width, stripBottom)
            Using pen As New Pen(CardBorder)
                g.DrawLine(pen, 0, 0, Width, 0)
                g.DrawLine(pen, 0, stripBottom, Width, stripBottom)
            End Using

            Using normalFont As New Font("Segoe UI", 9.0F),
                  activeFont As New Font("Segoe UI", 9.0F, FontStyle.Bold),
                  pillBrush As New SolidBrush(Green)
                For i = 0 To TabCount - 1
                    Dim r = GetTabRect(i)
                    r.Inflate(-2, -3)
                    Dim selected = (i = SelectedIndex)
                    If selected Then
                        Using path = RoundedRect(r, 6)
                            g.FillPath(pillBrush, path)
                        End Using
                    End If
                    Dim textColor = If(selected, Color.White, If(i = hoverIndex, TextMain, TextSoft))
                    TextRenderer.DrawText(g, TabPages(i).Text, If(selected, activeFont, normalFont), r, textColor,
                                          TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis)
                Next
            End Using
        End Sub

        Private Shared Function RoundedRect(r As Rectangle, radius As Integer) As Drawing2D.GraphicsPath
            Dim d = radius * 2
            Dim path As New Drawing2D.GraphicsPath()
            path.AddArc(r.X, r.Y, d, d, 180, 90)
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

End Namespace
