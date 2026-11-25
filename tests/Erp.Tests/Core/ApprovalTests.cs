using Erp.Core;
using Xunit;

namespace Erp.Tests.Core;

public class ApprovalTests
{
    private static PurchaseOrder Draft(decimal qty, decimal price) => new()
    {
        Number = "PO-X", CreatedBy = "alice",
        Lines = { new PoLine { Qty = qty, UnitPrice = price, CostCode = "03-MAT" } }
    };

    [Fact]
    public void Below_threshold_is_auto_approved()
    {
        var po = Draft(1, 9_999m);
        po.Submit(10_000m);
        Assert.Equal(PoStatus.Approved, po.Status);
        Assert.Equal("system", po.ApprovedBy);
    }

    [Fact]
    public void Exactly_at_threshold_is_auto_approved()
    {
        var po = Draft(1, 10_000m);
        po.Submit(10_000m);
        Assert.Equal(PoStatus.Approved, po.Status);
    }

    [Fact]
    public void Above_threshold_waits_for_approval()
    {
        var po = Draft(1, 10_000.01m);
        po.Submit(10_000m);
        Assert.Equal(PoStatus.PendingApproval, po.Status);
        Assert.Null(po.ApprovedBy);
    }

    [Fact]
    public void Creator_cannot_approve_own_po()
    {
        var po = Draft(1, 50_000m);
        po.Submit(10_000m);
        var ex = Assert.Throws<DomainException>(() => po.Approve("ALICE"));
        Assert.Contains("creator", ex.Message);
    }

    [Fact]
    public void Another_user_can_approve()
    {
        var po = Draft(1, 50_000m);
        po.Submit(10_000m);
        po.Approve("bob");
        Assert.Equal(PoStatus.Approved, po.Status);
        Assert.Equal("bob", po.ApprovedBy);
    }

    [Fact]
    public void Empty_po_cannot_be_submitted()
    {
        var po = new PurchaseOrder { Number = "PO-E", CreatedBy = "alice" };
        Assert.Throws<DomainException>(() => po.Submit(10_000m));
    }

    [Fact]
    public void Cannot_submit_twice()
    {
        var po = Draft(1, 100m);
        po.Submit(10_000m);
        Assert.Throws<DomainException>(() => po.Submit(10_000m));
    }
}
