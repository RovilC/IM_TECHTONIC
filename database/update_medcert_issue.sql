-- ============================================================================
-- Med Certs: RHU Management now "issues" certificates instead of approving
-- them (Reject removed). Certificates approved under the old wording become
-- 'Issued' so they show under the Issued filter.
-- ============================================================================
USE rhu_care;

UPDATE other_documents_services
SET status = 'Issued'
WHERE document_type = 'Medical Certificate' AND status = 'Approved';
