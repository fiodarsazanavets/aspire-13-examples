
using Microsoft.EntityFrameworkCore;
using OnlineShop.ApiService.Model;

namespace OnlineShop.ApiService;

public class DataSeederService(IServiceProvider serviceProvider) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();

        await RunMigrationAsync(context, stoppingToken);
        SeedData(context);
    }

    private static async Task RunMigrationAsync(
        ProductsDbContext context,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await context.Database.MigrateAsync(cancellationToken);
        });
    }

    private static void SeedData(ProductsDbContext context)
    {
        if (!context.Products.Any())
        {
            var products = new List<Product>
            {
                new Product
                {
                    Title = "Wireless Optical Mouse",
                    Summary = "Ergonomic wireless optical mouse with adjustable DPI and long battery life, suitable for everyday office and home use.",
                    Price = 24.99m,
                    DateAdded = new DateTime(2025, 1, 5)
                },
                new Product
                {
                    Title = "Mechanical Gaming Keyboard",
                    Summary = "RGB backlit mechanical keyboard with blue switches, anti-ghosting keys, and durable aluminum frame.",
                    Price = 129.99m,
                    DateAdded = new DateTime(2025, 1, 6)
                },
                new Product
                {
                    Title = "27-inch 4K Monitor",
                    Summary = "27-inch UHD 4K monitor with IPS panel, 3840x2160 resolution, HDR support, and ultra-thin bezels.",
                    Price = 399.00m,
                    DateAdded = new DateTime(2025, 1, 7)
                },
                new Product
                {
                    Title = "USB-C Docking Station",
                    Summary = "Multi-port USB-C docking station with HDMI, DisplayPort, Ethernet, USB 3.0 ports, and 100W power delivery.",
                    Price = 179.50m,
                    DateAdded = new DateTime(2025, 1, 8)
                },
                new Product
                {
                    Title = "External SSD 1TB",
                    Summary = "Portable 1TB external SSD with USB 3.2 Gen 2 support, delivering fast read/write speeds in a compact design.",
                    Price = 149.99m,
                    DateAdded = new DateTime(2025, 1, 9)
                },
                new Product
                {
                    Title = "Noise-Cancelling Headphones",
                    Summary = "Over-ear wireless headphones with active noise cancellation, high-fidelity sound, and 30-hour battery life.",
                    Price = 249.00m,
                    DateAdded = new DateTime(2025, 1, 10)
                },
                new Product
                {
                    Title = "Webcam Full HD 1080p",
                    Summary = "Full HD 1080p webcam with built-in microphone, autofocus, and low-light correction for video conferencing.",
                    Price = 69.99m,
                    DateAdded = new DateTime(2025, 1, 11)
                },
                new Product
                {
                    Title = "Gaming Laptop Backpack",
                    Summary = "Water-resistant backpack designed for gaming laptops up to 17 inches, featuring padded compartments and USB charging port.",
                    Price = 59.95m,
                    DateAdded = new DateTime(2025, 1, 12)
                },
                new Product
                {
                    Title = "Wi-Fi 6 Router",
                    Summary = "Dual-band Wi-Fi 6 router offering high-speed wireless connectivity, improved range, and support for multiple devices.",
                    Price = 199.00m,
                    DateAdded = new DateTime(2025, 1, 13)
                },
                new Product
                {
                    Title = "Portable Laser Printer",
                    Summary = "Compact monochrome laser printer suitable for small offices, offering fast printing speeds and wireless connectivity.",
                    Price = 289.99m,
                    DateAdded = new DateTime(2025, 1, 14)
                }
            };

            context.Products.AddRange(products);
            context.SaveChanges();
        }
    }
}
