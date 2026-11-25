-- CHG-002: custom fields on Project + outbox table for the budget-threshold business event.
-- Apply to a database created BEFORE CHG-002. A fresh database already has these (EnsureCreated builds the current model).
ALTER TABLE Projects ADD COLUMN AlertEmail TEXT NULL;
ALTER TABLE Projects ADD COLUMN AlertThresholdPct INTEGER NOT NULL DEFAULT 90;
ALTER TABLE Projects ADD COLUMN AlertSent INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS OutboxEmails (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ToAddress TEXT NOT NULL,
    Subject TEXT NOT NULL,
    Body TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    SentAt TEXT NULL
);
