namespace Erp.Core;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

public enum ProjectStatus { Active, Closed }
public enum PoStatus { Draft, PendingApproval, Approved, Closed, Cancelled }
public enum InvoiceStatus { Received, Mismatch, Posted }

public class Project
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Budget { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    // ---- CHG-002 customization: custom fields + business event (see docs/CHANGES.md) ----
    /// <summary>Who gets the "budget threshold reached" email.</summary>
    public string? AlertEmail { get; set; }
    /// <summary>Percent of budget at which the alert fires. Default 90.</summary>
    public int AlertThresholdPct { get; set; } = 90;
    /// <summary>Set once the alert has fired so it is sent only once.</summary>
    public bool AlertSent { get; set; }
}

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int VendorId { get; set; }
    public int ProjectId { get; set; }
    public PoStatus Status { get; set; } = PoStatus.Draft;
    public string CreatedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public List<PoLine> Lines { get; set; } = new();
    public List<Receipt> Receipts { get; set; } = new();

    public decimal Total => Lines.Sum(l => l.Qty * l.UnitPrice);

    /// <summary>Business rule: above the threshold a PO waits for approval, otherwise it auto-approves.</summary>
    public void Submit(decimal approvalThreshold)
    {
        if (Status != PoStatus.Draft)
            throw new DomainException($"PO {Number} is {Status}; only Draft orders can be submitted.");
        if (Lines.Count == 0)
            throw new DomainException("A purchase order needs at least one line.");

        if (Total > approvalThreshold)
        {
            Status = PoStatus.PendingApproval;
        }
        else
        {
            Status = PoStatus.Approved;
            ApprovedBy = "system";
            ApprovedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Business rule: separation of duties - the creator cannot approve their own PO.</summary>
    public void Approve(string approver)
    {
        if (string.IsNullOrWhiteSpace(approver))
            throw new DomainException("An approver is required.");
        if (Status != PoStatus.PendingApproval)
            throw new DomainException($"PO {Number} is {Status}; only PendingApproval orders can be approved.");
        if (string.Equals(approver, CreatedBy, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("The creator of a purchase order cannot approve it.");

        Status = PoStatus.Approved;
        ApprovedBy = approver;
        ApprovedAt = DateTime.UtcNow;
    }
}

public class PoLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public string Description { get; set; } = "";
    public string CostCode { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Receipt
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string ReceivedBy { get; set; } = "";
    public List<ReceiptLine> Lines { get; set; } = new();
}

public class ReceiptLine
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public int PoLineId { get; set; }
    public decimal QtyReceived { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int VendorId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Received;
    public string? MatchNote { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? PoLineId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>Actual cost posted against a project budget.</summary>
public class CostLedgerEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? InvoiceId { get; set; }
    public string CostCode { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = "";
}

/// <summary>Stand-in for an email server: alerts are written here instead of being sent.</summary>
public class OutboxEmail
{
    public int Id { get; set; }
    public string ToAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
