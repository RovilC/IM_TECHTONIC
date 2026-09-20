-- ============================================================================
-- Project C.A.R.E - Seed / Demo Data
-- Run AFTER schema.sql.
--
-- Creates the 6 roles from the proposal's User Levels table, one demo login
-- per role, and the barangays referenced in the proposal's own UI mockups
-- (Figures 5 and 32). Replace the barangay list with the RHU's official
-- barangay list before going live.
--
-- All demo accounts use the password:  Admin@123
-- (change these immediately in a real deployment)
-- ============================================================================

USE rhu_care;

-- ----------------------------------------------------------------------------
-- Roles  (Table 3. User Levels)
-- ----------------------------------------------------------------------------
INSERT INTO roles (role_name, description, module_access, can_add, can_edit, can_delete, can_view, status) VALUES
('System Administrator', 'Entire system, user accounts, roles, permissions, system settings, audit logs',
 'dashboard,patients,registration,family,encounters,triage,consultation,lab,pharmacy,inventory,certificates,reports,users,roles,audit', TRUE, TRUE, TRUE, TRUE, 'Active'),
('RHU Management', 'Overall dashboard, all sections'' reports, statistics, monitoring, approvals',
 'dashboard,reports,statistics,approvals', TRUE, FALSE, FALSE, TRUE, 'Active'),
('Records Staff', 'Registration, family/household records, patient directory (summary only -- no clinical/encounter access)',
 'dashboard,patients,registration,family', TRUE, TRUE, FALSE, TRUE, 'Active'),
('Healthcare Staff', 'Triage, consultation, diagnosis, procedures, laboratory requests/results',
 'dashboard,patients,encounters,triage,consultation,lab', TRUE, TRUE, FALSE, TRUE, 'Active'),
('Pharmacy & Inventory Staff', 'Medicine dispensing, batch/lot, inventory, medicine sources, expiration, stock monitoring',
 'dashboard,pharmacy,inventory,dispensing,stock', TRUE, TRUE, FALSE, TRUE, 'Active'),
('Laboratory Staff', 'Laboratory requests, test processing, findings/results, release of results, referrals',
 'dashboard,lab,referrals', TRUE, TRUE, FALSE, TRUE, 'Active');

-- ----------------------------------------------------------------------------
-- Demo users -- password for every account below is:  Admin@123
-- ----------------------------------------------------------------------------
INSERT INTO users_staff (full_name, username, password_hash, role_id, contact_number, email, status) VALUES
('System Administrator', 'admin',      'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 1, '0917-000-0001', 'admin@rhuvinzons.doh.gov.ph', 'Active'),
('RHU Management User', 'management', 'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 2, '0917-000-0002', 'management@rhuvinzons.doh.gov.ph', 'Active'),
('Records Staff User', 'records',     'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 3, '0917-000-0003', 'records@rhuvinzons.doh.gov.ph', 'Active'),
('Dr. Lorna Reyes', 'healthcare',      'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 4, '0917-000-0004', 'healthcare@rhuvinzons.doh.gov.ph', 'Active'),
('Pharmacy Staff User', 'pharmacy',   'PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 5, '0917-000-0005', 'pharmacy@rhuvinzons.doh.gov.ph', 'Active'),
('Laboratory Staff User', 'laboratory','PBKDF2$100000$AQIDBAUGBwgJCgsMDQ4PEA==$7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=', 6, '0917-000-0006', 'laboratory@rhuvinzons.doh.gov.ph', 'Active');

-- ----------------------------------------------------------------------------
-- Barangays (Vinzons, Camarines Norte) -- names referenced in the proposal's
-- own mockups (Figures 5 & 32). Replace/extend with the RHU's full official
-- barangay list before go-live.
-- ----------------------------------------------------------------------------
INSERT INTO barangays (barangay_name, municipality, province, region) VALUES
('Bagasbas', 'Vinzons', 'Camarines Norte', 'Region V'),
('Bayan', 'Vinzons', 'Camarines Norte', 'Region V'),
('Bulala', 'Vinzons', 'Camarines Norte', 'Region V'),
('Alayao', 'Vinzons', 'Camarines Norte', 'Region V'),
('Capalonga', 'Vinzons', 'Camarines Norte', 'Region V'),
('Mabilo', 'Vinzons', 'Camarines Norte', 'Region V'),
('Lugui', 'Vinzons', 'Camarines Norte', 'Region V'),
('Paridel', 'Vinzons', 'Camarines Norte', 'Region V'),
('Salvacion', 'Vinzons', 'Camarines Norte', 'Region V'),
('Mangcayo', 'Vinzons', 'Camarines Norte', 'Region V'),
('Santo Domingo', 'Vinzons', 'Camarines Norte', 'Region V');

-- ----------------------------------------------------------------------------
-- Starter medicines + one stock batch each, so the Pharmacy module and
-- Medicine Inventory Report have something to display immediately.
-- ----------------------------------------------------------------------------
INSERT INTO medicines (medicine_name, category, brand, unit, minimum_stock_level) VALUES
('Paracetamol 500mg', 'Analgesic/Antipyretic', 'Biogesic', 'tablet', 100),
('Amoxicillin 500mg', 'Antibiotic', 'Amoxil', 'capsule', 100),
('Losartan 50mg', 'Antihypertensive', 'Cozaar', 'tablet', 100),
('Metformin 500mg', 'Antidiabetic', 'Glucophage', 'tablet', 100),
('Amlodipine 5mg', 'Antihypertensive', 'Norvasc', 'tablet', 100);

INSERT INTO medicine_stock_batches (medicine_id, batch_lot_no, quantity_received, quantity_remaining, source, expiration_date, date_received, received_by, remarks)
SELECT medicine_id, 'DR-2026-001', 300, 300, 'RHU supply', DATE_ADD(CURDATE(), INTERVAL 18 MONTH), NOW(), (SELECT user_id FROM users_staff WHERE username='pharmacy'), 'Initial stock'
FROM medicines WHERE medicine_name = 'Paracetamol 500mg';

INSERT INTO medicine_stock_batches (medicine_id, batch_lot_no, quantity_received, quantity_remaining, source, expiration_date, date_received, received_by, remarks)
SELECT medicine_id, 'DR-2026-002', 150, 150, 'RHU supply', DATE_ADD(CURDATE(), INTERVAL 12 MONTH), NOW(), (SELECT user_id FROM users_staff WHERE username='pharmacy'), 'Initial stock'
FROM medicines WHERE medicine_name = 'Amoxicillin 500mg';

INSERT INTO medicine_stock_batches (medicine_id, batch_lot_no, quantity_received, quantity_remaining, source, expiration_date, date_received, received_by, remarks)
SELECT medicine_id, 'DR-2026-003', 200, 200, 'RHU supply', DATE_ADD(CURDATE(), INTERVAL 24 MONTH), NOW(), (SELECT user_id FROM users_staff WHERE username='pharmacy'), 'Initial stock'
FROM medicines WHERE medicine_name = 'Losartan 50mg';
