using Microsoft.EntityFrameworkCore;
using OnlineShop.ApiService.Model;

namespace OnlineShop.ApiService;

public class ProductsDbContext : DbContext
{
    public ProductsDbContext(
        DbContextOptions<ProductsDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => new { x.OrderId, x.ProductId });

            entity.HasOne(x => x.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(x => x.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

