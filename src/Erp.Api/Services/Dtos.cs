namespace Erp.Api.Services;

public class ErpOptions
{
    /// <summary>POs above this value need a second person's approval.</summary>
    public decimal ApprovalThreshold { get; set; } = 10_000m;
    /// <summary>Allowed unit-price difference between invoice and PO, in percent.</summary>
    public decimal PriceTolerancePct { get; set; } = 2m;
    public bool SeedDemoData { get; set; }
}

public record CreateProjectRequest(string Code, string Name, decimal Budget, string? AlertEmail, int? AlertThresholdPct);
public record CreateVendorRequest(string Name, string? Email);

public record PoLineDto(string Description, string CostCode, decimal Qty, decimal UnitPrice);
public record CreatePoRequest(int VendorId, int ProjectId, string CreatedBy, List<PoLineDto> Lines);
public record ApproveRequest(string Approver);

public record ReceiptLineDto(int PoLineId, decimal Qty);
public record ReceiveRequest(string ReceivedBy, List<ReceiptLineDto> Lines);

public record InvoiceLineDto(int? PoLineId, decimal Qty, decimal UnitPrice);
public record CreateInvoiceRequest(string Number, int VendorId, int? PurchaseOrderId, DateTime InvoiceDate, decimal Amount, List<InvoiceLineDto> Lines);

public record InvoiceResult(int InvoiceId, string Number, string Status, string? MatchNote, decimal PostedAmount);
