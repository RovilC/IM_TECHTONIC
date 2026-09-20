Namespace Data

    ''' <summary>
    ''' Generates the human-readable codes shown throughout the mockups
    ''' (PT-2026-0001, ENC-2026-0001, HH-2026-001, RX-2026-0001) by counting
    ''' existing rows for the current year. Good enough for single-RHU,
    ''' single-writer usage; not safe under heavy concurrent inserts, but
    ''' neither is a single desktop-app RHU workstation.
    ''' </summary>
    Public Module IdGenerator

        Public Function NextPatientNo() As String
            Dim year = DateTime.Now.Year
            Dim count = Convert.ToInt64(Db.QueryScalar("SELECT COUNT(*) FROM patients WHERE YEAR(date_registered) = @Year", New With {.Year = year}))
            Return $"PT-{year}-{(count + 1):D4}"
        End Function

        Public Function NextEncounterNo() As String
            Dim year = DateTime.Now.Year
            Dim count = Convert.ToInt64(Db.QueryScalar("SELECT COUNT(*) FROM encounters WHERE YEAR(encounter_date) = @Year", New With {.Year = year}))
            Return $"ENC-{year}-{(count + 1):D4}"
        End Function

        Public Function NextHouseholdNo() As String
            Dim year = DateTime.Now.Year
            Dim count = Convert.ToInt64(Db.QueryScalar("SELECT COUNT(*) FROM families_households WHERE YEAR(date_registered) = @Year", New With {.Year = year}))
            Return $"HH-{year}-{(count + 1):D3}"
        End Function

        Public Function NextPrescriptionNo() As String
            Dim year = DateTime.Now.Year
            Dim count = Convert.ToInt64(Db.QueryScalar("SELECT COUNT(*) FROM prescriptions_medication_orders WHERE YEAR(medication_date) = @Year", New With {.Year = year}))
            Return $"RX-{year}-{(count + 1):D4}"
        End Function

    End Module

End Namespace
