-- Report 3: invoices that did not pass the three-way match (or have not been matched yet).
SELECT i.Number AS Number,
       v.Name AS Vendor,
       po.Number AS PoNumber,
       i.Amount AS Amount,
       i.Status AS Status,
       i.MatchNote AS MatchNote
FROM Invoices i
JOIN Vendors v ON v.Id = i.VendorId
LEFT JOIN PurchaseOrders po ON po.Id = i.PurchaseOrderId
WHERE i.Status IN ('Mismatch', 'Received')
ORDER BY i.Number
