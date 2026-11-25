-- Rollback for CHG-002. Needs SQLite 3.35+ for DROP COLUMN. Back up the .db file first (copy erp.db erp.db.bak).
-- This discards any queued alert emails and the per-project alert settings.
DROP TABLE IF EXISTS OutboxEmails;
ALTER TABLE Projects DROP COLUMN AlertSent;
ALTER TABLE Projects DROP COLUMN AlertThresholdPct;
ALTER TABLE Projects DROP COLUMN AlertEmail;
