using Microsoft.EntityFrameworkCore;
using PointOfSale.Data.Entities;

namespace PointOfSale.Data;

public class PointOfSaleDbContext(DbContextOptions<PointOfSaleDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLineItem> SaleLineItems => Set<SaleLineItem>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(b =>
        {
            b.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.HasIndex(p => p.Sku).IsUnique();
            b.Property(p => p.Price).HasPrecision(10, 2);

            b.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sale>(b =>
        {
            b.HasIndex(s => s.SaleNumber).IsUnique();
            b.Property(s => s.Subtotal).HasPrecision(10, 2);
            b.Property(s => s.TaxTotal).HasPrecision(10, 2);
            b.Property(s => s.DiscountTotal).HasPrecision(10, 2);
            b.Property(s => s.Total).HasPrecision(10, 2);
            b.Property(s => s.AmountTendered).HasPrecision(10, 2);
            b.Property(s => s.ChangeDue).HasPrecision(10, 2);

            b.HasOne(s => s.CashierUser)
                .WithMany()
                .HasForeignKey(s => s.CashierUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(s => s.VoidedByUser)
                .WithMany()
                .HasForeignKey(s => s.VoidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleLineItem>(b =>
        {
            b.Property(l => l.UnitPriceSnapshot).HasPrecision(10, 2);
            b.Property(l => l.DiscountAmount).HasPrecision(10, 2);
            b.Property(l => l.LineTotal).HasPrecision(10, 2);

            b.HasOne(l => l.Sale)
                .WithMany(s => s.LineItems)
                .HasForeignKey(l => l.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(l => l.Product)
                .WithMany(p => p.SaleLineItems)
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAdjustment>(b =>
        {
            b.HasOne(a => a.Product)
                .WithMany(p => p.StockAdjustments)
                .HasForeignKey(a => a.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.RelatedSale)
                .WithMany()
                .HasForeignKey(a => a.RelatedSaleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.PerformedByUser)
                .WithMany()
                .HasForeignKey(a => a.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
