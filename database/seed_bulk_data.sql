-- ============================================================================
-- Project C.A.R.E - Bulk Test/Demo Data
--
-- Run AFTER schema.sql and seed_data.sql. Adds ~10,000 rows to every
-- transactional/entity table (households, patients, encounters,
-- consultations, diagnoses, prescriptions, dispensing, lab requests,
-- referrals, certificates, stock batches, audit logs) so the app,
-- its reports, and its dashboard have real volume to show.
--
-- Lookup tables (roles, barangays, medicines) are intentionally NOT padded
-- to 10,000 rows -- the app puts them straight into dropdowns, and 10,000
-- "barangays" to scroll through would make the UI unusable. Medicines gets
-- a modest top-up for variety; roles/barangays are left as seed_data.sql
-- set them. Staff (users_staff) gets a modest top-up (~40 total) instead
-- of 10,000 fake logins.
--
-- Every bulk-generated business code uses a "-BULK-" infix (PT-BULK-000123,
-- ENC-BULK-000456, etc.) so it's visually obvious which records are demo
-- data versus what you enter yourself through the app. It also means
-- IdGenerator's per-year row counts (used to build the NEXT code when you
-- register a patient through the UI) will include whichever bulk rows
-- happen to land in the current year -- cosmetic only (codes stay unique
-- and valid), but the first code you generate afterward may not start
-- back at 0001.
--
-- IMPORTANT (why this looks different from a naive generator): every
-- per-row random value here is produced by a derived table that SELECTs
-- directly from a real multi-row source (seed_numbers, encounters,
-- consultations, ...). MySQL only guarantees RAND()/subqueries are
-- re-evaluated per row when they're computed against an actual multi-row
-- FROM source -- wrapping a bare "SELECT RAND()" (no FROM, or a
-- single-row FROM) in "CROSS JOIN (...) x" gets evaluated ONCE and reused
-- for every output row, which would have made whole columns identical
-- across all 10,000 rows. Every "coherent pair" below (e.g. a diagnosis
-- description and its matching chronic/acute classification, or a lab
-- test name and its matching category+specimen) is computed as columns
-- of the SAME derived-table row so they stay consistent with each other.
--
-- This is a large, mostly set-based script; it should complete in a few
-- minutes on typical hardware. Import the same way as schema.sql:
--   mysql -u root -p rhu_care < seed_bulk_data.sql
-- or via phpMyAdmin -> Import (both handle the DELIMITER block below;
-- a tool that blindly splits on ";" will not).
-- ============================================================================

USE rhu_care;

-- ----------------------------------------------------------------------------
-- 0a. Top up the medicine catalog for variety (INSERT IGNORE: safe to
-- re-run, skips any name that already exists from seed_data.sql).
-- ----------------------------------------------------------------------------
INSERT IGNORE INTO medicines (medicine_name, category, brand, unit, minimum_stock_level) VALUES
('Ibuprofen 400mg', 'Analgesic/Anti-inflammatory', 'Medicol', 'tablet', 100),
('Cefalexin 500mg', 'Antibiotic', 'Keflex', 'capsule', 100),
('Ascorbic Acid 500mg', 'Vitamin', 'Cecon', 'tablet', 150),
('Ferrous Sulfate + Folic Acid', 'Hematinic', 'FerroPlus', 'tablet', 100),
('Multivitamins', 'Vitamin', 'Enervon', 'tablet', 150),
('Cotrimoxazole 800/160mg', 'Antibiotic', 'Bactrim', 'tablet', 100),
('Salbutamol Inhaler', 'Bronchodilator', 'Ventolin', 'inhaler', 30),
('Mefenamic Acid 500mg', 'Analgesic', 'Ponstan', 'tablet', 100),
('Omeprazole 20mg', 'Antacid', 'Losec', 'capsule', 100),
('Metronidazole 500mg', 'Antibiotic', 'Flagyl', 'tablet', 100),
('Loperamide 2mg', 'Antidiarrheal', 'Imodium', 'tablet', 100),
('Oral Rehydration Salts', 'Electrolyte', 'Hydrite', 'sachet', 100),
('Paracetamol Syrup 250mg/5mL', 'Analgesic/Antipyretic', 'Tempra Syrup', 'bottle', 60),
('Amoxicillin Syrup 250mg/5mL', 'Antibiotic', 'Amoxil Syrup', 'bottle', 60),
('Simvastatin 20mg', 'Antihyperlipidemic', 'Zocor', 'tablet', 100),
('Enalapril 5mg', 'Antihypertensive', 'Renitec', 'tablet', 100),
('Glibenclamide 5mg', 'Antidiabetic', 'Euglucon', 'tablet', 100),
('Hydrochlorothiazide 25mg', 'Antihypertensive', 'Aquazide', 'tablet', 100),
('Isoniazid 300mg', 'Anti-TB', 'Isonarif', 'tablet', 100),
('Rifampicin 300mg', 'Anti-TB', 'Rifacin', 'capsule', 100),
('Ethambutol 400mg', 'Anti-TB', 'Myambutol', 'tablet', 100),
('Pyrazinamide 500mg', 'Anti-TB', 'Pyrafat', 'tablet', 100),
('Zinc Sulfate 20mg', 'Mineral Supplement', 'Zinkid', 'tablet', 100),
('Vitamin A 200,000 IU', 'Vitamin', 'Aquasol A', 'capsule', 100),
('Erythromycin 500mg', 'Antibiotic', 'Ilosone', 'tablet', 60),
('Diphenhydramine 50mg', 'Antihistamine', 'Benadryl', 'tablet', 100),
('Betamethasone Cream', 'Topical Steroid', 'Diprosone', 'tube', 40),
('Cotrimoxazole Syrup', 'Antibiotic', 'Bactrim Syrup', 'bottle', 40),
('Tranexamic Acid 500mg', 'Antihemorrhagic', 'Hemostan', 'tablet', 60),
('Cefuroxime 500mg', 'Antibiotic', 'Zinnat', 'tablet', 60);

-- ----------------------------------------------------------------------------
-- 0b. Top up staff to ~40 accounts spread across the 6 roles (all use the
-- same demo password: Admin@123). Inserted one role-batch at a time so each
-- role's ids stay contiguous for later reference.
-- ----------------------------------------------------------------------------
INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Ramon Villareal', 'admin02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 1, '0917-100-0002', 'admin02@rhuvinzons.doh.gov.ph', 'Active');

INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Corazon Mendoza', 'management02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 2, '0917-100-0003', 'management02@rhuvinzons.doh.gov.ph', 'Active'),
('Eduardo Bautista', 'management03', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 2, '0917-100-0004', 'management03@rhuvinzons.doh.gov.ph', 'Active');

INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Jose Cereno', 'records02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0005', 'records02@rhuvinzons.doh.gov.ph', 'Active'),
('Ana Marie Aquino', 'records03', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0006', 'records03@rhuvinzons.doh.gov.ph', 'Active'),
('Rosario Villanueva', 'records04', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0007', 'records04@rhuvinzons.doh.gov.ph', 'Active'),
('Willard Andrade', 'records05', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0008', 'records05@rhuvinzons.doh.gov.ph', 'Active'),
('Corazon Salvacion', 'records06', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0009', 'records06@rhuvinzons.doh.gov.ph', 'Active'),
('Josefina Pascua', 'records07', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0010', 'records07@rhuvinzons.doh.gov.ph', 'Active'),
('Marites Bulala', 'records08', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-100-0011', 'records08@rhuvinzons.doh.gov.ph', 'Active');

INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Dr. Jose Miguel Santos', 'healthcare02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0012', 'healthcare02@rhuvinzons.doh.gov.ph', 'Active'),
('Dr. Ana Reyes', 'healthcare03', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0013', 'healthcare03@rhuvinzons.doh.gov.ph', 'Active'),
('Nurse Maria Santos', 'healthcare04', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0014', 'healthcare04@rhuvinzons.doh.gov.ph', 'Active'),
('Nurse Rovil Cereno', 'healthcare05', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0015', 'healthcare05@rhuvinzons.doh.gov.ph', 'Active'),
('Dr. Ricardo Dela Cruz', 'healthcare06', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0016', 'healthcare06@rhuvinzons.doh.gov.ph', 'Active'),
('Nurse Niecel Nocillado', 'healthcare07', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0017', 'healthcare07@rhuvinzons.doh.gov.ph', 'Active'),
('Dr. Fernando Aquino', 'healthcare08', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0018', 'healthcare08@rhuvinzons.doh.gov.ph', 'Active'),
('Nurse Teresa Bautista', 'healthcare09', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0019', 'healthcare09@rhuvinzons.doh.gov.ph', 'Active'),
('Dr. Patricia Villareal', 'healthcare10', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0020', 'healthcare10@rhuvinzons.doh.gov.ph', 'Active'),
('Nurse Angela Mendoza', 'healthcare11', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-100-0021', 'healthcare11@rhuvinzons.doh.gov.ph', 'Active');

INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Roberto Cruz', 'pharmacy02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0022', 'pharmacy02@rhuvinzons.doh.gov.ph', 'Active'),
('Isabel Garcia', 'pharmacy03', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0023', 'pharmacy03@rhuvinzons.doh.gov.ph', 'Active'),
('Alejandro Lopez', 'pharmacy04', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0024', 'pharmacy04@rhuvinzons.doh.gov.ph', 'Active'),
('Gloria Reyes', 'pharmacy05', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0025', 'pharmacy05@rhuvinzons.doh.gov.ph', 'Active'),
('Ernesto Villanueva', 'pharmacy06', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0026', 'pharmacy06@rhuvinzons.doh.gov.ph', 'Active'),
('Remedios Santos', 'pharmacy07', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-100-0027', 'pharmacy07@rhuvinzons.doh.gov.ph', 'Active');

INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('Manuel Aquino', 'laboratory02', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0028', 'laboratory02@rhuvinzons.doh.gov.ph', 'Active'),
('Cristina Mendoza', 'laboratory03', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0029', 'laboratory03@rhuvinzons.doh.gov.ph', 'Active'),
('Rafael Villareal', 'laboratory04', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0030', 'laboratory04@rhuvinzons.doh.gov.ph', 'Active'),
('Angela Cruz', 'laboratory05', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0031', 'laboratory05@rhuvinzons.doh.gov.ph', 'Active'),
('Carlos Dela Cruz', 'laboratory06', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0032', 'laboratory06@rhuvinzons.doh.gov.ph', 'Active'),
('Lourdes Bautista', 'laboratory07', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-100-0033', 'laboratory07@rhuvinzons.doh.gov.ph', 'Active');

-- ----------------------------------------------------------------------------
-- 1. Build a 1..10000 numbers helper table (via a stored procedure loop --
-- portable across both MySQL and MariaDB, unlike recursive-CTE recursion
-- depth limits which differ between them). Dropped again at the very end.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS seed_numbers;
CREATE TABLE seed_numbers (n INT PRIMARY KEY) ENGINE=InnoDB;

DROP PROCEDURE IF EXISTS seed_fill_numbers;
DELIMITER $$
CREATE PROCEDURE seed_fill_numbers()
BEGIN
    DECLARE i INT DEFAULT 1;
    WHILE i <= 10000 DO
        INSERT INTO seed_numbers (n) VALUES (i);
        SET i = i + 1;
    END WHILE;
END$$
DELIMITER ;
CALL seed_fill_numbers();
DROP PROCEDURE seed_fill_numbers;

-- ----------------------------------------------------------------------------
-- 2. Medicine Stock Batches (10,000) -- independent restock history. Deliberately
-- generated BEFORE Pharmacy Dispensing (section 9) below, so that section has a
-- real, varied pool of batches per medicine to draw from and actually decrement --
-- otherwise "dispensing" would have nothing real to deduct from and the Inventory
-- In/Out report's numbers wouldn't reconcile. qty is computed once per row (from
-- seed_numbers) and reused for both quantity_received and quantity_remaining so
-- remaining never exceeds received.
-- ----------------------------------------------------------------------------
INSERT INTO medicine_stock_batches (medicine_id, batch_lot_no, quantity_received, quantity_remaining, source, expiration_date, date_received, received_by)
SELECT
    (SELECT medicine_id FROM medicines ORDER BY RAND() LIMIT 1),
    CONCAT('DR-BULK-', LPAD(x.n, 6, '0')),
    x.qty,
    FLOOR(x.qty * RAND()),
    ELT(1 + FLOOR(RAND() * 3), 'RHU supply', 'DOH', 'Donation'),
    DATE_ADD(CURDATE(), INTERVAL (FLOOR(RAND() * 36) - 6) MONTH),
    DATE_SUB(NOW(), INTERVAL FLOOR(RAND() * 1095 * 1440) MINUTE),
    (SELECT user_id FROM users_staff WHERE role_id = 5 ORDER BY RAND() LIMIT 1)
FROM (SELECT n, 50 + FLOOR(RAND() * 450) AS qty FROM seed_numbers) x;

-- ----------------------------------------------------------------------------
-- 3. Families / Households (10,000)
-- ----------------------------------------------------------------------------
SELECT COALESCE(MAX(family_id), 0) INTO @beforeF FROM families_households;

INSERT INTO families_households (household_no, family_head_id, address, barangay_id, household_status, date_registered, remarks)
SELECT
    CONCAT('HH-BULK-', LPAD(sn.n, 6, '0')),
    NULL,
    CONCAT('Purok ', 1 + FLOOR(RAND() * 7), ', Sitio ', ELT(1 + FLOOR(RAND() * 5), 'Maligaya', 'Masagana', 'Malinis', 'Masipag', 'Matiwasay')),
    (SELECT barangay_id FROM barangays ORDER BY RAND() LIMIT 1),
    ELT(1 + FLOOR(RAND() * 3), 'Active', 'Regularly Monitored', 'Inactive'),
    DATE_SUB(CURDATE(), INTERVAL FLOOR(RAND() * 1095) DAY),
    NULL
FROM seed_numbers sn;

SET @minF = @beforeF + 1;
SELECT MAX(family_id) INTO @maxF FROM families_households;
SET @countF = @maxF - @minF + 1;

-- ----------------------------------------------------------------------------
-- 4. Patients (10,000) -- ~70% linked to one of the new households above.
-- (x derives is_male from seed_numbers -- a real 10,000-row source -- so
-- RAND() is genuinely re-evaluated per row, and both the first-name pick
-- and the sex column below agree with each other.)
-- ----------------------------------------------------------------------------
SELECT COALESCE(MAX(patient_id), 0) INTO @beforeP FROM patients;

INSERT INTO patients (
    patient_no, first_name, middle_name, last_name, date_of_birth, sex, civil_status,
    address, barangay_id, family_id, philhealth_no, philhealth_category, philhealth_status,
    contact_number, blood_type, is_4ps_member, is_indigent, known_allergies, existing_conditions,
    status, date_registered
)
SELECT
    CONCAT('PT-BULK-', LPAD(x.n, 6, '0')),
    IF(x.is_male = 1,
        ELT(1 + FLOOR(RAND() * 15), 'Juan', 'Jose', 'Antonio', 'Pedro', 'Ramon', 'Ricardo', 'Eduardo', 'Roberto', 'Carlos', 'Miguel', 'Rafael', 'Alejandro', 'Fernando', 'Ernesto', 'Manuel'),
        ELT(1 + FLOOR(RAND() * 15), 'Maria', 'Ana', 'Rosa', 'Carmen', 'Elena', 'Teresa', 'Josefa', 'Cristina', 'Patricia', 'Angela', 'Isabel', 'Lourdes', 'Gloria', 'Corazon', 'Remedios')),
    NULL,
    ELT(1 + FLOOR(RAND() * 20), 'Dela Cruz', 'Santos', 'Reyes', 'Bautista', 'Garcia', 'Villanueva', 'Mendoza', 'Aquino', 'Villareal', 'Cruz', 'Lopez', 'Gonzales', 'Ramos', 'Flores', 'Torres', 'Andrade', 'Salvacion', 'Pascua', 'Bulala', 'Cereno'),
    DATE_SUB(CURDATE(), INTERVAL FLOOR(RAND() * 90 * 365.25) DAY),
    IF(x.is_male = 1, 'Male', 'Female'),
    ELT(1 + FLOOR(RAND() * 4), 'Single', 'Married', 'Widowed', 'Separated'),
    CONCAT('Purok ', 1 + FLOOR(RAND() * 7)),
    (SELECT barangay_id FROM barangays ORDER BY RAND() LIMIT 1),
    IF(RAND() < 0.7, @minF + FLOOR(RAND() * @countF), NULL),
    IF(RAND() < 0.6, CONCAT(FLOOR(RAND() * 90000000) + 10000000, '-', FLOOR(RAND() * 9)), NULL),
    ELT(1 + FLOOR(RAND() * 5), 'Formal Economy', 'Informal Economy', 'Indigent', 'Senior Citizen', 'Sponsored'),
    ELT(1 + FLOOR(RAND() * 3), 'Member', 'Dependent', 'None'),
    CONCAT('09', FLOOR(RAND() * 9) + 1, LPAD(FLOOR(RAND() * 100000000), 8, '0')),
    ELT(1 + FLOOR(RAND() * 9), 'Unknown', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'),
    IF(RAND() < 0.15, TRUE, FALSE),
    IF(RAND() < 0.1, TRUE, FALSE),
    IF(RAND() < 0.12, ELT(1 + FLOOR(RAND() * 4), 'Penicillin', 'Sulfa drugs', 'Seafood', 'Dust'), NULL),
    IF(RAND() < 0.25, ELT(1 + FLOOR(RAND() * 5), 'Hypertension', 'Type 2 Diabetes Mellitus', 'Asthma', 'Obesity', 'None'), NULL),
    IF(RAND() < 0.97, 'Active', 'Inactive'),
    DATE_SUB(NOW(), INTERVAL FLOOR(RAND() * 1095 * 1440) MINUTE)
FROM (SELECT n, IF(RAND() < 0.5, 1, 0) AS is_male FROM seed_numbers) x;

SET @minP = @beforeP + 1;
SELECT MAX(patient_id) INTO @maxP FROM patients;
SET @countP = @maxP - @minP + 1;

-- Give each new household a head (the lowest-id member it actually has, if any).
UPDATE families_households fh
SET family_head_id = (
    SELECT p.patient_id FROM patients p
    WHERE p.family_id = fh.family_id
    ORDER BY p.patient_id LIMIT 1
)
WHERE fh.family_id BETWEEN @minF AND @maxF;

UPDATE patients p
JOIN families_households fh ON fh.family_head_id = p.patient_id
SET p.family_head_or_no = TRUE
WHERE fh.family_id BETWEEN @minF AND @maxF;

-- ----------------------------------------------------------------------------
-- 5. Encounters (10,000) -- random bulk patient; edate computed once per
-- row (from seed_numbers) and reused for encounter_date/triage_at/created_at
-- so those three timestamps agree with each other.
-- ----------------------------------------------------------------------------
SELECT COALESCE(MAX(encounter_id), 0) INTO @beforeE FROM encounters;

INSERT INTO encounters (
    patient_id, encounter_no, encounter_date, visit_type, referral_source, priority_level,
    chief_complaint, triage_temp_c, triage_bp_systolic, triage_bp_diastolic, triage_heart_rate,
    triage_respiratory_rate, triage_spo2, triage_weight_kg, triage_height_cm,
    triage_pain_scale, triage_classification, triage_by, triage_at, status, created_by, created_at
)
SELECT
    @minP + FLOOR(RAND() * @countP),
    CONCAT('ENC-BULK-', LPAD(x.n, 6, '0')),
    x.edate,
    ELT(1 + FLOOR(RAND() * 5), 'Outpatient Consultation', 'Follow-up', 'Immunization', 'Prenatal/Postpartum', 'Emergency'),
    ELT(1 + FLOOR(RAND() * 3), 'Walk-in', 'Referred', 'Scheduled'),
    ELT(1 + FLOOR(RAND() * 3), 'Routine', 'Urgent', 'Emergency'),
    ELT(1 + FLOOR(RAND() * 10), 'Fever and colds', 'Persistent cough', 'Headache and dizziness', 'Hypertension follow-up', 'Diabetes follow-up', 'Abdominal pain', 'Skin rash and itchiness', 'Routine prenatal checkup', 'Well-baby immunization', 'General weakness'),
    ROUND(36.2 + RAND() * 2.3, 1),
    100 + FLOOR(RAND() * 60),
    60 + FLOOR(RAND() * 40),
    60 + FLOOR(RAND() * 50),
    16 + FLOOR(RAND() * 10),
    ROUND(94 + RAND() * 6, 1),
    ROUND(35 + RAND() * 60, 1),
    ROUND(130 + RAND() * 60, 1),
    FLOOR(RAND() * 11),
    ELT(1 + FLOOR(RAND() * 3), 'Non-urgent', 'Priority', 'Emergency'),
    (SELECT user_id FROM users_staff WHERE role_id = 4 ORDER BY RAND() LIMIT 1),
    x.edate,
    IF(RAND() < 0.9, 'Closed', ELT(1 + FLOOR(RAND() * 2), 'Open', 'In Progress')),
    (SELECT user_id FROM users_staff WHERE role_id IN (3, 4) ORDER BY RAND() LIMIT 1),
    x.edate
FROM (SELECT n, DATE_SUB(NOW(), INTERVAL FLOOR(RAND() * 1095 * 1440) MINUTE) AS edate FROM seed_numbers) x;

SET @minE = @beforeE + 1;
SELECT MAX(encounter_id) INTO @maxE FROM encounters;

-- Fill in BMI now that weight/height are known (kept as a separate pass to keep the insert above readable).
UPDATE encounters
SET triage_bmi = ROUND(triage_weight_kg / POWER(triage_height_cm / 100, 2), 2)
WHERE encounter_id BETWEEN @minE AND @maxE AND triage_weight_kg IS NOT NULL AND triage_height_cm IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 6. Consultations (10,000) -- one per bulk encounter, a bit after it started.
-- ----------------------------------------------------------------------------
SELECT COALESCE(MAX(consultation_id), 0) INTO @beforeC FROM consultations;

INSERT INTO consultations (
    encounter_id, doctor_id, consultation_date, chief_complaint, history_of_present_illness,
    past_medical_history, current_medications, pe_general, treatment_plan, follow_up_date, status
)
SELECT
    e.encounter_id,
    (SELECT user_id FROM users_staff WHERE role_id = 4 ORDER BY RAND() LIMIT 1),
    DATE_ADD(e.encounter_date, INTERVAL 15 + FLOOR(RAND() * 60) MINUTE),
    e.chief_complaint,
    CONCAT('Patient reports symptoms for the past ', 1 + FLOOR(RAND() * 7), ' day(s).'),
    IF(RAND() < 0.4, ELT(1 + FLOOR(RAND() * 3), 'Hypertension', 'Type 2 Diabetes Mellitus', 'No previous surgeries'), 'No significant past medical history'),
    IF(RAND() < 0.3, ELT(1 + FLOOR(RAND() * 3), 'Amlodipine 5mg OD', 'Metformin 500mg BID', 'Losartan 50mg OD'), NULL),
    'Patient is awake, alert, and in no acute distress.',
    ELT(1 + FLOOR(RAND() * 6), 'Continue current medications, return for follow-up in 2 weeks.', 'Prescribed medications as noted, advised rest and hydration.', 'Lifestyle modification counseling given, low salt/low sugar diet advised.', 'Referred for laboratory work-up.', 'Symptomatic treatment given, advised to return if symptoms worsen.', 'Health teaching provided, advised follow-up check-up.'),
    IF(RAND() < 0.6, DATE_ADD(e.encounter_date, INTERVAL 7 + FLOOR(RAND() * 60) DAY), NULL),
    'Completed'
FROM encounters e
WHERE e.encounter_id BETWEEN @minE AND @maxE;

SET @minC = @beforeC + 1;
SELECT MAX(consultation_id) INTO @maxC FROM consultations;

-- ----------------------------------------------------------------------------
-- 7. Diagnoses (10,000) -- one per bulk consultation. d_idx picks a single
-- diagnosis/classification pair from the SAME row of the derived table so
-- e.g. "Pulmonary Tuberculosis" always lands with "Chronic", never mismatched.
-- ----------------------------------------------------------------------------
INSERT INTO diagnoses (consultation_id, icd_code, diagnosis_desc, classification, status, created_at)
SELECT
    x.consultation_id,
    NULL,
    ELT(x.d_idx, 'Essential (Primary) Hypertension', 'Type 2 Diabetes Mellitus without Complications', 'Acute Upper Respiratory Tract Infection', 'Acute Gastroenteritis', 'Urinary Tract Infection', 'Contact Dermatitis', 'Bronchial Asthma in Acute Exacerbation', 'Dyslipidemia', 'Community-Acquired Pneumonia', 'Allergic Rhinitis', 'Migraine', 'Low Back Pain', 'Osteoarthritis', 'Iron Deficiency Anemia', 'Peptic Ulcer Disease', 'Acute Otitis Media', 'Acute Conjunctivitis', 'Pulmonary Tuberculosis', 'HIV Infection - Asymptomatic', 'Scabies'),
    ELT(x.d_idx, 'Chronic', 'Chronic', 'Acute', 'Acute', 'Acute', 'Acute', 'Chronic', 'Chronic', 'Acute', 'Chronic', 'Acute', 'Chronic', 'Chronic', 'Chronic', 'Chronic', 'Acute', 'Acute', 'Chronic', 'Chronic', 'Acute'),
    IF(RAND() < 0.85, 'Active', 'Resolved'),
    x.consultation_date
FROM (
    SELECT consultation_id, consultation_date, 1 + FLOOR(RAND() * 20) AS d_idx
    FROM consultations
    WHERE consultation_id BETWEEN @minC AND @maxC
) x;

-- ----------------------------------------------------------------------------
-- 8. Prescriptions / Medication Orders (10,000) -- one per bulk encounter.
-- ----------------------------------------------------------------------------
SELECT COALESCE(MAX(prescription_id), 0) INTO @beforeRx FROM prescriptions_medication_orders;

INSERT INTO prescriptions_medication_orders (
    prescription_number, encounter_id, doctor_id, medication_date, medicine_name, dosage, frequency, quantity, instructions, status
)
SELECT
    CONCAT('RX-BULK-', LPAD(x.rn, 6, '0')),
    x.encounter_id,
    (SELECT user_id FROM users_staff WHERE role_id = 4 ORDER BY RAND() LIMIT 1),
    x.encounter_date,
    (SELECT medicine_name FROM medicines ORDER BY RAND() LIMIT 1),
    ELT(1 + FLOOR(RAND() * 4), '1 tablet', '500mg', '2 tablets', '5mL'),
    ELT(1 + FLOOR(RAND() * 4), 'Once daily', 'Twice daily', 'Three times daily', 'As needed'),
    5 + FLOOR(RAND() * 56),
    ELT(1 + FLOOR(RAND() * 4), 'Take after meals', 'Take before meals', 'Take with plenty of water', 'Take as prescribed'),
    'Pending'
FROM (
    SELECT encounter_id, encounter_date, encounter_id - @minE + 1 AS rn
    FROM encounters
    WHERE encounter_id BETWEEN @minE AND @maxE
) x;

SET @minRx = @beforeRx + 1;
SELECT MAX(prescription_id) INTO @maxRx FROM prescriptions_medication_orders;

-- ----------------------------------------------------------------------------
-- 9. Pharmacy Dispensing (10,000) -- fully dispense every bulk prescription
-- (patient_id is pulled from the prescription's own encounter, so a
-- dispensing record always matches the patient who was actually prescribed
-- the medicine -- not an unrelated random patient). Each row is linked to a
-- real stock batch of that same medicine (section 2 ran first specifically
-- so this pool exists) via stock_deduction_id, and the batch is actually
-- decremented afterward -- otherwise the Inventory In/Out report's
-- Stock In/Stock Out/On Hand numbers wouldn't reconcile against each other.
-- ----------------------------------------------------------------------------
INSERT INTO pharmacy_dispensing (prescription_id, patient_id, date_dispensed, medicine_name, quantity_dispensed, dosage, instruction, dispensed_by, stock_deduction_id)
SELECT
    rx.prescription_id,
    e.patient_id,
    DATE_ADD(rx.medication_date, INTERVAL 10 + FLOOR(RAND() * 120) MINUTE),
    rx.medicine_name,
    rx.quantity,
    rx.dosage,
    rx.instructions,
    (SELECT user_id FROM users_staff WHERE role_id = 5 ORDER BY RAND() LIMIT 1),
    (SELECT b.batch_id FROM medicine_stock_batches b
     JOIN medicines m ON m.medicine_id = b.medicine_id
     WHERE m.medicine_name = rx.medicine_name
     ORDER BY RAND() LIMIT 1)
FROM prescriptions_medication_orders rx
JOIN encounters e ON e.encounter_id = rx.encounter_id
WHERE rx.prescription_id BETWEEN @minRx AND @maxRx;

UPDATE prescriptions_medication_orders
SET status = 'Dispensed'
WHERE prescription_id BETWEEN @minRx AND @maxRx;

-- Decrement each affected batch by the TOTAL dispensed against it (several
-- of the 10,000 dispensing rows above can land on the same batch), clamped
-- at 0 so demo data never shows negative stock.
UPDATE medicine_stock_batches b
JOIN (
    SELECT stock_deduction_id, SUM(quantity_dispensed) AS total_dispensed
    FROM pharmacy_dispensing
    WHERE prescription_id BETWEEN @minRx AND @maxRx AND stock_deduction_id IS NOT NULL
    GROUP BY stock_deduction_id
) agg ON agg.stock_deduction_id = b.batch_id
SET b.quantity_remaining = GREATEST(b.quantity_remaining - agg.total_dispensed, 0);

-- ----------------------------------------------------------------------------
-- 10. Laboratory (10,000) -- one request per bulk encounter. t_idx keeps
-- test name/category/specimen consistent with each other; is_released
-- keeps result/result_date/status/verified_by consistent with each other.
-- ----------------------------------------------------------------------------
INSERT INTO laboratory (encounter_id, requested_by, test_name, test_category, specimen_type, priority_level, request_date, result, result_date, status, verified_by)
SELECT
    x.encounter_id,
    (SELECT user_id FROM users_staff WHERE role_id = 4 ORDER BY RAND() LIMIT 1),
    ELT(x.t_idx, 'Complete Blood Count (CBC)', 'Fasting Blood Sugar (FBS)', 'Urinalysis', 'HbA1c (Glycated Hemoglobin)', 'Total Cholesterol', 'Sputum Microscopy / TB LAMP', 'Chest X-ray', 'Pregnancy Test'),
    ELT(x.t_idx, 'Hematology', 'Clinical Chemistry', 'Urinalysis', 'Clinical Chemistry', 'Clinical Chemistry', 'Microbiology', 'Other', 'Other'),
    ELT(x.t_idx, 'Blood (Venous)', 'Blood (Venous)', 'Urine', 'Blood (Venous)', 'Blood (Venous)', 'Sputum', 'N/A', 'Urine'),
    ELT(1 + FLOOR(RAND() * 3), 'Routine', 'Urgent', 'STAT'),
    x.encounter_date,
    IF(x.is_released = 1, ELT(1 + FLOOR(RAND() * 4), 'Within normal limits', 'Slightly elevated', 'Positive', 'Negative'), NULL),
    IF(x.is_released = 1, DATE_ADD(x.encounter_date, INTERVAL 1 + FLOOR(RAND() * 3) DAY), NULL),
    IF(x.is_released = 1, 'Released', ELT(1 + FLOOR(RAND() * 2), 'Pending', 'In Progress')),
    IF(x.is_released = 1, (SELECT user_id FROM users_staff WHERE role_id = 6 ORDER BY RAND() LIMIT 1), NULL)
FROM (
    SELECT encounter_id, encounter_date, 1 + FLOOR(RAND() * 8) AS t_idx, IF(RAND() < 0.8, 1, 0) AS is_released
    FROM encounters
    WHERE encounter_id BETWEEN @minE AND @maxE
) x;

-- ----------------------------------------------------------------------------
-- 11. Referrals (10,000) -- one per bulk encounter.
-- ----------------------------------------------------------------------------
INSERT INTO referrals (encounter_id, referred_to_facility, reason_for_referral, referral_date, referred_by, referral_type)
SELECT
    e.encounter_id,
    ELT(1 + FLOOR(RAND() * 5), 'Camarines Norte Provincial Hospital', 'Bicol Medical Center', 'Mother Seton Hospital', 'Vinzons District Hospital', 'Daet Provincial Hospital'),
    ELT(1 + FLOOR(RAND() * 5), 'Uncontrolled hypertension, needs specialist evaluation', 'Suspected TB, needs confirmatory testing', 'Further laboratory work-up not available at RHU', 'High-risk pregnancy, needs OB-GYN specialist', 'Persistent symptoms despite treatment'),
    e.encounter_date,
    (SELECT user_id FROM users_staff WHERE role_id = 4 ORDER BY RAND() LIMIT 1),
    ELT(1 + FLOOR(RAND() * 2), 'Specialist Referral', 'Facility Referral')
FROM encounters e
WHERE e.encounter_id BETWEEN @minE AND @maxE;

-- ----------------------------------------------------------------------------
-- 12. Medical Certificates (10,000) -- one per bulk encounter.
-- ----------------------------------------------------------------------------
INSERT INTO other_documents_services (encounter_id, document_type, document_name, purpose, created_by, created_at, status, reference_no)
SELECT
    e.encounter_id,
    'Medical Certificate',
    'Medical Certificate',
    ELT(1 + FLOOR(RAND() * 5), 'Employment', 'School Requirement', 'Travel', 'Fit-to-Work', 'Insurance Claim'),
    (SELECT user_id FROM users_staff WHERE role_id IN (3, 4) ORDER BY RAND() LIMIT 1),
    e.encounter_date,
    'Issued',
    CONCAT('MC-BULK-', LPAD(e.encounter_id, 6, '0'))
FROM encounters e
WHERE e.encounter_id BETWEEN @minE AND @maxE;

-- ----------------------------------------------------------------------------
-- 13. Audit Logs (10,000) -- synthetic historical activity. user_id is
-- picked once per row (from seed_numbers) and reused to look up that SAME
-- staff member's username/role, so the snapshot columns stay consistent
-- with each other; a_idx keeps action/module/description consistent.
-- ----------------------------------------------------------------------------
INSERT INTO audit_logs (user_id, username_snapshot, role_snapshot, action, module, target_record, description, status, created_at)
SELECT
    x.user_id,
    (SELECT username FROM users_staff WHERE user_id = x.user_id),
    (SELECT r.role_name FROM users_staff u JOIN roles r ON r.role_id = u.role_id WHERE u.user_id = x.user_id),
    ELT(x.a_idx, 'CREATE', 'UPDATE', 'VIEW', 'LOGIN', 'CREATE', 'UPDATE'),
    ELT(x.a_idx, 'patients', 'encounters', 'consultation', 'auth', 'lab', 'pharmacy'),
    CONCAT(ELT(x.a_idx, 'patients', 'encounters', 'consultation', 'auth', 'lab', 'pharmacy'), ' #', x.n),
    ELT(x.a_idx, 'Registered new patient', 'Updated encounter record', 'Viewed patient chart', 'User signed in', 'Requested laboratory test', 'Dispensed medication'),
    IF(RAND() < 0.97, 'Success', 'Failed'),
    x.logdate
FROM (
    SELECT
        n,
        (SELECT user_id FROM users_staff ORDER BY RAND() LIMIT 1) AS user_id,
        1 + FLOOR(RAND() * 6) AS a_idx,
        DATE_SUB(NOW(), INTERVAL FLOOR(RAND() * 1095 * 1440) MINUTE) AS logdate
    FROM seed_numbers
) x;

-- ----------------------------------------------------------------------------
-- Cleanup
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS seed_numbers;

SELECT 'Bulk seed complete.' AS Status,
       (SELECT COUNT(*) FROM patients) AS Patients,
       (SELECT COUNT(*) FROM families_households) AS Households,
       (SELECT COUNT(*) FROM encounters) AS Encounters,
       (SELECT COUNT(*) FROM consultations) AS Consultations,
       (SELECT COUNT(*) FROM diagnoses) AS Diagnoses,
       (SELECT COUNT(*) FROM prescriptions_medication_orders) AS Prescriptions,
       (SELECT COUNT(*) FROM pharmacy_dispensing) AS Dispensing,
       (SELECT COUNT(*) FROM laboratory) AS LabRequests,
       (SELECT COUNT(*) FROM referrals) AS Referrals,
       (SELECT COUNT(*) FROM other_documents_services) AS Certificates,
       (SELECT COUNT(*) FROM medicine_stock_batches) AS StockBatches,
       (SELECT COUNT(*) FROM audit_logs) AS AuditLogs,
       (SELECT COUNT(*) FROM users_staff) AS StaffAccounts,
       (SELECT COUNT(*) FROM medicines) AS Medicines;
