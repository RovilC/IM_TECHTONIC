Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace Forms

    ''' <summary>
    ''' Visual-only card styling for the Audit Logs report (same look as the Role Permissions form). Called once at
    ''' the end of ReportsForm.BuildAuditLogReport. Existing controls are only re-parented and restyled; the grid
    ''' keeps its original table design (UiTheme.StyleGrid) and all filter/refresh/export logic is untouched.
    ''' </summary>
    Friend Module AuditLogsTheme

        Public Sub Apply(page As Panel, btnExport As Button,
                         dtpFrom As DateTimePicker, dtpTo As DateTimePicker,
                         cboAction As ComboBox, cboUser As ComboBox,
                         lblSummary As Label, grid As DataGridView)

            Dim labels = page.Controls.OfType(Of Label)().ToList()
            Dim title = labels.First()   ' the report title is the first label ReportHeader adds
            Dim lblFrom = labels.First(Function(l) l.Text = "From")
            Dim lblTo = labels.First(Function(l) l.Text = "To")
            Dim lblAction = labels.First(Function(l) l.Text = "Action")
            Dim lblUser = labels.First(Function(l) l.Text = "User")

            page.BackColor = RolesPermissionsTheme.Background
            page.AutoScrollMinSize = New Size(900, 520)   ' narrow windows scroll instead of crushing the layout

            ' ---- cards: filters on top, table below (Top-docked controls added later sit higher) ----
            Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 16, .BackColor = RolesPermissionsTheme.Background}
            page.Controls.Add(gap)

            Dim filtersCard As New Panel With {.Dock = DockStyle.Top, .Height = 172}
            RolesPermissionsTheme.MakeCard(filtersCard)
            page.Controls.Add(filtersCard)

            Dim tableCard As New Panel With {.Dock = DockStyle.Fill}
            RolesPermissionsTheme.MakeCard(tableCard)
            tableCard.Padding = New Padding(14)
            page.Controls.Add(tableCard)
            tableCard.BringToFront()

            ' ---- table card: the same grid, just filling its card ----
            tableCard.Controls.Add(grid)
            grid.Dock = DockStyle.Fill

            ' ---- filters card: summary (bottom), filter row, then title row (top) ----
            lblSummary.UseMnemonic = False
            lblSummary.Font = New Font("Segoe UI", 9.0F)
            lblSummary.ForeColor = ClinicalUi.TextSoft
            lblSummary.AutoSize = True
            lblSummary.Dock = DockStyle.Top
            lblSummary.Padding = New Padding(0, 10, 0, 0)
            filtersCard.Controls.Add(lblSummary)

            Dim filterRow As New Panel With {.Dock = DockStyle.Top, .Height = 62, .BackColor = Color.White}
            filtersCard.Controls.Add(filterRow)
            PlaceField(filterRow, lblFrom, dtpFrom, 0)
            PlaceField(filterRow, lblTo, dtpTo, 170)
            PlaceField(filterRow, lblAction, cboAction, 340)
            PlaceField(filterRow, lblUser, cboUser, 520)

            Dim headerRow As New Panel With {.Dock = DockStyle.Top, .Height = 40, .BackColor = Color.White}
            filtersCard.Controls.Add(headerRow)
            title.UseMnemonic = False   ' so "System Activity & Audit Logs Report" shows its ampersand
            title.Font = New Font("Segoe UI Semibold", 11.5F)
            title.ForeColor = ClinicalUi.TextMain
            headerRow.Controls.Add(title)
            title.Location = New Point(0, 2)

            ' Export sits at the right end of the card's title row. ReportHeader pins the button using the PAGE width
            ' (page.SizeChanged), which would push it out of the card, so re-pin it against the title row afterwards:
            ' this handler is added after that one and therefore runs last.
            ClinicalUi.StyleSecondary(btnExport)
            btnExport.Height = 34
            headerRow.Controls.Add(btnExport)
            Dim pin = Sub()
                          btnExport.Top = 0
                          btnExport.Left = Math.Max(0, headerRow.ClientSize.Width - btnExport.Width)
                      End Sub
            AddHandler page.SizeChanged, Sub() pin()
            AddHandler headerRow.SizeChanged, Sub() pin()
            pin()
        End Sub

        Private Sub PlaceField(row As Panel, caption As Label, input As Control, x As Integer)
            row.Controls.Add(caption)
            caption.UseMnemonic = False
            caption.Font = ClinicalUi.CaptionFont
            caption.ForeColor = ClinicalUi.LabelGray
            caption.Location = New Point(x, 0)
            row.Controls.Add(input)
            input.Font = ClinicalUi.InputFont
            input.Location = New Point(x, 22)
        End Sub

    End Module

End Namespace
