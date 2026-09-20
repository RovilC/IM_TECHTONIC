Imports System.Drawing
Imports System.Windows.Forms
Imports TechTonic_IM_RHU.Data

Namespace Forms

    ''' <summary>Medicine catalog + stock batches (Figure 33): add medicines, receive stock, monitor low/expiring stock.</summary>
    Public Class MedicineInventoryForm
        Inherits Form

        Private gridMedicines, gridBatches As DataGridView
        Private lblSelected As Label
        Private selectedMedicineId As Integer? = Nothing
        Private selectedMedicineName As String = ""
        Private btnReceiveStock As Button

        Public Sub New()
            InitializeComponent()
            LoadMedicines()
        End Sub

        Private Sub InitializeComponent()
            Me.BackColor = Color.White
            Me.Font = UiTheme.BaseFont

            Dim toolbar As New Panel With {.Dock = DockStyle.Top, .Height = 50}
            Me.Controls.Add(toolbar)
            Dim btnAddMedicine As New Button With {.Text = "+ Add Medicine", .Size = New Size(150, 34), .Location = New Point(0, 8)}
            UiTheme.StylePrimaryButton(btnAddMedicine)
            AddHandler btnAddMedicine.Click, AddressOf BtnAddMedicine_Click
            toolbar.Controls.Add(btnAddMedicine)

            Dim split As New SplitContainer With {.Dock = DockStyle.Fill}
            Me.Controls.Add(split)
            split.BringToFront()
            UiTheme.ConfigureSplitter(split, 450, 300, 700)

            Dim lblMedicines As New Label With {.Text = "Medicine Catalog", .Font = UiTheme.SubHeaderFont, .Dock = DockStyle.Top, .Height = 30}
            split.Panel1.Controls.Add(lblMedicines)

            gridMedicines = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridMedicines)
            AddHandler gridMedicines.SelectionChanged, AddressOf GridMedicines_SelectionChanged
            split.Panel1.Controls.Add(gridMedicines)
            gridMedicines.BringToFront()

            lblSelected = New Label With {.Text = "Stock Batches -- select a medicine on the left to view its batches", .Font = UiTheme.SubHeaderFont, .Dock = DockStyle.Top, .Height = 30}
            split.Panel2.Controls.Add(lblSelected)

            gridBatches = New DataGridView With {.Dock = DockStyle.Fill}
            UiTheme.StyleGrid(gridBatches)
            split.Panel2.Controls.Add(gridBatches)
            gridBatches.BringToFront()

            btnReceiveStock = New Button With {.Text = "+ Receive Stock", .Dock = DockStyle.Bottom, .Height = 40, .Enabled = False}
            UiTheme.StylePrimaryButton(btnReceiveStock)
            AddHandler btnReceiveStock.Click, AddressOf BtnReceiveStock_Click
            split.Panel2.Controls.Add(btnReceiveStock)
        End Sub

        Private Sub LoadMedicines()
            Try
                Dim table = Db.Query(
                    "SELECT m.medicine_id AS Id, m.medicine_name AS Medicine, m.brand AS Brand, m.category AS Category, m.unit AS Unit,
                            m.minimum_stock_level AS 'Min. Stock Level',
                            COALESCE((SELECT SUM(b.quantity_remaining) FROM medicine_stock_batches b WHERE b.medicine_id = m.medicine_id), 0) AS 'On Hand',
                            (SELECT MIN(b2.expiration_date) FROM medicine_stock_batches b2 WHERE b2.medicine_id = m.medicine_id AND b2.quantity_remaining > 0) AS 'Nearest Expiry',
                            CASE
                                WHEN COALESCE((SELECT SUM(b3.quantity_remaining) FROM medicine_stock_batches b3 WHERE b3.medicine_id = m.medicine_id), 0) <= 0 THEN 'Out of Stock'
                                WHEN COALESCE((SELECT SUM(b4.quantity_remaining) FROM medicine_stock_batches b4 WHERE b4.medicine_id = m.medicine_id), 0) < m.minimum_stock_level THEN 'Low Stock'
                                ELSE 'Sufficient'
                            END AS Status
                     FROM medicines m
                     WHERE m.status = 'Active'
                     ORDER BY m.medicine_name")
                gridMedicines.DataSource = table
                If gridMedicines.Columns.Contains("Id") Then gridMedicines.Columns("Id").Visible = False
            Catch ex As Exception
                MessageBox.Show("Could not load medicines: " & ex.Message)
            End Try
        End Sub

        Private Sub GridMedicines_SelectionChanged(sender As Object, e As EventArgs)
            If gridMedicines.CurrentRow Is Nothing Then Return
            selectedMedicineId = Convert.ToInt32(gridMedicines.CurrentRow.Cells("Id").Value)
            selectedMedicineName = gridMedicines.CurrentRow.Cells("Medicine").Value.ToString()
            lblSelected.Text = "Stock Batches -- " & selectedMedicineName
            btnReceiveStock.Enabled = True
            LoadBatches()
        End Sub

        Private Sub LoadBatches()
            If Not selectedMedicineId.HasValue Then Return
            Try
                gridBatches.DataSource = Db.Query(
                    "SELECT batch_lot_no AS 'Lot No.', quantity_received AS Received, quantity_remaining AS Remaining,
                            source AS Source, expiration_date AS Expires, date_received AS 'Date Received'
                     FROM medicine_stock_batches WHERE medicine_id = @Id ORDER BY expiration_date ASC",
                    New With {.Id = selectedMedicineId.Value})
            Catch ex As Exception
                MessageBox.Show("Could not load batches: " & ex.Message)
            End Try
        End Sub

        Private Sub BtnAddMedicine_Click(sender As Object, e As EventArgs)
            Using dlg As New AddMedicineDialog()
                If dlg.ShowDialog(Me) = DialogResult.OK Then LoadMedicines()
            End Using
        End Sub

        Private Sub BtnReceiveStock_Click(sender As Object, e As EventArgs)
            If Not selectedMedicineId.HasValue Then Return
            Using dlg As New ReceiveStockDialog(selectedMedicineId.Value, selectedMedicineName)
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    LoadMedicines()
                    LoadBatches()
                End If
            End Using
        End Sub

    End Class

End Namespace
