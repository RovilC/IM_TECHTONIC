-- ============================================================================
-- Laboratory results record where they were performed: RHU laboratory or an
-- external laboratory, with the clinic/laboratory name and the doctor /
-- pathologist. Laboratory Staff sidebar: Dashboard + Laboratory.
-- Users must log out and back in for the role change to take effect.
-- ============================================================================
ALTER TABLE laboratory
    ADD COLUMN result_source VARCHAR(20)  NULL,
    ADD COLUMN result_clinic VARCHAR(150) NULL,
    ADD COLUMN result_doctor VARCHAR(100) NULL;

UPDATE roles SET module_access = 'dashboard,lab' WHERE role_name = 'Laboratory Staff';
