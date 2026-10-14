-- ============================================================================
-- Healthcare Staff: sidebar limited to Dashboard + Patient Directory.
-- "encounters" stays so they can still start/open encounters (triage,
-- consultation, lab requests, medical certificates) from a patient's
-- profile; the Consultation and Laboratory sidebar tabs are no longer granted.
-- Users must log out and back in for the change to take effect.
-- ============================================================================
UPDATE roles
SET module_access = 'dashboard,patients,encounters'
WHERE role_name = 'Healthcare Staff';
