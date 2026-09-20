Namespace Data

    ''' <summary>Static test catalog for the Laboratory Request screen (Figure 30: Hematology / Clinical Chemistry sections).</summary>
    Public Module LabTestCatalog

        Public ReadOnly Property Categories As List(Of (Category As String, Tests As String()))
            Get
                Return New List(Of (String, String())) From {
                    ("Hematology", New String() {"Complete Blood Count (CBC)", "CBC with Platelet Count", "Hemoglobin (Hgb)"}),
                    ("Clinical Chemistry", New String() {"Fasting Blood Sugar (FBS)", "Random Blood Sugar (RBS)", "HbA1c (Glycated Hemoglobin)", "Total Cholesterol", "Lipid Profile", "Creatinine", "BUN"}),
                    ("Urinalysis", New String() {"Routine Urinalysis"}),
                    ("Microbiology", New String() {"Sputum Microscopy / TB LAMP", "Xpert MTB/RIF"}),
                    ("Other", New String() {"Pregnancy Test", "Chest X-ray"})
                }
            End Get
        End Property

    End Module

End Namespace
