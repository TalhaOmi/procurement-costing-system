using Erp.Core;
using Xunit;

namespace Erp.Tests.Core;

public class PostingAndBudgetTests
{
    private static Project Proj(decimal budget, bool alertSent = false) => new()
    {
        Id = 3, Code = "P-1001", Name = "Test", Budget = budget,
        AlertEmail = "pm@example.test", AlertThresholdPct = 90, AlertSent = alertSent
    };

    [Fact]
    public void Posting_creates_one_ledger_row_per_cost_code()
    {
        var po = TestData.ApprovedPo();
        var plan = InvoicePosting.Plan(Proj(100_000m), po, TestData.InvoiceFor(po), 0m, DateTime.UtcNow);

        Assert.Equal(2, plan.Entries.Count);
        Assert.Equal(1_000m, plan.Entries.Single(e => e.CostCode == "03-MAT").Amount);
        Assert.Equal(1_000m, plan.Entries.Single(e => e.CostCode == "01-EQP").Amount);
        Assert.Equal(2_000m, plan.ActualAfter);
    }

    [Fact]
    public void No_alert_below_threshold()
    {
        var po = TestData.ApprovedPo();
        var plan = InvoicePosting.Plan(Proj(100_000m), po, TestData.InvoiceFor(po), 0m, DateTime.UtcNow);
        Assert.Null(plan.Event);
    }

    [Fact]
    public void Alert_fires_when_posting_reaches_90_percent()
    {
        var po = TestData.ApprovedPo();
        // 87,000 already posted + 2,000 invoice = 89,000 of 100,000 (89%): no alert
        Assert.Null(InvoicePosting.Plan(Proj(100_000m), po, TestData.InvoiceFor(po), 87_000m, DateTime.UtcNow).Event);
        // 88,000 + 2,000 = 90,000 (exactly 90%): alert
        var plan = InvoicePosting.Plan(Proj(100_000m), po, TestData.InvoiceFor(po), 88_000m, DateTime.UtcNow);
        Assert.NotNull(plan.Event);
        Assert.Equal(90_000m, plan.Event!.Actual);
    }

    [Fact]
    public void Alert_fires_only_once()
    {
        var po = TestData.ApprovedPo();
        var plan = InvoicePosting.Plan(Proj(100_000m, alertSent: true), po, TestData.InvoiceFor(po), 95_000m, DateTime.UtcNow);
        Assert.Null(plan.Event);
    }

    [Fact]
    public void Zero_budget_never_alerts()
    {
        Assert.False(BudgetMonitor.ThresholdReached(0m, 1_000m, 90));
    }

    [Fact]
    public void Alert_email_names_project_and_percentage()
    {
        var ev = new BudgetThresholdCrossed(3, "P-1001", 100_000m, 92_000m, 90, "pm@example.test");
        var mail = BudgetAlerts.Build(ev);
        Assert.NotNull(mail);
        Assert.Equal("pm@example.test", mail!.ToAddress);
        Assert.Contains("P-1001", mail.Subject);
        Assert.Contains("92", mail.Subject);
    }

    [Fact]
    public void No_alert_email_without_an_address()
    {
        var ev = new BudgetThresholdCrossed(3, "P-1001", 100_000m, 92_000m, 90, null);
        Assert.Null(BudgetAlerts.Build(ev));
    }
}
