-- ============================================================================
-- Restricts the System Administrator role on an already-seeded database to:
--   Dashboard (view only), User Management, Roles & Permissions,
--   Audit Logs (view only).
-- Users must log out and back in for the change to take effect.
-- ============================================================================
UPDATE roles
SET module_access = 'dashboard,users,roles,audit',
    description   = 'User accounts, roles & permissions; view-only dashboard and audit logs'
WHERE role_name = 'System Administrator';
