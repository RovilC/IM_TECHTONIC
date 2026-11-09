Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>
    ''' Visual-only card styling for the Reports section (same look as Role Permissions / Audit Logs). Called once
    ''' per report by ReportsForm after the report's panel has been built. Existing controls are only re-parented
    ''' and restyled: the filter area keeps its exact original layout inside a "filters" card, and every grid keeps
    ''' its table design (UiTheme.StyleGrid) in its own card, with the bold label above it as the card title.
    ''' No filter, query, refresh or export logic is touched.
    ''' </summary>
    Friend Module ReportsTheme

        Public Sub ApplyReport(page As Panel)
            Dim kids = page.Controls.Cast(Of Control).ToList()
            Dim grids = kids.OfType(Of DataGridView)().OrderBy(Function(g) g.Top).ToList()
            If grids.Count = 0 Then Return

            Dim labels = kids.OfType(Of Label)().ToList()
            Dim title = labels.FirstOrDefault()                   ' ReportHeader adds the title label first
            Dim btnExport = kids.OfType(Of Button)().FirstOrDefault()

            ' ---- which bold label is the title of which grid? (a filter caption has an input directly below it; a grid title does not) ----
            Dim sections As New List(Of Tuple(Of Label, DataGridView))
            For Each g In grids
                Dim gridTop = g.Top
                Dim t = labels.FirstOrDefault(
                    Function(l) l IsNot title AndAlso l.Font.Bold AndAlso l.Left = g.Left AndAlso l.Bottom <= gridTop + 2 AndAlso gridTop - l.Bottom <= 40 AndAlso
                                Not kids.Any(Function(c) Not TypeOf c Is Label AndAlso Not TypeOf c Is DataGridView AndAlso Not TypeOf c Is Button AndAlso
                                                         c.Left = l.Left AndAlso c.Top >= l.Bottom - 2 AndAlso c.Top <= l.Bottom + 30))
                sections.Add(Tuple.Create(t, g))
            Next
            Dim sectionTitles = sections.Where(Function(s) s.Item1 IsNot Nothing).Select(Function(s) CType(s.Item1, Control)).ToList()

            Dim firstSectionTop = sections.Min(Function(s) If(s.Item1 IsNot Nothing, s.Item1.Top, s.Item2.Top))
            Dim topZone = kids.Where(Function(c) Not TypeOf c Is DataGridView AndAlso Not sectionTitles.Contains(c)).ToList()

            page.BackColor = RolesPermissionsTheme.Background
            For Each l In labels
                l.UseMnemonic = False   ' so "&" in a title or caption shows literally
            Next

            ' ---- filters card: title row + filter controls at their original coordinates ----
            Dim filtersCard As New Panel With {.Dock = DockStyle.Top}
            RolesPermissionsTheme.MakeCard(filtersCard)
            Dim content As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
            filtersCard.Controls.Add(content)
            For Each c In topZone
                content.Controls.Add(c)        ' Location is kept, so the filter layout is exactly as before
            Next
            For Each c In topZone
                StyleTopZoneControl(c, title)
            Next

            ' A few reports place a note label on top of the filter inputs (same spot, so they overlap); with the
            ' roomier inputs that collision shows. Nudge any such plain label to just below the inputs.
            Dim inputs = topZone.Where(Function(c) TypeOf c Is ComboBox OrElse TypeOf c Is DateTimePicker OrElse TypeOf c Is TextBox).ToList()
            If inputs.Count > 0 Then
                Dim inputsBottom = inputs.Max(Function(c) c.Bottom)
                For Each l In topZone.OfType(Of Label)()
                    If l IsNot title AndAlso Not l.Font.Bold AndAlso inputs.Any(Function(i) i.Bounds.IntersectsWith(l.Bounds)) Then l.Top = inputsBottom + 8
                Next
            End If

            ' The card is as tall as its content, but never shorter than the space the original layout reserved for
            ' it (up to the first section), so labels that grow later (summaries, household info) stay inside it.
            Dim contentBottom = topZone.Max(Function(c) c.Bottom)
            filtersCard.Height = Math.Max(Math.Max(contentBottom + 10, firstSectionTop - 8), 40) + filtersCard.Padding.Vertical

            If btnExport IsNot Nothing Then
                ClinicalUi.StyleSecondary(btnExport)
                btnExport.Height = 34
                btnExport.Top = 0
                ' ReportHeader pins the button from the PAGE width (page.SizeChanged); re-pin it inside the card
                ' afterwards. This handler is added later, so it runs last.
                Dim pin = Sub() btnExport.Left = Math.Max(0, content.ClientSize.Width - btnExport.Width)
                AddHandler page.SizeChanged, Sub() pin()
                AddHandler content.SizeChanged, Sub() pin()
                pin()
            End If

            ' ---- one card per grid ----
            Dim cards As New List(Of Panel) From {filtersCard}
            Dim fixedHeights As Integer = filtersCard.Height
            For i = 0 To sections.Count - 1
                Dim sec = sections(i)
                Dim g = sec.Item2
                Dim card As New Panel()
                RolesPermissionsTheme.MakeCard(card)
                card.Padding = New Padding(14)
                Dim height = g.Height + card.Padding.Vertical
                card.Controls.Add(g)           ' added first: the Fill control docks last
                g.Dock = DockStyle.Fill
                If sec.Item1 IsNot Nothing Then
                    Dim t = sec.Item1
                    t.AutoSize = False
                    t.Dock = DockStyle.Top
                    t.Height = 30
                    t.TextAlign = ContentAlignment.MiddleLeft
                    t.Font = New Font("Segoe UI Semibold", 10.5F)
                    t.ForeColor = ClinicalUi.TextMain
                    t.Padding = New Padding(2, 0, 0, 6)
                    card.Controls.Add(t)
                    height += 30
                End If
                If i < sections.Count - 1 Then
                    card.Dock = DockStyle.Top
                    card.Height = height
                    fixedHeights += height + 16
                Else
                    card.Dock = DockStyle.Fill   ' the last grid takes the remaining height
                End If
                cards.Add(card)
            Next

            ' ---- assemble: bottom-most first (Fill), then Top cards upward with 16px gaps ----
            page.Controls.Add(cards(cards.Count - 1))
            For i = cards.Count - 2 To 0 Step -1
                page.Controls.Add(New Panel With {.Dock = DockStyle.Top, .Height = 16, .BackColor = RolesPermissionsTheme.Background})
                page.Controls.Add(cards(i))
            Next
            cards(cards.Count - 1).BringToFront()

            ' The report used to scroll at fixed sizes; keep a minimum so a small window scrolls rather than crushing the table.
            page.AutoScrollMinSize = New Size(900, fixedHeights + 16 + 280)
        End Sub

        Private Sub StyleTopZoneControl(c As Control, title As Label)
            If c Is title Then
                title.Font = New Font("Segoe UI Semibold", 11.5F)
                title.ForeColor = ClinicalUi.TextMain
            ElseIf TypeOf c Is Label Then
                Dim l = DirectCast(c, Label)
                If l.Font.Bold Then               ' filter caption ("From", "Barangay", ...)
                    l.Font = ClinicalUi.CaptionFont
                    l.ForeColor = ClinicalUi.LabelGray
                End If
            ElseIf TypeOf c Is ComboBox OrElse TypeOf c Is DateTimePicker OrElse TypeOf c Is TextBox Then
                c.Font = ClinicalUi.InputFont
            End If
        End Sub

    End Module

End Namespace
