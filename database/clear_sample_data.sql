-- ============================================================================
-- Removes all sample/demo data and leaves a clean, ready-to-use system:
--   KEPT:    roles, barangays, the medicine list (no stock), and the six main
--            login accounts (admin, management, records, healthcare,
--            pharmacy, laboratory -- password Admin@123, change after go-live)
--   DELETED: patients, households, encounters, consultations, diagnoses,
--            prescriptions, dispensing, laboratory, referrals, certificates,
--            medicine stock batches, audit logs, and every other user account
-- Auto-increment counters restart at 1 so real records get clean IDs.
-- BACK UP FIRST -- this cannot be undone.
-- ============================================================================
USE rhu_care;
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE audit_logs;
TRUNCATE TABLE referrals;
TRUNCATE TABLE other_documents_services;
TRUNCATE TABLE laboratory;
TRUNCATE TABLE pharmacy_dispensing;
TRUNCATE TABLE prescriptions_medication_orders;
TRUNCATE TABLE diagnoses;
TRUNCATE TABLE consultations;
TRUNCATE TABLE encounters;
TRUNCATE TABLE patients;
TRUNCATE TABLE families_households;
TRUNCATE TABLE medicine_stock_batches;

DELETE FROM users_staff
WHERE username NOT IN ('admin', 'management', 'records', 'healthcare', 'pharmacy', 'laboratory');
ALTER TABLE users_staff AUTO_INCREMENT = 1;   -- MySQL bumps this to MAX(user_id) + 1

SET FOREIGN_KEY_CHECKS = 1;
