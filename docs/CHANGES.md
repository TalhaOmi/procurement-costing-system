# Change log and rollback guide

Every change to configuration, schema or business rules gets an entry here: what changed, why, how to check it worked, and how to undo it. This is the "document configurations and system changes" habit applied to this project. The format would work for a real ERP change request too.

How to roll back in general:
1. Back up first: `copy erp.db erp.db.bak` (SQLite) or take a database backup.
2. Run the rollback script named in the entry, or revert the commit (`git revert <hash>`), or set the old config value.
3. Re-run the verification step in the entry to confirm the system is back to its previous behaviour.

---

## CHG-001: Baseline schema and rules

| | |
|---|---|
| Date | 2026-11-24 |
| Type | Schema + business rules |
| What | Tables for projects, vendors, purchase orders and lines, receipts and lines, invoices and lines, cost ledger. Rules: PO approval threshold, three-way match, cost posting. |
| Why | Starting point for the procure-to-pay flow. |
| Applied by | `Database.EnsureCreated()` on first start (see "What I'd do differently" in the README: real migrations would replace this). |
| Verify | `GET /reports/budget-vs-actual` returns the demo projects; `dotnet test` passes. |
| Rollback | Stop the app and delete `erp.db`. All data is demo data. |

## CHG-002: Custom fields on Project and a budget-threshold business event

| | |
|---|---|
| Date | 2026-11-24 |
| Type | Customization (custom fields + business event) |
| What | Added `Project.AlertEmail`, `Project.AlertThresholdPct` (default 90) and `Project.AlertSent`. Added the `OutboxEmails` table. When a posted invoice takes a project's actual cost to the threshold percentage of its budget, a `BudgetThresholdCrossed` event fires once and writes an email to the outbox. |
| Why | Project managers should hear about a budget problem before the last invoice lands, not after. |
| Code | `Erp.Core/Events.cs`, `InvoicePosting.Plan` in `Erp.Core/Rules.cs`, handler step in `ProcurementService.MatchAndPostAsync`. |
| Apply to an existing database | `db/changes/002_add_project_alert_fields.sql`. Fresh databases already include it. |
| Verify | Unit tests `PostingAndBudgetTests` (threshold at 89% vs 90%, fires once, no address means no email). In the demo data, invoice `LE-0091` takes P-1002 to 91.7% and `GET /outbox` shows one email. |
| Rollback | `db/rollback/002_add_project_alert_fields.sql` (needs SQLite 3.35+), then `git revert` the CHG-002 commit so the code stops reading the columns. Alert emails already queued are lost. |
| Side effects | Projects without `AlertEmail` still get `AlertSent = true` when the threshold is reached, but no email is queued. |

## CHG-003: Price tolerance on the three-way match set to 2%

| | |
|---|---|
| Date | 2026-11-24 |
| Type | Configuration |
| What | `Erp:PriceTolerancePct = 2` in `appsettings.json`. An invoice line may differ from the PO unit price by up to 2% and still match. |
| Why | Fuel surcharges and rounding made exact-price matching reject good invoices. |
| Verify | Tests `Price_within_tolerance_matches` and `Price_above_tolerance_fails`. |
| Rollback | Set `Erp:PriceTolerancePct` to `0` (exact match) and restart. No data changes. Invoices already posted stay posted. |

## CHG-004: PO approval threshold set to 10,000

| | |
|---|---|
| Date | 2026-11-24 |
| Type | Configuration |
| What | `Erp:ApprovalThreshold = 10000`. A PO above this amount waits for a second person; at or below it auto-approves. The creator can never approve their own PO. |
| Verify | `ApprovalTests`; validation check `PO_APPROVAL_BYPASS` lists any approved PO above the threshold that has no human approver. |
| Rollback | Restore the previous value and restart. POs already approved are not re-evaluated. |

---

### Template for new entries

```
## CHG-00X: short title
| Date | Type (schema / config / rule / customization) |
| What | ... |
| Why | ... |
| Verify | test name or URL to check |
| Rollback | script, config value, or git revert, plus what is lost |
```
