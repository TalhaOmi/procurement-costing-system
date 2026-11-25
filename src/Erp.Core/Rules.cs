namespace Erp.Core;

public record MatchIssue(string Code, string Message);

public record MatchResult(bool IsMatch, IReadOnlyList<MatchIssue> Issues)
{
    public string Summary => string.Join("; ", Issues.Select(i => $"{i.Code}: {i.Message}"));
}

/// <summary>
/// Three-way match: purchase order (what we ordered), receipt (what arrived) and invoice (what the vendor bills).
/// </summary>
public static class ThreeWayMatcher
{
    public static MatchResult Match(
        PurchaseOrder po,
        Invoice invoice,
        IEnumerable<Invoice> priorPostedInvoices,
        decimal priceTolerancePct = 2m)
    {
        var issues = new List<MatchIssue>();

        if (po.Status != PoStatus.Approved)
            issues.Add(new("PO_NOT_APPROVED", $"PO {po.Number} is {po.Status}."));

        if (po.VendorId != invoice.VendorId)
            issues.Add(new("VENDOR_MISMATCH", "Invoice vendor differs from the PO vendor."));

        if (invoice.Lines.Count == 0)
            issues.Add(new("NO_LINES", "Invoice has no lines."));

        var linesTotal = invoice.Lines.Sum(l => l.Qty * l.UnitPrice);
        if (Math.Abs(linesTotal - invoice.Amount) > 0.01m)
            issues.Add(new("TOTAL_MISMATCH", $"Header amount {invoice.Amount:0.00} but lines add up to {linesTotal:0.00}."));

        if (po.Receipts.Count == 0)
            issues.Add(new("NO_RECEIPT", "Nothing has been received against this PO yet."));

        var prior = priorPostedInvoices.SelectMany(i => i.Lines).ToList();

        foreach (var line in invoice.Lines)
        {
            var poLine = line.PoLineId is null ? null : po.Lines.FirstOrDefault(p => p.Id == line.PoLineId);
            if (poLine is null)
            {
                issues.Add(new("UNKNOWN_LINE", $"Invoice line {line.PoLineId?.ToString() ?? "(none)"} is not on PO {po.Number}."));
                continue;
            }

            var variancePct = poLine.UnitPrice == 0
                ? (line.UnitPrice == 0 ? 0m : 100m)
                : Math.Abs(line.UnitPrice - poLine.UnitPrice) / poLine.UnitPrice * 100m;
            if (variancePct > priceTolerancePct)
                issues.Add(new("PRICE_VARIANCE",
                    $"Line {poLine.Id}: billed {line.UnitPrice:0.00} vs PO {poLine.UnitPrice:0.00} ({variancePct:0.0}% > {priceTolerancePct}%)."));

            var received = po.Receipts.SelectMany(r => r.Lines).Where(r => r.PoLineId == poLine.Id).Sum(r => r.QtyReceived);
            var alreadyBilled = prior.Where(p => p.PoLineId == poLine.Id).Sum(p => p.Qty);
            var billable = received - alreadyBilled;
            if (line.Qty > billable)
                issues.Add(new("QTY_NOT_RECEIVED",
                    $"Line {poLine.Id}: billing {line.Qty} but only {billable} received and not yet billed."));
        }

        return new MatchResult(issues.Count == 0, issues);
    }
}

public static class BudgetMonitor
{
    /// <summary>True when actual cost has reached the alert percentage of the budget.</summary>
    public static bool ThresholdReached(decimal budget, decimal actual, int thresholdPct)
        => budget > 0 && actual >= budget * thresholdPct / 100m;
}

public record PostingPlan(IReadOnlyList<CostLedgerEntry> Entries, decimal ActualAfter, BudgetThresholdCrossed? Event);

public static class InvoicePosting
{
    /// <summary>
    /// Turns a matched invoice into cost-ledger rows (one per cost code) and works out whether the
    /// project just crossed its alert threshold. Pure function: no database access.
    /// </summary>
    public static PostingPlan Plan(Project project, PurchaseOrder po, Invoice invoice, decimal actualBefore, DateTime now)
    {
        var entries = invoice.Lines
            .GroupBy(l => po.Lines.First(p => p.Id == l.PoLineId).CostCode)
            .Select(g => new CostLedgerEntry
            {
                ProjectId = project.Id,
                PurchaseOrderId = po.Id,
                InvoiceId = invoice.Id,
                CostCode = g.Key,
                Amount = g.Sum(x => x.Qty * x.UnitPrice),
                PostedAt = now,
                Description = $"Invoice {invoice.Number} / PO {po.Number}"
            })
            .ToList();

        var after = actualBefore + entries.Sum(e => e.Amount);

        BudgetThresholdCrossed? ev = null;
        if (!project.AlertSent && BudgetMonitor.ThresholdReached(project.Budget, after, project.AlertThresholdPct))
            ev = new BudgetThresholdCrossed(project.Id, project.Code, project.Budget, after, project.AlertThresholdPct, project.AlertEmail);

        return new PostingPlan(entries, after, ev);
    }
}
