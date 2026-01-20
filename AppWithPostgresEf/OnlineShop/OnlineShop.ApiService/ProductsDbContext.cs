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
}
