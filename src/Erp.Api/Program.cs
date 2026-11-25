using Erp.Api.Data;
using Erp.Api.Services;
using Erp.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ErpOptions>(builder.Configuration.GetSection("Erp"));
builder.Services.AddDbContext<ErpDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Erp") ?? "Data Source=erp.db"));
builder.Services.AddScoped<ProcurementService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Rule violations come back as 400 (or 404) with a plain message instead of a 500.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (NotFoundException ex) { ctx.Response.StatusCode = 404; await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }); }
    catch (DomainException ex) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }); }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    db.Database.EnsureCreated();
    if (app.Configuration.GetValue<bool>("Erp:SeedDemoData"))
        await DemoData.SeedAsync(db, scope.ServiceProvider.GetRequiredService<ProcurementService>());
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

// ---- master data ----
app.MapGet("/projects", (ErpDbContext db) => db.Projects.OrderBy(p => p.Code).ToListAsync());
app.MapPost("/projects", async (CreateProjectRequest r, ProcurementService s) => Results.Created("/projects", await s.CreateProjectAsync(r)));
app.MapGet("/vendors", (ErpDbContext db) => db.Vendors.OrderBy(v => v.Name).ToListAsync());
app.MapPost("/vendors", async (CreateVendorRequest r, ProcurementService s) => Results.Created("/vendors", await s.CreateVendorAsync(r)));

// ---- purchase orders ----
app.MapGet("/purchase-orders", async (ErpDbContext db) =>
    await db.PurchaseOrders.Include(p => p.Lines).OrderBy(p => p.Number).ToListAsync());
app.MapPost("/purchase-orders", async (CreatePoRequest r, ProcurementService s) => Results.Created("/purchase-orders", await s.CreatePurchaseOrderAsync(r)));
app.MapPost("/purchase-orders/{id:int}/submit", async (int id, ProcurementService s) => Results.Ok(await s.SubmitAsync(id)));
app.MapPost("/purchase-orders/{id:int}/approve", async (int id, ApproveRequest r, ProcurementService s) => Results.Ok(await s.ApproveAsync(id, r.Approver)));
app.MapPost("/purchase-orders/{id:int}/receipts", async (int id, ReceiveRequest r, ProcurementService s) => Results.Ok(await s.ReceiveAsync(id, r)));

// ---- invoices ----
app.MapGet("/invoices", async (ErpDbContext db) => await db.Invoices.Include(i => i.Lines).OrderBy(i => i.Number).ToListAsync());
app.MapPost("/invoices", async (CreateInvoiceRequest r, ProcurementService s) => Results.Created("/invoices", await s.CreateInvoiceAsync(r)));
app.MapPost("/invoices/{id:int}/match-and-post", async (int id, ProcurementService s) => Results.Ok(await s.MatchAndPostAsync(id)));

// ---- ledger + outbox ----
app.MapGet("/cost-ledger", (ErpDbContext db) => db.CostLedger.OrderBy(c => c.Id).ToListAsync());
app.MapGet("/outbox", (ErpDbContext db) => db.OutboxEmails.OrderByDescending(o => o.Id).ToListAsync());

// ---- reports (SQL files in /Sql) ----
app.MapGet("/reports/budget-vs-actual", (ReportService r) => r.BudgetVsActualAsync());
app.MapGet("/reports/open-purchase-orders", (ReportService r) => r.OpenPurchaseOrdersAsync());
app.MapGet("/reports/unmatched-invoices", (ReportService r) => r.UnmatchedInvoicesAsync());
app.MapGet("/reports/validation", (ReportService r) => r.ValidationAsync());

app.Run();

public partial class Program { }
