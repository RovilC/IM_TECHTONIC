Imports System.Drawing

Namespace Forms

    ''' <summary>Set by UiTheme.TrackChanges when the user edits any watched input.</summary>
    Public Class ChangeTracker
        Public Property IsDirty As Boolean
        Public Sub Reset()
            IsDirty = False
        End Sub
    End Class

    ''' <summary>
    ''' A screen hosted in the main window that may hold unsaved work. The main window asks
    ''' before navigating away (sidebar, Back, log out); returning False keeps the user there.
    ''' </summary>
    Public Interface IConfirmLeave
        Function ConfirmLeave() As Boolean
    End Interface

    ''' <summary>Shared colors/fonts so every form matches the proposal's green RHU look (Figures 1-30).</summary>
    Public Module UiTheme
        Public ReadOnly SidebarDark As Color = ColorTranslator.FromHtml("#1B4332")
        Public ReadOnly SidebarActive As Color = ColorTranslator.FromHtml("#2D6A4F")
        Public ReadOnly PrimaryGreen As Color = ColorTranslator.FromHtml("#2D6A4F")
        Public ReadOnly PrimaryGreenDark As Color = ColorTranslator.FromHtml("#1B4332")
        Public ReadOnly AccentGreen As Color = ColorTranslator.FromHtml("#40916C")
        Public ReadOnly BackgroundLight As Color = ColorTranslator.FromHtml("#F1F5F1")
        Public ReadOnly CardWhite As Color = Color.White
        Public ReadOnly TextDark As Color = ColorTranslator.FromHtml("#1B2A22")
        Public ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#6B7A70")
        Public ReadOnly BorderLight As Color = ColorTranslator.FromHtml("#DDE5DE")
        Public ReadOnly DangerRed As Color = ColorTranslator.FromHtml("#C0392B")
        Public ReadOnly WarningAmber As Color = ColorTranslator.FromHtml("#B7791F")

        Public ReadOnly BaseFont As New Font("Segoe UI", 9.5F)
        Public ReadOnly HeaderFont As New Font("Segoe UI", 16.0F, FontStyle.Bold)
        Public ReadOnly SubHeaderFont As New Font("Segoe UI", 11.0F, FontStyle.Bold)
        Public ReadOnly BoldFont As New Font("Segoe UI", 9.5F, FontStyle.Bold)

        ''' <summary>Yes/No confirmation before a save, submit or other change; No is the default so a stray Enter does nothing.</summary>
        Public Function Confirm(owner As IWin32Window, message As String, Optional title As String = "Confirm") As Boolean
            Return MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes
        End Function

        ''' <summary>Asks before throwing away typed-in data (Cancel / Back / leaving a screen).</summary>
        Public Function ConfirmDiscard(owner As IWin32Window, Optional what As String = "the information you entered") As Boolean
            Return MessageBox.Show(owner, $"Discard {what}? Unsaved changes will be lost.", "Discard Changes",
                                   MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) = DialogResult.Yes
        End Function

        Public Sub ShowSuccess(owner As IWin32Window, message As String, Optional title As String = "Saved")
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

        ''' <summary>Uniform error message for a failed operation, with a plain-language hint when the database is unreachable.</summary>
        Public Sub ShowError(owner As IWin32Window, action As String, ex As Exception)
            Dim hint = ""
            If TypeOf ex Is MySqlConnector.MySqlException Then
                Dim mex = DirectCast(ex, MySqlConnector.MySqlException)
                If mex.ErrorCode = MySqlConnector.MySqlErrorCode.UnableToConnectToHost Then
                    hint = vbCrLf & vbCrLf & "The database could not be reached. Check that XAMPP / MySQL is running, then try again."
                ElseIf mex.ErrorCode = MySqlConnector.MySqlErrorCode.DuplicateKeyEntry Then
                    hint = vbCrLf & vbCrLf & "A record with the same value already exists."
                End If
            End If
            MessageBox.Show(owner, $"Could not {action}.{vbCrLf}{vbCrLf}{ex.Message}{hint}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Sub

        ''' <summary>
        ''' Watches every input inside <paramref name="root"/> (text boxes, combos, check/radio
        ''' buttons, date pickers, numeric boxes, editable grids) and reports whether the user
        ''' changed anything since this call. Call it AFTER the screen's data has been loaded so
        ''' the initial fill doesn't count; Reset() after a successful save.
        ''' </summary>
        Public Function TrackChanges(root As Control) As ChangeTracker
            Dim tracker As New ChangeTracker()
            HookChanges(root, tracker)
            Return tracker
        End Function

        ''' <summary>
        ''' For data-entry dialogs: closing without saving (Cancel, Esc or the X) asks before
        ''' discarding anything the user typed. Saving sets DialogResult = OK, which skips the
        ''' question. Call at the end of the dialog's constructor, after its data is loaded.
        ''' </summary>
        Public Function GuardUnsavedClose(dialog As Form) As ChangeTracker
            Dim tracker = TrackChanges(dialog)
            AddHandler dialog.FormClosing,
                Sub(s, e)
                    If dialog.DialogResult = DialogResult.OK OrElse Not tracker.IsDirty Then Return
                    If e.CloseReason <> CloseReason.UserClosing AndAlso e.CloseReason <> CloseReason.None Then Return
                    If Not ConfirmDiscard(dialog) Then e.Cancel = True
                End Sub
            Return tracker
        End Function

        Private Sub HookChanges(root As Control, tracker As ChangeTracker)
            For Each c As Control In root.Controls
                Dim mark As EventHandler = Sub() If c.Focused OrElse c.ContainsFocus Then tracker.IsDirty = True
                If TypeOf c Is TextBox Then
                    AddHandler c.TextChanged, mark
                ElseIf TypeOf c Is ComboBox Then
                    AddHandler DirectCast(c, ComboBox).SelectedIndexChanged, mark
                    AddHandler c.TextChanged, mark
                ElseIf TypeOf c Is CheckBox Then
                    AddHandler DirectCast(c, CheckBox).CheckedChanged, mark
                ElseIf TypeOf c Is RadioButton Then
                    AddHandler DirectCast(c, RadioButton).CheckedChanged, mark
                ElseIf TypeOf c Is DateTimePicker Then
                    AddHandler DirectCast(c, DateTimePicker).ValueChanged, mark
                ElseIf TypeOf c Is NumericUpDown Then
                    AddHandler DirectCast(c, NumericUpDown).ValueChanged, mark
                ElseIf TypeOf c Is DataGridView Then
                    Dim g = DirectCast(c, DataGridView)
                    AddHandler g.CellValueChanged, Sub(s, e) If e.RowIndex >= 0 AndAlso g.ContainsFocus Then tracker.IsDirty = True
                    AddHandler g.UserDeletedRow, Sub() tracker.IsDirty = True
                End If
                HookChanges(c, tracker)
            Next
        End Sub

        Public Sub StylePrimaryButton(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0
            btn.BackColor = PrimaryGreen
            btn.ForeColor = Color.White
            btn.Font = BoldFont
            btn.Cursor = Cursors.Hand
            btn.Height = 36
        End Sub

        Public Sub StyleSecondaryButton(btn As Button)
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.BorderColor = BorderLight
            btn.BackColor = Color.White
            btn.ForeColor = TextDark
            btn.Font = BaseFont
            btn.Cursor = Cursors.Hand
            btn.Height = 36
        End Sub

        ''' <summary>
        ''' Configures a SplitContainer's Panel1MinSize/Panel2MinSize/
        ''' SplitterDistance without the "SplitterDistance must be between
        ''' Panel1MinSize and Width - Panel2MinSize" crash. The crash isn't
        ''' really about SplitterDistance: SplitContainer reconciles it as
        ''' soon as EITHER min-size property is assigned (internally calling
        ''' ApplyPanel1MinSize/ApplyPanel2MinSize), and at construction time
        ''' -- before the control is parented/laid out -- its Width is still
        ''' the tiny constructor default, so even just setting Panel2MinSize
        ''' to something like 300 can overflow. Callers must NOT set
        ''' Panel1MinSize, Panel2MinSize, or SplitterDistance themselves;
        ''' this defers all three together until the control has a real,
        ''' adequately large width, then clamps into range regardless.
        ''' </summary>
        Public Sub ConfigureSplitter(split As SplitContainer, panel1MinSize As Integer, panel2MinSize As Integer, desiredDistance As Integer)
            Dim applied As Boolean = False
            Dim handler As EventHandler = Nothing
            handler =
                Sub(s, e)
                    If applied Then Return
                    If split.Width < panel1MinSize + panel2MinSize + split.SplitterWidth + 10 Then Return ' still not usably sized yet
                    split.Panel1MinSize = panel1MinSize
                    split.Panel2MinSize = panel2MinSize
                    Dim maxAllowed = split.Width - panel2MinSize
                    split.SplitterDistance = Math.Max(panel1MinSize, Math.Min(desiredDistance, maxAllowed))
                    applied = True
                    RemoveHandler split.SizeChanged, handler
                End Sub
            AddHandler split.SizeChanged, handler
            handler.Invoke(split, EventArgs.Empty) ' apply immediately if already sized
        End Sub

        ''' <summary>
        ''' Removes and disposes every child of a container. Plain
        ''' Controls.Clear() only detaches children -- it never calls
        ''' Dispose, so their native window handles (and anything they in
        ''' turn hold, like a DataGridView's cells) leak until the GC
        ''' finalizes them. Use this instead of Controls.Clear() wherever a
        ''' content panel gets rebuilt repeatedly (navigation, tab/report
        ''' switching), since that's exactly the pattern that accumulates.
        ''' </summary>
        Public Sub ClearAndDispose(container As Control)
            While container.Controls.Count > 0
                Dim c = container.Controls(0)
                container.Controls.RemoveAt(0)
                c.Dispose()
            End While
        End Sub

        ''' <summary>
        ''' Wires a search TextBox so typing doesn't fire a database query on
        ''' every keystroke. With a few thousand rows and a search screen
        ''' that re-queries on TextChanged directly, typing an 8-letter name
        ''' fires 8 full table scans (search columns use LIKE '%term%', which
        ''' can't use a B-tree index, so each one really does scan the whole
        ''' table). This restarts a short timer on every keystroke and only
        ''' runs the search once typing actually pauses.
        ''' </summary>
        Public Sub AttachDebouncedSearch(textBox As TextBox, action As Action, Optional delayMs As Integer = 350)
            Dim timer As New Timer With {.Interval = delayMs}
            AddHandler timer.Tick,
                Sub()
                    timer.Stop()
                    action()
                End Sub
            AddHandler textBox.TextChanged,
                Sub()
                    timer.Stop()
                    timer.Start()
                End Sub
            AddHandler textBox.Disposed, Sub() timer.Dispose()
        End Sub

        ''' <summary>
        ''' Default row cap for list screens (Patient Directory, Encounters,
        ''' Laboratory, etc.). With 10,000+ rows in a table, loading
        ''' "everything" into a DataGridView the moment a screen opens (no
        ''' search text yet) is both slow and useless -- nobody scrolls
        ''' through 10,000 rows. Every list query should add "LIMIT {ResultCap}"
        ''' and report via FormatCountLabel so hitting the cap is visible
        ''' rather than silently truncated.
        ''' </summary>
        Public Const ResultCap As Integer = 300

        ''' <summary>Shows the row count, and -- only when the cap was actually hit -- a hint that narrowing the search will reveal more.</summary>
        Public Function FormatCountLabel(rowCount As Integer, noun As String) As String
            If rowCount >= ResultCap Then
                Return $"Showing first {rowCount} {noun}(s) -- type to search for more specific results."
            End If
            Return $"{rowCount} {noun}(s)"
        End Function

        ''' <summary>
        ''' Turns a ComboBox into a type-to-search picker backed by the
        ''' database instead of one preloaded with every row up front.
        ''' Preloading is fine for a lookup table with a dozen rows
        ''' (barangays, roles) but not for patients/households at 10,000+
        ''' rows -- populating that many ComboBox items is slow AND useless,
        ''' since nobody scrolls a 10,000-item dropdown to find one name.
        ''' Debounced the same way as AttachDebouncedSearch, then re-queries
        ''' `loadResults` (typically "...LIKE @Search... LIMIT 20") and
        ''' refills Items, reopening the dropdown so results are visible.
        ''' </summary>
        Public Sub AttachSearchableCombo(combo As ComboBox, minChars As Integer, loadResults As Func(Of String, List(Of Object)))
            combo.DropDownStyle = ComboBoxStyle.DropDown
            combo.AutoCompleteMode = AutoCompleteMode.None
            Dim timer As New Timer With {.Interval = 300}
            AddHandler timer.Tick,
                Sub()
                    timer.Stop()
                    Dim text = combo.Text
                    If text.Trim().Length < minChars Then Return
                    Dim results = loadResults(text)
                    Dim caret = combo.SelectionStart
                    combo.Items.Clear()
                    For Each r In results
                        combo.Items.Add(r)
                    Next
                    combo.Text = text
                    Try
                        combo.SelectionStart = caret
                        combo.SelectionLength = 0
                    Catch
                        ' harmless if the control doesn't like the caret position we ask for
                    End Try
                    If combo.Items.Count > 0 Then combo.DroppedDown = True
                End Sub
            AddHandler combo.TextChanged,
                Sub()
                    timer.Stop()
                    timer.Start()
                End Sub
            AddHandler combo.Disposed, Sub() timer.Dispose()
        End Sub

        ''' <summary>
        ''' Keeps one control pinned to its container's right edge. Anchor =
        ''' Top|Right looks like the "correct" WinForms way to do this, but
        ''' it's unreliable in this app's shell pattern: every module form is
        ''' constructed (InitializeComponent runs, children get Anchor set)
        ''' BEFORE MainDashboardForm embeds it and gives it its real size --
        ''' so the anchor's "distance from the right edge" gets captured
        ''' against the control's tiny un-parented default width, and the
        ''' button ends up positioned far off to the right once the real
        ''' size kicks in (invisible, not clipped -- looks like a missing
        ''' button, not a broken one). This repositions on every resize
        ''' instead of relying on a one-time captured distance.
        ''' </summary>
        Public Sub PinToRight(control As Control, container As Control, rightMargin As Integer, Optional top As Integer = -1)
            Dim reposition As EventHandler =
                Sub(s, e)
                    control.Left = Math.Max(container.Width - control.Width - rightMargin, 0)
                    If top >= 0 Then control.Top = top
                End Sub
            AddHandler container.SizeChanged, reposition
            ' AutoSize controls (labels whose Text is set after construction) change their own
            ' Width later without the container ever resizing -- re-run then too, or the label
            ' ends up positioned using its old (often near-empty) width.
            AddHandler control.SizeChanged, reposition
            reposition.Invoke(container, EventArgs.Empty)
        End Sub

        ''' <summary>Same idea as PinToRight, for a horizontal row of several controls: the first one ends up flush against the right margin, each further one stacks to its left.</summary>
        Public Sub PinRowToRight(container As Control, top As Integer, rightMargin As Integer, spacing As Integer, ParamArray controls() As Control)
            Dim reposition As EventHandler =
                Sub(s, e)
                    Dim x = container.Width - rightMargin
                    For Each c In controls
                        x -= c.Width
                        c.Left = x
                        c.Top = top
                        x -= spacing
                    Next
                End Sub
            AddHandler container.SizeChanged, reposition
            reposition.Invoke(container, EventArgs.Empty)
        End Sub

        Public Sub StyleGrid(grid As DataGridView)
            grid.BackgroundColor = Color.White
            grid.BorderStyle = BorderStyle.None
            grid.EnableHeadersVisualStyles = False
            grid.ColumnHeadersDefaultCellStyle.BackColor = BackgroundLight
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark
            grid.ColumnHeadersDefaultCellStyle.Font = BoldFont
            grid.ColumnHeadersHeight = 34
            grid.DefaultCellStyle.SelectionBackColor = AccentGreen
            grid.DefaultCellStyle.SelectionForeColor = Color.White
            grid.DefaultCellStyle.Padding = New Padding(4)
            grid.RowTemplate.Height = 30
            grid.GridColor = BorderLight
            grid.AllowUserToAddRows = False
            grid.AllowUserToDeleteRows = False
            grid.ReadOnly = True
            grid.RowHeadersVisible = False
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            grid.MultiSelect = False
            grid.Font = BaseFont
        End Sub
    End Module

End Namespace
