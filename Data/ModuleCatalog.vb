Namespace Data

    ''' <summary>Canonical list of sidebar module keys (must match MainDashboardForm's nav items and roles.module_access values).</summary>
    Public Module ModuleCatalog

        Public ReadOnly Property Modules As (Key As String, Label As String)()
            Get
                Return New (String, String)() {
                    ("dashboard", "Dashboard"),
                    ("patients", "Patient Directory"),
                    ("registration", "Register Patient"),
                    ("family", "Family / Household"),
                    ("encounters", "Encounters / Triage / Consultation"),
                    ("lab", "Laboratory"),
                    ("pharmacy", "Pharmacy"),
                    ("inventory", "Medicine Inventory"),
                    ("certificates", "Medical Certificates"),
                    ("reports", "Reports"),
                    ("users", "User Management"),
                    ("roles", "Roles & Permissions"),
                    ("audit", "Audit Logs")
                }
            End Get
        End Property

    End Module

End Namespace
