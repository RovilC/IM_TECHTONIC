-- ============================================================================
-- Project C.A.R.E (Centralized Access to Records and Entries)
-- Rural Health Unit - Vinzons, Camarines Norte
-- MySQL Database Schema
--
-- Built from the project proposal's Data Dictionary (Tables 5-16) and
-- Entity-Relationship Diagram. Normalized to 3NF as described in the
-- proposal (barangay/role/family lookups instead of duplicated strings).
--
-- A few tables/columns extend beyond the literal data-dictionary appendix
-- because the proposal's own UI mockups and reports require them (e.g. a
-- structured DIAGNOSES table so the Morbidity Report can aggregate by
-- condition instead of parsing free text, or MEDICINES/stock batches so the
-- Medicine Inventory Report and expiration tracking are possible at all).
-- Each such extension is called out in a comment at the point it appears.
--
-- Import with:
--   mysql -u root -p < schema.sql
-- or via phpMyAdmin (XAMPP) -> Import.
-- ============================================================================

CREATE DATABASE IF NOT EXISTS rhu_care
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE rhu_care;

SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------------------------------------------------------
-- L. ROLES  (Data Dictionary Table 16)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS roles;
CREATE TABLE roles (
    role_id        INT AUTO_INCREMENT PRIMARY KEY,
    role_name      VARCHAR(50)  NOT NULL UNIQUE,
    description    TEXT NULL,
    module_access  TEXT NULL,                 -- comma-separated module keys this role may open
    can_add        BOOLEAN NOT NULL DEFAULT FALSE,
    can_edit       BOOLEAN NOT NULL DEFAULT FALSE,
    can_delete     BOOLEAN NOT NULL DEFAULT FALSE,
    can_view       BOOLEAN NOT NULL DEFAULT TRUE,
    status         VARCHAR(20) NOT NULL DEFAULT 'Active',
    created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- K. USERS / STAFF  (Data Dictionary Table 15)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS users_staff;
CREATE TABLE users_staff (
    user_id         INT AUTO_INCREMENT PRIMARY KEY,
    full_name       VARCHAR(100) NOT NULL,
    username        VARCHAR(50)  NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,     -- PBKDF2 hash, see Security/PasswordHasher.vb
    role_id         INT NOT NULL,
    contact_number  VARCHAR(20)  NULL,
    email           VARCHAR(100) NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'Active',
    date_created    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login      DATETIME NULL,
    CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles(role_id)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- A. BARANGAYS  (Data Dictionary Table 5)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS barangays;
CREATE TABLE barangays (
    barangay_id    INT AUTO_INCREMENT PRIMARY KEY,
    barangay_name  VARCHAR(100) NOT NULL UNIQUE,
    municipality   VARCHAR(100) NOT NULL DEFAULT 'Vinzons',
    province       VARCHAR(100) NOT NULL DEFAULT 'Camarines Norte',
    region         VARCHAR(100) NOT NULL DEFAULT 'Region V',
    zip_code       VARCHAR(20)  NULL,
    barangay_code  VARCHAR(20)  NULL,
    date_created   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    date_updated   DATETIME NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- B. FAMILIES / HOUSEHOLDS  (Data Dictionary Table 6)
-- family_head_id references PATIENTS, which itself references this table,
-- so the FK is added after both tables exist (see ALTER TABLE below).
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS families_households;
CREATE TABLE families_households (
    family_id         INT AUTO_INCREMENT PRIMARY KEY,
    household_no      VARCHAR(20) NOT NULL UNIQUE,
    family_head_id    INT NULL,               -- FK added after patients table is created
    address           VARCHAR(255) NULL,
    barangay_id       INT NOT NULL,
    municipality      VARCHAR(100) NOT NULL DEFAULT 'Vinzons',
    province          VARCHAR(100) NOT NULL DEFAULT 'Camarines Norte',
    household_status  VARCHAR(20) NULL,
    date_registered   DATE NOT NULL,
    date_updated      DATETIME NULL,
    remarks           TEXT NULL,
    CONSTRAINT fk_family_barangay FOREIGN KEY (barangay_id) REFERENCES barangays(barangay_id)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- C. PATIENTS  (Data Dictionary Table 7)
-- Extended with contact/emergency/clinical-flag columns that the
-- Registration wizard mockups (Figures 6-9) and reports collect but the
-- appendix table omitted (contact_number, occupation, allergies, etc.).
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS patients;
CREATE TABLE patients (
    patient_id            INT AUTO_INCREMENT PRIMARY KEY,
    patient_no            VARCHAR(20) NOT NULL UNIQUE,
    first_name            VARCHAR(50) NOT NULL,
    middle_name           VARCHAR(50) NULL,
    last_name             VARCHAR(50) NOT NULL,
    suffix                VARCHAR(10) NULL,
    date_of_birth         DATE NOT NULL,
    sex                   VARCHAR(10) NOT NULL,
    civil_status           VARCHAR(20) NULL,
    occupation            VARCHAR(100) NULL,
    address               VARCHAR(255) NULL,
    barangay_id           INT NOT NULL,
    family_id             INT NULL,
    philhealth_no         VARCHAR(50) NULL,
    philhealth_category   VARCHAR(50) NULL,
    philhealth_status     VARCHAR(20) NULL,     -- Member / Dependent
    client_type           VARCHAR(50) NULL,
    family_head_or_no     BOOLEAN NOT NULL DEFAULT FALSE,
    for_24_to_0_mos       VARCHAR(50) NULL,      -- age-category indicator for infants (0-24 mos)
    -- contact & emergency contact (Figure 8)
    contact_number        VARCHAR(20)  NULL,
    alternate_contact     VARCHAR(20)  NULL,
    emergency_contact_name VARCHAR(100) NULL,
    emergency_relationship VARCHAR(50)  NULL,
    emergency_contact_no  VARCHAR(20)  NULL,
    -- health information (Figure 9)
    blood_type            VARCHAR(5) NULL,
    is_4ps_member         BOOLEAN NOT NULL DEFAULT FALSE,
    is_indigent           BOOLEAN NOT NULL DEFAULT FALSE,
    known_allergies       TEXT NULL,
    existing_conditions   TEXT NULL,
    -- last recorded anthropometrics/vitals cache (updated by latest triage)
    height_cm             DECIMAL(5,2) NULL,
    weight_kg             DECIMAL(5,2) NULL,
    respiratory_rate      INT NULL,
    heart_rate            INT NULL,
    temp_celsius          DECIMAL(4,1) NULL,
    status                VARCHAR(20) NOT NULL DEFAULT 'Active',
    date_registered       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    date_updated          DATETIME NULL,
    registered_by         INT NULL,
    CONSTRAINT fk_patient_barangay FOREIGN KEY (barangay_id) REFERENCES barangays(barangay_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_patient_family FOREIGN KEY (family_id) REFERENCES families_households(family_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_patient_registered_by FOREIGN KEY (registered_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

ALTER TABLE families_households
    ADD CONSTRAINT fk_family_head FOREIGN KEY (family_head_id) REFERENCES patients(patient_id)
        ON UPDATE CASCADE ON DELETE SET NULL;

-- ----------------------------------------------------------------------------
-- D. ENCOUNTERS  (Data Dictionary Table 8)
-- Extended with visit_type/priority/referral_source/status and the triage
-- vital-sign columns (Figure 23). The proposal's business-process diagram
-- treats Triage as a step of one encounter rather than a separate entity,
-- and no "TRIAGE" table appears in the data dictionary, so vitals are kept
-- here, one row per visit, rather than only cached on PATIENTS.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS encounters;
CREATE TABLE encounters (
    encounter_id       INT AUTO_INCREMENT PRIMARY KEY,
    patient_id         INT NOT NULL,
    encounter_no       VARCHAR(20) NOT NULL UNIQUE,
    encounter_date     DATETIME NOT NULL,
    visit_type         VARCHAR(50) NULL,        -- e.g. Outpatient Consultation, Follow-up
    referral_source    VARCHAR(50) NULL,        -- e.g. Walk-in, Referred
    priority_level     VARCHAR(20) NULL,        -- Routine / Urgent / Emergency
    purpose            VARCHAR(255) NULL,
    chief_complaint    TEXT NULL,
    additional_notes   TEXT NULL,
    -- triage vitals (Figure 23), captured once per encounter
    triage_temp_c          DECIMAL(4,1) NULL,
    triage_bp_systolic     INT NULL,
    triage_bp_diastolic    INT NULL,
    triage_heart_rate      INT NULL,
    triage_respiratory_rate INT NULL,
    triage_spo2             DECIMAL(4,1) NULL,
    triage_weight_kg        DECIMAL(5,2) NULL,
    triage_height_cm        DECIMAL(5,2) NULL,
    triage_bmi               DECIMAL(5,2) NULL,
    triage_pain_scale        INT NULL,
    triage_general_appearance TEXT NULL,
    triage_classification     VARCHAR(50) NULL,
    triage_by            INT NULL,
    triage_at             DATETIME NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'Open',   -- Open / In Progress / Closed
    created_by          INT NOT NULL,
    created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at          DATETIME NULL,
    CONSTRAINT fk_encounter_patient FOREIGN KEY (patient_id) REFERENCES patients(patient_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_encounter_created_by FOREIGN KEY (created_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_encounter_triage_by FOREIGN KEY (triage_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- E. CONSULTATIONS  (Data Dictionary Table 9)
-- Extended with history/physical-exam narrative columns (Figures 24-26)
-- since SOAP note detail beyond subjective/objective/assessment/plan is
-- shown in the mockups but not itemized in the appendix table.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS consultations;
CREATE TABLE consultations (
    consultation_id      INT AUTO_INCREMENT PRIMARY KEY,
    encounter_id         INT NOT NULL,
    doctor_id            INT NOT NULL,
    consultation_date    DATETIME NOT NULL,
    chief_complaint      TEXT NULL,
    history_of_present_illness TEXT NULL,
    past_medical_history TEXT NULL,
    family_history        TEXT NULL,
    social_history         TEXT NULL,
    current_medications    TEXT NULL,
    -- physical examination by system (Figure 26)
    pe_general            TEXT NULL,
    pe_heent               TEXT NULL,
    pe_cardiovascular       TEXT NULL,
    pe_respiratory           TEXT NULL,
    pe_abdomen                TEXT NULL,
    pe_extremities              TEXT NULL,
    pe_neurological               TEXT NULL,
    subjective            TEXT NULL,
    objective             TEXT NULL,
    assessment            TEXT NULL,
    treatment_plan        TEXT NULL,
    follow_up_date        DATE NULL,
    follow_up_instructions TEXT NULL,
    status                VARCHAR(20) NOT NULL DEFAULT 'Draft',  -- Draft / Completed
    CONSTRAINT fk_consult_encounter FOREIGN KEY (encounter_id) REFERENCES encounters(encounter_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_consult_doctor FOREIGN KEY (doctor_id) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- Diagnoses -- extension table, not itemized separately in the appendix.
-- Figure 27 shows multiple ICD-coded diagnoses per consultation with a
-- chronic/acute classification and status; the Morbidity Report (Report #5,
-- Figure 35) requires exactly this structure to aggregate cases by
-- condition, so free-text-only storage on CONSULTATIONS is not sufficient.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS diagnoses;
CREATE TABLE diagnoses (
    diagnosis_id      INT AUTO_INCREMENT PRIMARY KEY,
    consultation_id   INT NOT NULL,
    icd_code          VARCHAR(20) NULL,
    diagnosis_desc    VARCHAR(255) NOT NULL,
    classification    VARCHAR(20) NULL,     -- Acute / Chronic
    status            VARCHAR(20) NOT NULL DEFAULT 'Active',
    created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_diagnosis_consultation FOREIGN KEY (consultation_id) REFERENCES consultations(consultation_id)
        ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- F. PRESCRIPTIONS / MEDICATION ORDERS  (Data Dictionary Table 10)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS prescriptions_medication_orders;
CREATE TABLE prescriptions_medication_orders (
    prescription_id      INT AUTO_INCREMENT PRIMARY KEY,
    prescription_number  VARCHAR(50) NOT NULL UNIQUE,
    encounter_id         INT NOT NULL,
    doctor_id            INT NOT NULL,
    medication_date      DATETIME NOT NULL,
    medicine_name        VARCHAR(100) NOT NULL,
    dosage               VARCHAR(100) NULL,
    frequency            VARCHAR(100) NULL,
    quantity             INT NOT NULL,
    instructions          TEXT NULL,
    status                VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending / Dispensed / Cancelled
    CONSTRAINT fk_rx_encounter FOREIGN KEY (encounter_id) REFERENCES encounters(encounter_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_rx_doctor FOREIGN KEY (doctor_id) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- Medicines & stock batches -- extension tables. The proposal's Pharmacy &
-- Inventory Staff role (batch/lot, expiration, stock monitoring) and the
-- Medicine Inventory Report (Figure 33) need a real stock ledger; the
-- appendix's PHARMACY_DISPENSING table alone has nowhere to record
-- incoming stock, batch/lot numbers, or expiration dates.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS medicines;
CREATE TABLE medicines (
    medicine_id          INT AUTO_INCREMENT PRIMARY KEY,
    medicine_name        VARCHAR(100) NOT NULL UNIQUE,
    category              VARCHAR(50) NULL,
    brand                 VARCHAR(100) NULL,
    unit                  VARCHAR(20) NULL,     -- tablet, bottle, vial, etc.
    minimum_stock_level   INT NOT NULL DEFAULT 0,
    status                VARCHAR(20) NOT NULL DEFAULT 'Active',
    created_at            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

DROP TABLE IF EXISTS medicine_stock_batches;
CREATE TABLE medicine_stock_batches (
    batch_id             INT AUTO_INCREMENT PRIMARY KEY,
    medicine_id          INT NOT NULL,
    batch_lot_no         VARCHAR(50) NULL,
    quantity_received    INT NOT NULL,
    quantity_remaining   INT NOT NULL,
    source                VARCHAR(100) NULL,   -- e.g. RHU supply, DOH, donation
    expiration_date       DATE NULL,
    date_received          DATETIME NOT NULL,
    received_by            INT NULL,
    remarks                 TEXT NULL,
    CONSTRAINT fk_batch_medicine FOREIGN KEY (medicine_id) REFERENCES medicines(medicine_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_batch_received_by FOREIGN KEY (received_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- G. PHARMACY / DISPENSING  (Data Dictionary Table 11)
-- stock_deduction_id now formally references the batch it was drawn from.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS pharmacy_dispensing;
CREATE TABLE pharmacy_dispensing (
    pharmacy_id          INT AUTO_INCREMENT PRIMARY KEY,
    prescription_id      INT NOT NULL,
    patient_id            INT NOT NULL,
    date_dispensed        DATETIME NOT NULL,
    medicine_name          VARCHAR(100) NOT NULL,
    quantity_dispensed      INT NOT NULL,
    dosage                   VARCHAR(100) NULL,
    instruction               TEXT NULL,
    dispensed_by              INT NOT NULL,
    stock_deduction_id        INT NULL,
    remarks                   TEXT NULL,
    CONSTRAINT fk_dispense_rx FOREIGN KEY (prescription_id) REFERENCES prescriptions_medication_orders(prescription_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_dispense_patient FOREIGN KEY (patient_id) REFERENCES patients(patient_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_dispense_by FOREIGN KEY (dispensed_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_dispense_batch FOREIGN KEY (stock_deduction_id) REFERENCES medicine_stock_batches(batch_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- H. LABORATORY  (Data Dictionary Table 12)
-- Extended with priority_level and clinical_notes (Figure 30 request form).
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS laboratory;
CREATE TABLE laboratory (
    laboratory_id     INT AUTO_INCREMENT PRIMARY KEY,
    encounter_id      INT NOT NULL,
    requested_by      INT NOT NULL,
    test_name         VARCHAR(100) NOT NULL,
    test_category     VARCHAR(50) NULL,
    specimen_type     VARCHAR(50) NULL,
    priority_level     VARCHAR(20) NULL,
    clinical_notes      TEXT NULL,
    request_date       DATETIME NOT NULL,
    result              TEXT NULL,
    result_date          DATETIME NULL,
    status                VARCHAR(20) NOT NULL DEFAULT 'For Approval',  -- For Approval / Pending / In Progress / Released / Referred / Rejected
    verified_by            INT NULL,
    -- RHU Management approval: every test checked in one LabRequestDialog submit shares a request_no
    request_no             VARCHAR(30) NULL,
    lab_destination        VARCHAR(20) NULL,   -- In-House / External
    external_facility      VARCHAR(150) NULL,
    approved_by            INT NULL,
    approved_at            DATETIME NULL,
    approval_remarks       TEXT NULL,
    -- where the released result was actually performed (LabResultDialog)
    result_source          VARCHAR(20) NULL,   -- RHU Laboratory / External Laboratory
    result_clinic          VARCHAR(150) NULL,  -- clinic / laboratory name
    result_doctor          VARCHAR(100) NULL,  -- doctor / pathologist who signed the result
    CONSTRAINT fk_lab_approved_by FOREIGN KEY (approved_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_lab_encounter FOREIGN KEY (encounter_id) REFERENCES encounters(encounter_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_lab_requested_by FOREIGN KEY (requested_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_lab_verified_by FOREIGN KEY (verified_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- I. REFERRALS  (Data Dictionary Table 13)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS referrals;
CREATE TABLE referrals (
    referral_id          INT AUTO_INCREMENT PRIMARY KEY,
    encounter_id         INT NOT NULL,
    referred_to_facility  VARCHAR(150) NOT NULL,
    reason_for_referral    TEXT NULL,
    referral_date            DATETIME NOT NULL,
    feedback_date              DATETIME NULL,
    feedback_notes               TEXT NULL,
    referred_by                   INT NOT NULL,
    received_by                    INT NULL,
    referral_type                    VARCHAR(50) NULL,
    CONSTRAINT fk_referral_encounter FOREIGN KEY (encounter_id) REFERENCES encounters(encounter_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_referral_by FOREIGN KEY (referred_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_referral_received_by FOREIGN KEY (received_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- J. OTHER DOCUMENTS / SERVICES  (Data Dictionary Table 14)
-- Covers medical certificates (Figure 20) and any other attached document.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS other_documents_services;
CREATE TABLE other_documents_services (
    document_id       INT AUTO_INCREMENT PRIMARY KEY,
    encounter_id       INT NOT NULL,
    document_type       VARCHAR(50) NOT NULL,     -- e.g. Medical Certificate, Lab Attachment
    document_name         VARCHAR(150) NOT NULL,
    file_path               VARCHAR(255) NULL,
    purpose                   VARCHAR(150) NULL,   -- Figure 20: purpose of issued certificate
    created_by                 INT NOT NULL,
    created_at                   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at                     DATETIME NULL,
    status                           VARCHAR(20) NULL,
    remarks                            TEXT NULL,
    reference_no                        VARCHAR(50) NULL,
    expiry_date                           DATE NULL,
    approved_by                           INT NULL,   -- RHU Management approval (status For Approval -> Approved / Rejected)
    approved_at                           DATETIME NULL,
    CONSTRAINT fk_doc_approved_by FOREIGN KEY (approved_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_doc_encounter FOREIGN KEY (encounter_id) REFERENCES encounters(encounter_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_doc_created_by FOREIGN KEY (created_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ----------------------------------------------------------------------------
-- Audit Logs -- extension table backing Report #9 / Figure 36. The
-- proposal names this report and shows its exact columns, but the data
-- dictionary appendix does not include a table for it.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS audit_logs;
CREATE TABLE audit_logs (
    audit_id          INT AUTO_INCREMENT PRIMARY KEY,
    user_id           INT NULL,
    username_snapshot VARCHAR(50) NULL,
    role_snapshot     VARCHAR(50) NULL,
    action            VARCHAR(20) NOT NULL,     -- CREATE / UPDATE / DELETE / VIEW / LOGIN / LOGIN_FAILED
    module            VARCHAR(100) NULL,
    target_record      VARCHAR(100) NULL,
    description           TEXT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'Success',
    created_at                DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_audit_user FOREIGN KEY (user_id) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB;

SET FOREIGN_KEY_CHECKS = 1;

-- ----------------------------------------------------------------------------
-- Indexes to support search/filter screens (Patient Directory search-by
-- name/ID/contact, encounter lookups, report date-range filters).
-- ----------------------------------------------------------------------------
CREATE INDEX idx_patients_name ON patients (last_name, first_name);
CREATE INDEX idx_patients_contact ON patients (contact_number);
CREATE INDEX idx_patients_barangay ON patients (barangay_id);
CREATE INDEX idx_patients_family ON patients (family_id);
CREATE INDEX idx_encounters_patient ON encounters (patient_id);
CREATE INDEX idx_encounters_date ON encounters (encounter_date);
CREATE INDEX idx_consultations_encounter ON consultations (encounter_id);
CREATE INDEX idx_diagnoses_consultation ON diagnoses (consultation_id);
CREATE INDEX idx_rx_encounter ON prescriptions_medication_orders (encounter_id);
CREATE INDEX idx_dispensing_patient ON pharmacy_dispensing (patient_id);
CREATE INDEX idx_lab_encounter ON laboratory (encounter_id);
CREATE INDEX idx_lab_request_no ON laboratory (request_no);
CREATE INDEX idx_referrals_encounter ON referrals (encounter_id);
CREATE INDEX idx_audit_created_at ON audit_logs (created_at);
CREATE INDEX idx_audit_user ON audit_logs (user_id);
