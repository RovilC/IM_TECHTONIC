-- ============================================================================
-- Records Staff: sidebar limited to Dashboard + Patient Directory ("registration"
-- only grants the directory's Register Patient button, no sidebar entry). Patients
-- are registered from the directory's "+ Register Patient" button (which now
-- includes household membership), and each patient's profile shows a
-- read-only, printable record summary.
-- Users must log out and back in for the change to take effect.
-- ============================================================================
UPDATE roles
SET module_access = 'dashboard,patients,registration',
    description   = 'Patient directory (registration incl. household, printable read-only record summary -- no encounter entry)'
WHERE role_name = 'Records Staff';
