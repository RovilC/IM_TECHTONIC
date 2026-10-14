-- ============================================================================
-- Adds the RHU Management approval workflow to an already-created database:
--   * Lab Requests tab   -- approve lab requests to the RHU lab or refer them
--                           to an external laboratory (printable request form)
--   * Med Certs tab      -- approve / print medical certificates
-- Users must log out and back in for the module change to take effect.
-- ============================================================================
ALTER TABLE laboratory
    MODIFY status VARCHAR(20) NOT NULL DEFAULT 'For Approval',
    ADD COLUMN request_no        VARCHAR(30)  NULL,
    ADD COLUMN lab_destination   VARCHAR(20)  NULL,
    ADD COLUMN external_facility VARCHAR(150) NULL,
    ADD COLUMN approved_by       INT          NULL,
    ADD COLUMN approved_at       DATETIME     NULL,
    ADD COLUMN approval_remarks  TEXT         NULL,
    ADD CONSTRAINT fk_lab_approved_by FOREIGN KEY (approved_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL;
CREATE INDEX idx_lab_request_no ON laboratory (request_no);

ALTER TABLE other_documents_services
    ADD COLUMN approved_by INT      NULL,
    ADD COLUMN approved_at DATETIME NULL,
    ADD CONSTRAINT fk_doc_approved_by FOREIGN KEY (approved_by) REFERENCES users_staff(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL;

UPDATE roles
SET module_access = 'dashboard,reports,lab_requests,medcerts',
    description   = 'Overall dashboard, all sections'' reports, lab request and medical certificate approvals',
    can_edit      = TRUE
WHERE role_name = 'RHU Management';
