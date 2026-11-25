using Erp.Core;

namespace Erp.Tests.Core;

internal static class TestData
{
    /// <summary>An approved PO: 100 bags of cement at 10.00 (line 1) and 5 mixers at 200.00 (line 2). Total 2,000.</summary>
    public static PurchaseOrder ApprovedPo(bool received = true)
    {
        var po = new PurchaseOrder
        {
            Id = 1, Number = "PO-0001", VendorId = 7, ProjectId = 3, CreatedBy = "alice",
            Status = PoStatus.Approved, ApprovedBy = "bob",
            Lines =
            {
                new PoLine { Id = 11, PurchaseOrderId = 1, Description = "Cement", CostCode = "03-MAT", Qty = 100, UnitPrice = 10m },
                new PoLine { Id = 12, PurchaseOrderId = 1, Description = "Mixer", CostCode = "01-EQP", Qty = 5, UnitPrice = 200m },
            }
        };
        if (received)
        {
            po.Receipts.Add(new Receipt
            {
                Id = 1, PurchaseOrderId = 1, ReceivedBy = "site",
                Lines =
                {
                    new ReceiptLine { Id = 1, ReceiptId = 1, PoLineId = 11, QtyReceived = 100 },
                    new ReceiptLine { Id = 2, ReceiptId = 1, PoLineId = 12, QtyReceived = 5 },
                }
            });
        }
        return po;
    }

    public static Invoice InvoiceFor(PurchaseOrder po, decimal qty1 = 100, decimal price1 = 10m, decimal qty2 = 5, decimal price2 = 200m)
        => new()
        {
            Id = 50, Number = "INV-1", VendorId = po.VendorId, PurchaseOrderId = po.Id,
            Amount = qty1 * price1 + qty2 * price2,
            Lines =
            {
                new InvoiceLine { Id = 1, InvoiceId = 50, PoLineId = 11, Qty = qty1, UnitPrice = price1 },
                new InvoiceLine { Id = 2, InvoiceId = 50, PoLineId = 12, Qty = qty2, UnitPrice = price2 },
            }
        };
}
