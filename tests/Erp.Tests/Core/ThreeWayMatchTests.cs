using Erp.Core;
using Xunit;

namespace Erp.Tests.Core;

public class ThreeWayMatchTests
{
    private static readonly Invoice[] None = Array.Empty<Invoice>();

    [Fact]
    public void Clean_invoice_matches()
    {
        var po = TestData.ApprovedPo();
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po), None);
        Assert.True(result.IsMatch);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Price_within_tolerance_matches()
    {
        var po = TestData.ApprovedPo();
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po, price1: 10.15m), None, priceTolerancePct: 2m);
        Assert.True(result.IsMatch);
    }

    [Fact]
    public void Price_above_tolerance_fails()
    {
        var po = TestData.ApprovedPo();
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po, price1: 11m), None, priceTolerancePct: 2m);
        Assert.False(result.IsMatch);
        Assert.Contains(result.Issues, i => i.Code == "PRICE_VARIANCE");
    }

    [Fact]
    public void Billing_more_than_received_fails()
    {
        var po = TestData.ApprovedPo();
        po.Receipts[0].Lines[0].QtyReceived = 60;
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po), None);
        Assert.False(result.IsMatch);
        Assert.Contains(result.Issues, i => i.Code == "QTY_NOT_RECEIVED");
    }

    [Fact]
    public void Nothing_received_fails()
    {
        var po = TestData.ApprovedPo(received: false);
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po), None);
        Assert.False(result.IsMatch);
        Assert.Contains(result.Issues, i => i.Code == "NO_RECEIPT");
    }

    [Fact]
    public void Already_billed_quantity_is_not_billable_again()
    {
        var po = TestData.ApprovedPo();
        var first = TestData.InvoiceFor(po);
        first.Id = 40;
        first.Status = InvoiceStatus.Posted;

        var second = TestData.InvoiceFor(po);
        var result = ThreeWayMatcher.Match(po, second, new[] { first });
        Assert.False(result.IsMatch);
        Assert.Contains(result.Issues, i => i.Code == "QTY_NOT_RECEIVED");
    }

    [Fact]
    public void Wrong_vendor_fails()
    {
        var po = TestData.ApprovedPo();
        var inv = TestData.InvoiceFor(po);
        inv.VendorId = 99;
        var result = ThreeWayMatcher.Match(po, inv, None);
        Assert.Contains(result.Issues, i => i.Code == "VENDOR_MISMATCH");
    }

    [Fact]
    public void Header_total_must_equal_lines()
    {
        var po = TestData.ApprovedPo();
        var inv = TestData.InvoiceFor(po);
        inv.Amount += 5m;
        var result = ThreeWayMatcher.Match(po, inv, None);
        Assert.Contains(result.Issues, i => i.Code == "TOTAL_MISMATCH");
    }

    [Fact]
    public void Line_not_on_po_fails()
    {
        var po = TestData.ApprovedPo();
        var inv = TestData.InvoiceFor(po);
        inv.Lines[1].PoLineId = 999;
        var result = ThreeWayMatcher.Match(po, inv, None);
        Assert.Contains(result.Issues, i => i.Code == "UNKNOWN_LINE");
    }

    [Fact]
    public void Unapproved_po_fails()
    {
        var po = TestData.ApprovedPo();
        po.Status = PoStatus.PendingApproval;
        var result = ThreeWayMatcher.Match(po, TestData.InvoiceFor(po), None);
        Assert.Contains(result.Issues, i => i.Code == "PO_NOT_APPROVED");
    }
}
