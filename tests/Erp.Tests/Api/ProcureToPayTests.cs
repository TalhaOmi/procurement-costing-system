using Erp.Api.Data;
using Erp.Api.Services;
using Erp.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Api;

/// <summary>Runs the real service and the real SQL report files against an in-memory SQLite database.</summary>
public sealed class ProcureToPayTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ErpDbContext _db;
    private readonly ProcurementService _svc;
    private readonly ReportService _reports;

    public ProcureToPayTests()
    {
        // The Sql/*.sql files are copied next to the test assembly because Erp.Api marks them as content.
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _db = new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        var options = Options.Create(new ErpOptions { ApprovalThreshold = 10_000m, PriceTolerancePct = 2m });
        _svc = new ProcurementService(_db, options);
        _reports = new ReportService(_db, options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Full_flow_posts_cost_and_fires_budget_alert_once()
    {
        var project = await _svc.CreateProjectAsync(new("P-T1", "Test job", 10_000m, "pm@example.test", 90));
        var vendor = await _svc.CreateVendorAsync(new("Test Vendor", null));

        var po = await _svc.CreatePurchaseOrderAsync(new(vendor.Id, project.Id, "alice",
            new() { new("Widgets", "03-MAT", 10, 920m) }));           // 9,200 = 92% of budget, under the 10k approval threshold
        await _svc.SubmitAsync(po.Id);
        var lineId = (await _db.PoLines.FirstAsync(l => l.PurchaseOrderId == po.Id)).Id;
        await _svc.ReceiveAsync(po.Id, new("site", new() { new(lineId, 10) }));
        var invoice = await _svc.CreateInvoiceAsync(new("T-1", vendor.Id, po.Id, DateTime.UtcNow, 9_200m, new() { new(lineId, 10, 920m) }));

        var result = await _svc.MatchAndPostAsync(invoice.Id);

        Assert.Equal("Posted", result.Status);
        var report = (await _reports.BudgetVsActualAsync()).Single(r => r.Code == "P-T1");
        Assert.Equal(9_200d, report.Actual);
        Assert.Equal(92d, report.PctUsed!.Value);
        var mail = Assert.Single(await _db.OutboxEmails.ToListAsync());
        Assert.Equal("pm@example.test", mail.ToAddress);
        Assert.True((await _db.Projects.FirstAsync(p => p.Id == project.Id)).AlertSent);
    }

    [Fact]
    public async Task Invoice_for_more_than_received_is_held_and_listed_as_unmatched()
    {
        var project = await _svc.CreateProjectAsync(new("P-T2", "Test job 2", 100_000m, null, null));
        var vendor = await _svc.CreateVendorAsync(new("Test Vendor 2", null));
        var po = await _svc.CreatePurchaseOrderAsync(new(vendor.Id, project.Id, "alice", new() { new("Bolts", "05-STL", 100, 10m) }));
        await _svc.SubmitAsync(po.Id);
        var lineId = (await _db.PoLines.FirstAsync(l => l.PurchaseOrderId == po.Id)).Id;
        await _svc.ReceiveAsync(po.Id, new("site", new() { new(lineId, 40) }));
        var invoice = await _svc.CreateInvoiceAsync(new("T-2", vendor.Id, po.Id, DateTime.UtcNow, 1_000m, new() { new(lineId, 100, 10m) }));

        var result = await _svc.MatchAndPostAsync(invoice.Id);

        Assert.Equal("Mismatch", result.Status);
        Assert.Contains("QTY_NOT_RECEIVED", result.MatchNote!);
        Assert.Empty(await _db.CostLedger.ToListAsync());
        var unmatched = Assert.Single(await _reports.UnmatchedInvoicesAsync());
        Assert.Equal("T-2", unmatched.Number);
    }

    [Fact]
    public async Task Large_po_is_listed_as_open_until_approved_and_cannot_receive_goods()
    {
        var project = await _svc.CreateProjectAsync(new("P-T3", "Test job 3", 500_000m, null, null));
        var vendor = await _svc.CreateVendorAsync(new("Test Vendor 3", null));
        var po = await _svc.CreatePurchaseOrderAsync(new(vendor.Id, project.Id, "alice", new() { new("Crane", "01-EQP", 1, 50_000m) }));
        await _svc.SubmitAsync(po.Id);
        var lineId = (await _db.PoLines.FirstAsync(l => l.PurchaseOrderId == po.Id)).Id;

        await Assert.ThrowsAsync<DomainException>(() => _svc.ReceiveAsync(po.Id, new("site", new() { new(lineId, 1) })));
        var open = Assert.Single(await _reports.OpenPurchaseOrdersAsync());
        Assert.Equal("PendingApproval", open.Status);

        await Assert.ThrowsAsync<DomainException>(() => _svc.ApproveAsync(po.Id, "alice"));
        await _svc.ApproveAsync(po.Id, "bob");
        Assert.Equal(50_000d, (await _reports.OpenPurchaseOrdersAsync()).Single().OpenBalance);
    }

    [Fact]
    public async Task Demo_data_seeds_and_validation_flags_the_planted_bad_records()
    {
        await DemoData.SeedAsync(_db, _svc);

        var flagged = (await _reports.ValidationAsync()).Select(v => v.CheckName).ToHashSet();
        foreach (var expected in new[]
                 {
                     "PO_LINE_BAD_VALUES", "PO_SELF_APPROVED", "OPEN_PO_INACTIVE_VENDOR",
                     "INVOICE_TOTAL_MISMATCH", "DUPLICATE_INVOICE_NUMBER", "PROJECT_NO_BUDGET"
                 })
            Assert.True(flagged.Contains(expected), $"{expected} was not flagged");

        // The clean demo records must not trip any check: every flagged row belongs to the planted PO-9001 / OG-* / P-1003 data.
        var rows = await _reports.ValidationAsync();
        Assert.True(rows.All(r => r.Detail.Contains("PO-9001") || r.Detail.Contains("OG-") || r.Detail.Contains("P-1003") || r.RecordType == "PoLine"),
            "a validation check flagged a record that should be clean");

        var warehouse = (await _reports.BudgetVsActualAsync()).Single(r => r.Code == "P-1002");
        Assert.True(warehouse.PctUsed > 90);
        Assert.Single(await _db.OutboxEmails.ToListAsync());
    }
}
