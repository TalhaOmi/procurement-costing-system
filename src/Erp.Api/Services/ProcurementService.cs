using Erp.Api.Data;
using Erp.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erp.Api.Services;

/// <summary>Orchestrates the procure-to-pay flow. The business rules themselves live in Erp.Core.</summary>
public class ProcurementService
{
    private readonly ErpDbContext _db;
    private readonly ErpOptions _opt;

    public ProcurementService(ErpDbContext db, IOptions<ErpOptions> options)
    {
        _db = db;
        _opt = options.Value;
    }

    public async Task<Project> CreateProjectAsync(CreateProjectRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
            throw new DomainException("Project code and name are required.");
        if (r.Budget <= 0)
            throw new DomainException("A project needs a budget above zero.");
        if (await _db.Projects.AnyAsync(p => p.Code == r.Code))
            throw new DomainException($"Project {r.Code} already exists.");

        var project = new Project
        {
            Code = r.Code.Trim(), Name = r.Name.Trim(), Budget = r.Budget,
            AlertEmail = r.AlertEmail, AlertThresholdPct = r.AlertThresholdPct ?? 90
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    public async Task<Vendor> CreateVendorAsync(CreateVendorRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Vendor name is required.");
        var vendor = new Vendor { Name = r.Name.Trim(), Email = r.Email };
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();
        return vendor;
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(CreatePoRequest r)
    {
        var vendor = await _db.Vendors.FindAsync(r.VendorId) ?? throw new NotFoundException($"Vendor {r.VendorId} not found.");
        if (!vendor.IsActive) throw new DomainException($"Vendor {vendor.Name} is inactive.");
        var project = await _db.Projects.FindAsync(r.ProjectId) ?? throw new NotFoundException($"Project {r.ProjectId} not found.");
        if (project.Status != ProjectStatus.Active) throw new DomainException($"Project {project.Code} is not active.");
        if (r.Lines is null || r.Lines.Count == 0) throw new DomainException("A purchase order needs at least one line.");
        if (r.Lines.Any(l => l.Qty <= 0 || l.UnitPrice < 0)) throw new DomainException("Line quantity must be positive and price cannot be negative.");
        if (string.IsNullOrWhiteSpace(r.CreatedBy)) throw new DomainException("CreatedBy is required.");

        var next = await _db.PurchaseOrders.CountAsync() + 1;
        var po = new PurchaseOrder
        {
            Number = $"PO-{next:D4}", VendorId = r.VendorId, ProjectId = r.ProjectId, CreatedBy = r.CreatedBy,
            Lines = r.Lines.Select(l => new PoLine { Description = l.Description, CostCode = l.CostCode, Qty = l.Qty, UnitPrice = l.UnitPrice }).ToList()
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();
        return po;
    }

    public async Task<PurchaseOrder> SubmitAsync(int poId)
    {
        var po = await LoadPoAsync(poId);
        po.Submit(_opt.ApprovalThreshold);
        await _db.SaveChangesAsync();
        return po;
    }

    public async Task<PurchaseOrder> ApproveAsync(int poId, string approver)
    {
        var po = await LoadPoAsync(poId);
        po.Approve(approver);
        await _db.SaveChangesAsync();
        return po;
    }

    public async Task<Receipt> ReceiveAsync(int poId, ReceiveRequest r)
    {
        var po = await LoadPoAsync(poId);
        if (po.Status != PoStatus.Approved) throw new DomainException($"PO {po.Number} is {po.Status}; goods can only be received against Approved orders.");
        if (r.Lines is null || r.Lines.Count == 0) throw new DomainException("A receipt needs at least one line.");

        foreach (var line in r.Lines)
        {
            var poLine = po.Lines.FirstOrDefault(l => l.Id == line.PoLineId)
                         ?? throw new DomainException($"Line {line.PoLineId} is not on {po.Number}.");
            if (line.Qty <= 0) throw new DomainException("Received quantity must be positive.");
            var already = po.Receipts.SelectMany(x => x.Lines).Where(x => x.PoLineId == poLine.Id).Sum(x => x.QtyReceived);
            if (already + line.Qty > poLine.Qty)
                throw new DomainException($"Line {poLine.Id}: receiving {line.Qty} would exceed the ordered {poLine.Qty} (already received {already}).");
        }

        var receipt = new Receipt
        {
            PurchaseOrderId = po.Id, ReceivedBy = r.ReceivedBy,
            Lines = r.Lines.Select(l => new ReceiptLine { PoLineId = l.PoLineId, QtyReceived = l.Qty }).ToList()
        };
        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Number)) throw new DomainException("Invoice number is required.");
        _ = await _db.Vendors.FindAsync(r.VendorId) ?? throw new NotFoundException($"Vendor {r.VendorId} not found.");
        if (await _db.Invoices.AnyAsync(i => i.VendorId == r.VendorId && i.Number == r.Number))
            throw new DomainException($"Invoice {r.Number} already exists for this vendor.");

        var invoice = new Invoice
        {
            Number = r.Number, VendorId = r.VendorId, PurchaseOrderId = r.PurchaseOrderId,
            InvoiceDate = r.InvoiceDate, Amount = r.Amount,
            Lines = (r.Lines ?? new()).Select(l => new InvoiceLine { PoLineId = l.PoLineId, Qty = l.Qty, UnitPrice = l.UnitPrice }).ToList()
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    /// <summary>Runs the three-way match; on success posts cost to the project budget and fires the budget event.</summary>
    public async Task<InvoiceResult> MatchAndPostAsync(int invoiceId)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == invoiceId)
                      ?? throw new NotFoundException($"Invoice {invoiceId} not found.");
        if (invoice.Status == InvoiceStatus.Posted) throw new DomainException($"Invoice {invoice.Number} is already posted.");

        if (invoice.PurchaseOrderId is null)
        {
            invoice.Status = InvoiceStatus.Mismatch;
            invoice.MatchNote = "NO_PO: invoice is not linked to a purchase order.";
            await _db.SaveChangesAsync();
            return new InvoiceResult(invoice.Id, invoice.Number, invoice.Status.ToString(), invoice.MatchNote, 0m);
        }

        var po = await _db.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Receipts).ThenInclude(r => r.Lines)
            .FirstAsync(p => p.Id == invoice.PurchaseOrderId);

        var prior = await _db.Invoices.Include(i => i.Lines)
            .Where(i => i.PurchaseOrderId == po.Id && i.Status == InvoiceStatus.Posted && i.Id != invoice.Id)
            .ToListAsync();

        var result = ThreeWayMatcher.Match(po, invoice, prior, _opt.PriceTolerancePct);
        if (!result.IsMatch)
        {
            invoice.Status = InvoiceStatus.Mismatch;
            invoice.MatchNote = result.Summary;
            await _db.SaveChangesAsync();
            return new InvoiceResult(invoice.Id, invoice.Number, invoice.Status.ToString(), invoice.MatchNote, 0m);
        }

        var project = await _db.Projects.FirstAsync(p => p.Id == po.ProjectId);
        var actualBefore = (await _db.CostLedger.Where(c => c.ProjectId == project.Id).Select(c => c.Amount).ToListAsync()).Sum();
        var plan = InvoicePosting.Plan(project, po, invoice, actualBefore, DateTime.UtcNow);

        _db.CostLedger.AddRange(plan.Entries);
        invoice.Status = InvoiceStatus.Posted;
        invoice.MatchNote = null;

        // Business event (CHG-002): handled in the same transaction so the alert can never get lost or double-fire.
        if (plan.Event is not null)
        {
            project.AlertSent = true;
            var mail = BudgetAlerts.Build(plan.Event);
            if (mail is not null) _db.OutboxEmails.Add(mail);
        }

        await _db.SaveChangesAsync();
        return new InvoiceResult(invoice.Id, invoice.Number, invoice.Status.ToString(), null, plan.Entries.Sum(e => e.Amount));
    }

    private async Task<PurchaseOrder> LoadPoAsync(int id)
        => await _db.PurchaseOrders
               .Include(p => p.Lines)
               .Include(p => p.Receipts).ThenInclude(r => r.Lines)
               .FirstOrDefaultAsync(p => p.Id == id)
           ?? throw new NotFoundException($"Purchase order {id} not found.");
}
