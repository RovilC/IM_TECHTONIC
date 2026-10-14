Namespace Data

    ''' <summary>
    ''' One patient's full record summary (demographics, household, visits,
    ''' diagnoses, labs, medicines, certificates, referrals). Shared by
    ''' PatientProfileForm's read-only tabs and PatientSummaryPrinter so the
    ''' screen and the printout always show the same data.
    ''' </summary>
    Public Module PatientSummaryData

        Public Function LoadPatient(patientId As Integer) As DataRow
            Dim table = Db.Query(
                "SELECT p.*, b.barangay_name,
                        TIMESTAMPDIFF(YEAR, p.date_of_birth, CURDATE()) AS age,
                        f.household_no, f.household_status,
                        CONCAT(head.last_name, ', ', head.first_name) AS household_head,
                        (SELECT COUNT(*) FROM patients m WHERE m.family_id = p.family_id) AS household_members
                 FROM patients p
                 JOIN barangays b ON b.barangay_id = p.barangay_id
                 LEFT JOIN families_households f ON f.family_id = p.family_id
                 LEFT JOIN patients head ON head.patient_id = f.family_head_id
                 WHERE p.patient_id = @Id",
                New With {.Id = patientId})
            Return If(table.Rows.Count = 0, Nothing, table.Rows(0))
        End Function

        ''' <summary>(Label, Value) pairs for the patient information block.</summary>
        Public Function PatientInfoFields(r As DataRow) As List(Of (Label As String, Value As String))
            Dim v = Function(col As String) As String
                        If IsDBNull(r(col)) Then Return "-"
                        Dim s = r(col).ToString().Trim()
                        Return If(s.Length = 0, "-", s)
                    End Function
            Dim membership = If(Convert.ToBoolean(r("is_4ps_member")), "4Ps Member", If(Convert.ToBoolean(r("is_indigent")), "Indigent", "Not a Member"))
            Dim household = If(IsDBNull(r("household_no")), "Not linked to a household",
                               $"{r("household_no")}  (Head: {v("household_head")}{If(Convert.ToBoolean(r("family_head_or_no")), " - this patient", "")};  {r("household_members")} member(s))")
            Return New List(Of (String, String)) From {
                ("Patient No.", v("patient_no")),
                ("Full Name", $"{r("last_name")}, {r("first_name")} {If(IsDBNull(r("middle_name")), "", r("middle_name"))} {If(IsDBNull(r("suffix")), "", r("suffix"))}".Trim()),
                ("Date of Birth", $"{Convert.ToDateTime(r("date_of_birth")):MMMM dd, yyyy}  ({r("age")} yrs old)"),
                ("Sex / Civil Status", $"{v("sex")} / {v("civil_status")}"),
                ("Occupation", v("occupation")),
                ("Contact No.", $"{v("contact_number")}{If(IsDBNull(r("alternate_contact")), "", "  /  " & r("alternate_contact").ToString())}"),
                ("Address", $"{If(IsDBNull(r("address")), "", r("address").ToString() & ", ")}{r("barangay_name")}, Vinzons, Camarines Norte"),
                ("Household", household),
                ("Emergency Contact", If(IsDBNull(r("emergency_contact_name")), "-", $"{r("emergency_contact_name")} ({v("emergency_relationship")}) - {v("emergency_contact_no")}")),
                ("PhilHealth", If(IsDBNull(r("philhealth_no")), "-", $"{r("philhealth_no")}  ({v("philhealth_status")} / {v("philhealth_category")})")),
                ("Membership", membership),
                ("Blood Type", v("blood_type")),
                ("Known Allergies", v("known_allergies")),
                ("Existing Conditions", v("existing_conditions")),
                ("Date Registered", $"{Convert.ToDateTime(r("date_registered")):MMMM dd, yyyy}")
            }
        End Function

        ''' <summary>Every history section, newest first. The Encounters table carries a hidden "Id" column (encounter_id) for drill-in.</summary>
        Public Function LoadSections(patientId As Integer) As List(Of (Title As String, Table As DataTable))
            Dim p = New With {.Id = patientId}
            Return New List(Of (String, DataTable)) From {
                ("Encounters / Visits", Db.Query(
                    "SELECT e.encounter_id AS Id, e.encounter_date AS Date, e.encounter_no AS 'Encounter No.', e.visit_type AS 'Visit Type',
                            e.chief_complaint AS 'Chief Complaint',
                            CONCAT_WS('  ',
                                IF(e.triage_bp_systolic IS NULL, NULL, CONCAT('BP ', e.triage_bp_systolic, '/', e.triage_bp_diastolic)),
                                IF(e.triage_temp_c IS NULL, NULL, CONCAT('T ', e.triage_temp_c, 'C')),
                                IF(e.triage_heart_rate IS NULL, NULL, CONCAT('HR ', e.triage_heart_rate)),
                                IF(e.triage_weight_kg IS NULL, NULL, CONCAT('Wt ', e.triage_weight_kg, 'kg'))) AS Vitals,
                            e.status AS Status, u.full_name AS 'Attending Staff'
                     FROM encounters e LEFT JOIN users_staff u ON u.user_id = e.created_by
                     WHERE e.patient_id = @Id ORDER BY e.encounter_date DESC", p)),
                ("Diagnoses", Db.Query(
                    "SELECT d.created_at AS Date, d.icd_code AS 'ICD Code', d.diagnosis_desc AS Diagnosis, d.classification AS Classification,
                            d.status AS Status, u.full_name AS Doctor
                     FROM diagnoses d
                     JOIN consultations c ON c.consultation_id = d.consultation_id
                     JOIN encounters e ON e.encounter_id = c.encounter_id
                     LEFT JOIN users_staff u ON u.user_id = c.doctor_id
                     WHERE e.patient_id = @Id ORDER BY d.created_at DESC", p)),
                ("Consultations", Db.Query(
                    "SELECT c.consultation_date AS Date, u.full_name AS Doctor, c.assessment AS Assessment, c.treatment_plan AS 'Treatment Plan',
                            c.follow_up_date AS 'Follow-up', c.status AS Status
                     FROM consultations c JOIN encounters e ON e.encounter_id = c.encounter_id
                     LEFT JOIN users_staff u ON u.user_id = c.doctor_id
                     WHERE e.patient_id = @Id ORDER BY c.consultation_date DESC", p)),
                ("Laboratory Results", Db.Query(
                    "SELECT l.request_date AS Requested, l.test_name AS Test, l.status AS Status,
                            COALESCE(l.result, IF(l.status = 'Referred', CONCAT('Referred to ', l.external_facility), NULL)) AS Result,
                            l.result_source AS 'Result From', l.result_clinic AS 'Clinic / Laboratory', l.result_doctor AS 'Doctor / Pathologist',
                            l.result_date AS 'Result Date'
                     FROM laboratory l JOIN encounters e ON e.encounter_id = l.encounter_id
                     WHERE e.patient_id = @Id ORDER BY l.request_date DESC", p)),
                ("Medicines Dispensed", Db.Query(
                    "SELECT d.date_dispensed AS Date, d.medicine_name AS Medicine, d.quantity_dispensed AS Qty, d.dosage AS Dosage,
                            d.instruction AS Instructions, u.full_name AS 'Dispensed By'
                     FROM pharmacy_dispensing d LEFT JOIN users_staff u ON u.user_id = d.dispensed_by
                     WHERE d.patient_id = @Id ORDER BY d.date_dispensed DESC", p)),
                ("Prescriptions", Db.Query(
                    "SELECT rx.medication_date AS Date, rx.prescription_number AS 'Rx No.', rx.medicine_name AS Medicine, rx.dosage AS Dosage,
                            rx.frequency AS Frequency, rx.quantity AS Qty, rx.status AS Status
                     FROM prescriptions_medication_orders rx JOIN encounters e ON e.encounter_id = rx.encounter_id
                     WHERE e.patient_id = @Id ORDER BY rx.medication_date DESC", p)),
                ("Medical Certificates", Db.Query(
                    "SELECT doc.created_at AS Date, doc.reference_no AS 'Certificate No.', doc.purpose AS Purpose, doc.status AS Status
                     FROM other_documents_services doc JOIN encounters e ON e.encounter_id = doc.encounter_id
                     WHERE e.patient_id = @Id AND doc.document_type = 'Medical Certificate' ORDER BY doc.created_at DESC", p)),
                ("Referrals", Db.Query(
                    "SELECT r.referral_date AS Date, r.referred_to_facility AS 'Referred To', r.referral_type AS Type, r.reason_for_referral AS Reason
                     FROM referrals r JOIN encounters e ON e.encounter_id = r.encounter_id
                     WHERE e.patient_id = @Id ORDER BY r.referral_date DESC", p))
            }
        End Function

    End Module

End Namespace
