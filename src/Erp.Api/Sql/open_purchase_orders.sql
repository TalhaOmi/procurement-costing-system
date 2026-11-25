-- Report 2: open purchase orders (waiting for approval, or approved and not fully billed).
SELECT Number, Vendor, Project, Status, OrderedAmount, ReceivedAmount, InvoicedAmount,
       OrderedAmount - InvoicedAmount AS OpenBalance
FROM (
    SELECT po.Number AS Number,
           v.Name AS Vendor,
           p.Code AS Project,
           po.Status AS Status,
           COALESCE((SELECT SUM(pl.Qty * pl.UnitPrice)
                     FROM PoLines pl WHERE pl.PurchaseOrderId = po.Id), 0) AS OrderedAmount,
           COALESCE((SELECT SUM(rl.QtyReceived * pl.UnitPrice)
                     FROM Receipts r
                     JOIN ReceiptLines rl ON rl.ReceiptId = r.Id
                     JOIN PoLines pl ON pl.Id = rl.PoLineId
                     WHERE r.PurchaseOrderId = po.Id), 0) AS ReceivedAmount,
           COALESCE((SELECT SUM(il.Qty * il.UnitPrice)
                     FROM Invoices i
                     JOIN InvoiceLines il ON il.InvoiceId = i.Id
                     WHERE i.PurchaseOrderId = po.Id AND i.Status = 'Posted'), 0) AS InvoicedAmount
    FROM PurchaseOrders po
    JOIN Vendors v ON v.Id = po.VendorId
    JOIN Projects p ON p.Id = po.ProjectId
    WHERE po.Status IN ('PendingApproval', 'Approved')
) x
WHERE Status = 'PendingApproval' OR OrderedAmount - InvoicedAmount > 0.01
ORDER BY Number
