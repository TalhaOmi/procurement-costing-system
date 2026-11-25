using Erp.Api.Services;
using Erp.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erp.Api.Data;

/// <summary>
/// Demo data for screenshots and the tests. The good records go through the real service (so the rules run);
/// the bad ones are inserted directly, the way an import or a manual database edit would, so the validation report has work to do.
/// </summary>
public static class DemoData
{
    public static async Task SeedAsync(ErpDbContext db, ProcurementService svc)
    {
        if (await db.Projects.AnyAsync()) return;

        var tower = await svc.CreateProjectAsync(new("P-1001", "Maple Ave Office Tower", 500_000m, "pm.tower@example.test", null));
        var warehouse = await svc.CreateProjectAsync(new("P-1002", "Riverside Warehouse", 120_000m, "pm.riverside@example.test", 90));

        var concrete = await svc.CreateVendorAsync(new("Northern Concrete Ltd", "ap@northernconcrete.example"));
        var steel = await svc.CreateVendorAsync(new("Apex Steel Supply", "billing@apexsteel.example"));
        var electric = await svc.CreateVendorAsync(new("Lakeside Electrical", "office@lakeside.example"));

        // PO-0001: small, auto-approved, fully received, invoice matches and posts.
        var po1 = await svc.CreatePurchaseOrderAsync(new(concrete.Id, tower.Id, "alice",
            new() { new("Ready-mix concrete 30MPa", "03-MAT", 50, 140m), new("Pump truck hire", "01-EQP", 2, 650m) }));
        await svc.SubmitAsync(po1.Id);
        po1 = await db.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.Id == po1.Id);
        await svc.ReceiveAsync(po1.Id, new("site.lead", po1.Lines.Select(l => new ReceiptLineDto(l.Id, l.Qty)).ToList()));
        var inv1 = await svc.CreateInvoiceAsync(new("NC-20871", concrete.Id, po1.Id, DateTime.UtcNow.AddDays(-12), 8_300m,
            po1.Lines.Select(l => new InvoiceLineDto(l.Id, l.Qty, l.UnitPrice)).ToList()));
        await svc.MatchAndPostAsync(inv1.Id);

        // PO-0002: above the approval threshold and still waiting for a second person.
        var po2 = await svc.CreatePurchaseOrderAsync(new(steel.Id, tower.Id, "alice",
            new() { new("Structural steel beams W14", "05-STL", 40, 1_850m) }));
        await svc.SubmitAsync(po2.Id);

        // PO-0003: approved by someone else, only partly received, then the vendor bills the full amount -> mismatch.
        var po3 = await svc.CreatePurchaseOrderAsync(new(steel.Id, tower.Id, "carol",
            new() { new("Rebar 20M bundles", "03-MAT", 200, 240m) }));
        await svc.SubmitAsync(po3.Id);
        await svc.ApproveAsync(po3.Id, "bob");
        po3 = await db.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.Id == po3.Id);
        await svc.ReceiveAsync(po3.Id, new("site.lead", new() { new(po3.Lines[0].Id, 120) }));
        var inv3 = await svc.CreateInvoiceAsync(new("AS-5530", steel.Id, po3.Id, DateTime.UtcNow.AddDays(-5), 48_000m,
            new() { new(po3.Lines[0].Id, 200, 240m) }));
        await svc.MatchAndPostAsync(inv3.Id);

        // PO-0004: warehouse job; this invoice takes the project past 90% of budget -> business event -> alert email.
        var po4 = await svc.CreatePurchaseOrderAsync(new(electric.Id, warehouse.Id, "alice",
            new() { new("Electrical fit-out", "16-ELC", 1, 110_000m) }));
        await svc.SubmitAsync(po4.Id);
        await svc.ApproveAsync(po4.Id, "bob");
        po4 = await db.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.Id == po4.Id);
        await svc.ReceiveAsync(po4.Id, new("site.lead", new() { new(po4.Lines[0].Id, 1) }));
        var inv4 = await svc.CreateInvoiceAsync(new("LE-0091", electric.Id, po4.Id, DateTime.UtcNow.AddDays(-2), 110_000m,
            new() { new(po4.Lines[0].Id, 1, 110_000m) }));
        await svc.MatchAndPostAsync(inv4.Id);

        // ---- Deliberately bad records (what a CSV import or a hand edit might leave behind) ----
        var legacy = new Vendor { Name = "Old Gravel Co (closed)", IsActive = false };
        db.Vendors.Add(legacy);
        db.Projects.Add(new Project { Code = "P-1003", Name = "Hospital Wing Reno (setup incomplete)", Budget = 0m });
        await db.SaveChangesAsync();

        var badProject = await db.Projects.FirstAsync(p => p.Code == "P-1003");
        var badPo = new PurchaseOrder
        {
            Number = "PO-9001", VendorId = legacy.Id, ProjectId = badProject.Id, CreatedBy = "import",
            Status = PoStatus.Approved, ApprovedBy = "import",                 // inactive vendor + self-approved
            Lines = { new PoLine { Description = "Gravel", CostCode = "02-SIT", Qty = -5, UnitPrice = 40m } } // negative qty
        };
        db.PurchaseOrders.Add(badPo);
        await db.SaveChangesAsync();

        db.Invoices.AddRange(
            // header says 1,000 but the line adds to 500
            new Invoice { Number = "OG-1", VendorId = legacy.Id, PurchaseOrderId = badPo.Id, Amount = 1_000m,
                          Lines = { new InvoiceLine { PoLineId = badPo.Lines[0].Id, Qty = 10, UnitPrice = 50m } } },
            // same vendor + number twice
            new Invoice { Number = "OG-2", VendorId = legacy.Id, Amount = 300m, Lines = { new InvoiceLine { Qty = 3, UnitPrice = 100m } } },
            new Invoice { Number = "OG-2", VendorId = legacy.Id, Amount = 300m, Lines = { new InvoiceLine { Qty = 3, UnitPrice = 100m } } });
        await db.SaveChangesAsync();
    }
}
