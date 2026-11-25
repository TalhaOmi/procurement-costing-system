using Erp.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Erp.Api.Services;

public class BudgetVsActualRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double Budget { get; set; }
    public double Committed { get; set; }
    public double Actual { get; set; }
    public double Variance { get; set; }
    public double? PctUsed { get; set; }
}

public class OpenPoRow
{
    public string Number { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Project { get; set; } = "";
    public string Status { get; set; } = "";
    public double OrderedAmount { get; set; }
    public double ReceivedAmount { get; set; }
    public double InvoicedAmount { get; set; }
    public double OpenBalance { get; set; }
}

public class UnmatchedInvoiceRow
{
    public string Number { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string? PoNumber { get; set; }
    public double Amount { get; set; }
    public string Status { get; set; } = "";
    public string? MatchNote { get; set; }
}

public class ValidationRow
{
    public string CheckName { get; set; } = "";
    public string RecordType { get; set; } = "";
    public int RecordId { get; set; }
    public string Detail { get; set; } = "";
}

/// <summary>Runs the SQL files in /Sql. Reports are plain SQL on purpose: they are reviewable and reusable outside the app.</summary>
public class ReportService
{
    private readonly ErpDbContext _db;
    private readonly ErpOptions _opt;

    public ReportService(ErpDbContext db, IOptions<ErpOptions> options)
    {
        _db = db;
        _opt = options.Value;
    }

    public static string LoadSql(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sql", name));

    public Task<List<BudgetVsActualRow>> BudgetVsActualAsync()
        => _db.Database.SqlQueryRaw<BudgetVsActualRow>(LoadSql("budget_vs_actual.sql")).ToListAsync();

    public Task<List<OpenPoRow>> OpenPurchaseOrdersAsync()
        => _db.Database.SqlQueryRaw<OpenPoRow>(LoadSql("open_purchase_orders.sql")).ToListAsync();

    public Task<List<UnmatchedInvoiceRow>> UnmatchedInvoicesAsync()
        => _db.Database.SqlQueryRaw<UnmatchedInvoiceRow>(LoadSql("unmatched_invoices.sql")).ToListAsync();

    public Task<List<ValidationRow>> ValidationAsync()
        => _db.Database
            .SqlQueryRaw<ValidationRow>(LoadSql("validation_checks.sql"), new SqliteParameter("@threshold", (double)_opt.ApprovalThreshold))
            .ToListAsync();
}
