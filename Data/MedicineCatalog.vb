Namespace Data

    ''' <summary>Static catalog of common RHU medicines (name, category, unit, common brand names), used to seed the Add Medicine dialog's autocomplete suggestions before the database has any entries of its own.</summary>
    Public Module MedicineCatalog

        Public Class Entry
            Public Property Name As String
            Public Property Category As String
            Public Property Unit As String
            Public Property Brands As String()
            Public Sub New(name As String, category As String, unit As String, ParamArray brands() As String)
                Me.Name = name
                Me.Category = category
                Me.Unit = unit
                Me.Brands = brands
            End Sub
        End Class

        Public ReadOnly Property Entries As List(Of Entry)
            Get
                Return New List(Of Entry) From {
                    New Entry("Paracetamol 500mg", "Analgesic/Antipyretic", "tablet", "Biogesic", "Calpol", "Tempra"),
                    New Entry("Amoxicillin 500mg", "Antibiotic", "capsule", "Amoxil", "Hi-Amox", "Ambimox"),
                    New Entry("Losartan 50mg", "Antihypertensive", "tablet", "Cozaar", "Lifezar", "Zartan"),
                    New Entry("Metformin 500mg", "Antidiabetic", "tablet", "Glucophage", "Neoform"),
                    New Entry("Amlodipine 5mg", "Antihypertensive", "tablet", "Norvasc", "Amlogard"),
                    New Entry("Ibuprofen 400mg", "Analgesic/Anti-inflammatory", "tablet", "Advil", "Medicol"),
                    New Entry("Cefalexin 500mg", "Antibiotic", "capsule", "Keflex"),
                    New Entry("Ascorbic Acid 500mg", "Vitamin", "tablet", "Cecon", "Poten-Cee"),
                    New Entry("Ferrous Sulfate + Folic Acid", "Hematinic", "tablet", "FerroPlus"),
                    New Entry("Multivitamins", "Vitamin", "tablet", "Enervon", "Centrum"),
                    New Entry("Cotrimoxazole 800/160mg", "Antibiotic", "tablet", "Bactrim", "Trimoxsul"),
                    New Entry("Salbutamol Inhaler", "Bronchodilator", "inhaler", "Ventolin", "Asmalin"),
                    New Entry("Mefenamic Acid 500mg", "Analgesic", "tablet", "Ponstan"),
                    New Entry("Omeprazole 20mg", "Antacid", "capsule", "Losec", "Omepron"),
                    New Entry("Metronidazole 500mg", "Antibiotic", "tablet", "Flagyl"),
                    New Entry("Loperamide 2mg", "Antidiarrheal", "tablet", "Imodium", "Diatabs"),
                    New Entry("Oral Rehydration Salts", "Electrolyte", "sachet", "Hydrite", "Pedialyte"),
                    New Entry("Paracetamol Syrup 250mg/5mL", "Analgesic/Antipyretic", "bottle", "Tempra Syrup", "Biogesic Syrup"),
                    New Entry("Amoxicillin Syrup 250mg/5mL", "Antibiotic", "bottle", "Amoxil Syrup"),
                    New Entry("Simvastatin 20mg", "Antihyperlipidemic", "tablet", "Zocor"),
                    New Entry("Enalapril 5mg", "Antihypertensive", "tablet", "Renitec"),
                    New Entry("Glibenclamide 5mg", "Antidiabetic", "tablet", "Euglucon"),
                    New Entry("Hydrochlorothiazide 25mg", "Antihypertensive", "tablet", "Aquazide"),
                    New Entry("Isoniazid 300mg", "Anti-TB", "tablet", "Isonarif"),
                    New Entry("Rifampicin 300mg", "Anti-TB", "capsule", "Rifacin"),
                    New Entry("Ethambutol 400mg", "Anti-TB", "tablet", "Myambutol"),
                    New Entry("Pyrazinamide 500mg", "Anti-TB", "tablet", "Pyrafat"),
                    New Entry("Zinc Sulfate 20mg", "Mineral Supplement", "tablet", "Zinkid"),
                    New Entry("Vitamin A 200,000 IU", "Vitamin", "capsule", "Aquasol A"),
                    New Entry("Erythromycin 500mg", "Antibiotic", "tablet", "Ilosone"),
                    New Entry("Diphenhydramine 50mg", "Antihistamine", "tablet", "Benadryl"),
                    New Entry("Betamethasone Cream", "Topical Steroid", "tube", "Diprosone"),
                    New Entry("Cotrimoxazole Syrup", "Antibiotic", "bottle", "Bactrim Syrup"),
                    New Entry("Tranexamic Acid 500mg", "Antihemorrhagic", "tablet", "Hemostan"),
                    New Entry("Cefuroxime 500mg", "Antibiotic", "tablet", "Zinnat")
                }
            End Get
        End Property

        ''' <summary>Fixed list of dispensing units for the Unit dropdown -- covers every unit used across the seeded catalog above.</summary>
        Public ReadOnly Property Units As String()
            Get
                Return New String() {"tablet", "capsule", "bottle", "vial", "ampule", "sachet", "tube", "box", "inhaler", "drops", "syrup", "patch", "pack", "strip"}
            End Get
        End Property

    End Module

End Namespace
