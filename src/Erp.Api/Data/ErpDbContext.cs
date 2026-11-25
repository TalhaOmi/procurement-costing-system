using Erp.Core;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Data;

public class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PoLine> PoLines => Set<PoLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<CostLedgerEntry> CostLedger => Set<CostLedgerEntry>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no decimal type. Storing money as REAL keeps SQL SUM() correct for the reports.
        // On SQL Server this would be decimal(18,2) instead (see README, "What I'd do differently").
        builder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Project>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>();
            e.HasIndex(p => p.Code).IsUnique();
        });

        m.Entity<PurchaseOrder>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>();
            e.Ignore(p => p.Total);
            e.HasIndex(p => p.Number).IsUnique();
            e.HasOne<Vendor>().WithMany().HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Project>().WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Receipts).WithOne().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Receipt>()
            .HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<Invoice>(e =>
        {
            e.Property(i => i.Status).HasConversion<string>();
            // Deliberately NOT unique on (VendorId, Number): the validation report is what catches duplicates
            // that arrive through imports. A production system would enforce this and keep the check as a safety net.
            e.HasIndex(i => new { i.VendorId, i.Number });
            e.HasOne<Vendor>().WithMany().HasForeignKey(i => i.VendorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<CostLedgerEntry>(e =>
        {
            e.ToTable("CostLedger");
            e.HasOne<Project>().WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
