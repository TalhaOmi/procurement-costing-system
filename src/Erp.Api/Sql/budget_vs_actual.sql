-- Report 1: budget versus actual by project.
-- Committed = value of approved/closed purchase orders. Actual = posted cost ledger.
SELECT Code, Name, Budget, Committed, Actual,
       Budget - Actual AS Variance,
       CASE WHEN Budget > 0 THEN ROUND(Actual * 100.0 / Budget, 1) ELSE NULL END AS PctUsed
FROM (
    SELECT p.Code AS Code,
           p.Name AS Name,
           p.Budget AS Budget,
           COALESCE((SELECT SUM(pl.Qty * pl.UnitPrice)
                     FROM PurchaseOrders po
                     JOIN PoLines pl ON pl.PurchaseOrderId = po.Id
                     WHERE po.ProjectId = p.Id AND po.Status IN ('Approved', 'Closed')), 0) AS Committed,
           COALESCE((SELECT SUM(l.Amount) FROM CostLedger l WHERE l.ProjectId = p.Id), 0) AS Actual
    FROM Projects p
) x
ORDER BY Code
