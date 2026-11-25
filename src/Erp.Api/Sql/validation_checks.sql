-- Data validation: every row returned is a bad or suspicious record. @threshold = PO approval threshold.
SELECT CheckName, RecordType, RecordId, Detail FROM (
    SELECT 'PO_LINE_BAD_VALUES' AS CheckName, 'PoLine' AS RecordType, pl.Id AS RecordId,
           'Qty=' || pl.Qty || ' UnitPrice=' || pl.UnitPrice AS Detail
    FROM PoLines pl WHERE pl.Qty <= 0 OR pl.UnitPrice < 0

    UNION ALL
    SELECT 'PO_WITHOUT_LINES', 'PurchaseOrder', po.Id, po.Number || ' is ' || po.Status || ' but has no lines'
    FROM PurchaseOrders po
    WHERE po.Status <> 'Draft' AND NOT EXISTS (SELECT 1 FROM PoLines pl WHERE pl.PurchaseOrderId = po.Id)

    UNION ALL
    SELECT 'PO_APPROVAL_BYPASS', 'PurchaseOrder', po.Id, po.Number || ' is above the approval threshold with no human approver'
    FROM PurchaseOrders po
    WHERE po.Status IN ('Approved', 'Closed')
      AND (po.ApprovedBy IS NULL OR po.ApprovedBy = 'system')
      AND (SELECT COALESCE(SUM(pl.Qty * pl.UnitPrice), 0) FROM PoLines pl WHERE pl.PurchaseOrderId = po.Id) > @threshold

    UNION ALL
    SELECT 'PO_SELF_APPROVED', 'PurchaseOrder', po.Id, po.Number || ' was approved by its creator ' || po.CreatedBy
    FROM PurchaseOrders po
    WHERE po.ApprovedBy IS NOT NULL AND LOWER(po.ApprovedBy) = LOWER(po.CreatedBy)

    UNION ALL
    SELECT 'OPEN_PO_INACTIVE_VENDOR', 'PurchaseOrder', po.Id, po.Number || ' is open for inactive vendor ' || v.Name
    FROM PurchaseOrders po JOIN Vendors v ON v.Id = po.VendorId
    WHERE v.IsActive = 0 AND po.Status IN ('PendingApproval', 'Approved')

    UNION ALL
    SELECT 'OVER_RECEIVED', 'PoLine', pl.Id, 'Received more than the ordered quantity of ' || pl.Qty
    FROM PoLines pl
    WHERE COALESCE((SELECT SUM(rl.QtyReceived) FROM ReceiptLines rl WHERE rl.PoLineId = pl.Id), 0) > pl.Qty

    UNION ALL
    SELECT 'INVOICE_TOTAL_MISMATCH', 'Invoice', i.Id,
           i.Number || ' header ' || i.Amount || ' vs lines ' ||
           COALESCE((SELECT SUM(il.Qty * il.UnitPrice) FROM InvoiceLines il WHERE il.InvoiceId = i.Id), 0)
    FROM Invoices i
    WHERE ABS(i.Amount - COALESCE((SELECT SUM(il.Qty * il.UnitPrice) FROM InvoiceLines il WHERE il.InvoiceId = i.Id), 0)) > 0.01

    UNION ALL
    SELECT 'DUPLICATE_INVOICE_NUMBER', 'Invoice', i.Id, i.Number || ' appears more than once for the same vendor'
    FROM Invoices i
    WHERE (SELECT COUNT(*) FROM Invoices o WHERE o.VendorId = i.VendorId AND o.Number = i.Number) > 1

    UNION ALL
    SELECT 'INVOICE_VENDOR_NE_PO_VENDOR', 'Invoice', i.Id, i.Number || ' vendor differs from vendor on ' || po.Number
    FROM Invoices i JOIN PurchaseOrders po ON po.Id = i.PurchaseOrderId
    WHERE po.VendorId <> i.VendorId

    UNION ALL
    SELECT 'POSTED_WITHOUT_LEDGER', 'Invoice', i.Id, i.Number || ' is Posted but has no cost ledger rows'
    FROM Invoices i
    WHERE i.Status = 'Posted' AND NOT EXISTS (SELECT 1 FROM CostLedger l WHERE l.InvoiceId = i.Id)

    UNION ALL
    SELECT 'PROJECT_NO_BUDGET', 'Project', p.Id, p.Code || ' is Active with a budget of ' || p.Budget
    FROM Projects p WHERE p.Status = 'Active' AND p.Budget <= 0
) checks
ORDER BY CheckName, RecordId
