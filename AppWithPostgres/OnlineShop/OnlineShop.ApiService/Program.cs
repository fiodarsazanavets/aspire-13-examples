using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OnlineShop.ApiService;
using OnlineShop.ServiceDefaults.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddHttpClient(
    "OidcBackchannel", o => o.BaseAddress = new("http://idp"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;

})
.AddJwtBearer()
.ConfigureApiJwt();

builder.AddNpgsqlDataSource("postgresdb");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<NpgsqlConnection>();
    await connection.OpenAsync();

    await using (var createTableCmd = new NpgsqlCommand(@"
        CREATE TABLE IF NOT EXISTS products (
            id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            title       VARCHAR(100) NOT NULL,
            summary     VARCHAR(2100) NOT NULL,
            price       NUMERIC(18,2) NOT NULL,
            date_added  DATE NOT NULL
        );
    ", connection))
    {
        await createTableCmd.ExecuteNonQueryAsync();
    }

    long count;
    await using (var checkDataCmd = new NpgsqlCommand("SELECT COUNT(*) FROM products;", connection))
    {
        count = (long)(await checkDataCmd.ExecuteScalarAsync())!;
    }

    if (count == 0)
    {
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO products (title, summary, price, date_added)
            VALUES
            (
                'Wireless Optical Mouse',
                'Ergonomic wireless optical mouse with adjustable DPI and long battery life, suitable for everyday office and home use.',
                24.99,
                DATE '2025-01-05'
            ),
            (
                'Mechanical Gaming Keyboard',
                'RGB backlit mechanical keyboard with blue switches, anti-ghosting keys, and durable aluminum frame.',
                129.99,
                DATE '2025-01-06'
            ),
            (
                '27-inch 4K Monitor',
                '27-inch UHD 4K monitor with IPS panel, 3840x2160 resolution, HDR support, and ultra-thin bezels.',
                399.00,
                DATE '2025-01-07'
            ),
            (
                'USB-C Docking Station',
                'Multi-port USB-C docking station with HDMI, DisplayPort, Ethernet, USB 3.0 ports, and 100W power delivery.',
                179.50,
                DATE '2025-01-08'
            ),
            (
                'External SSD 1TB',
                'Portable 1TB external SSD with USB 3.2 Gen 2 support, delivering fast read/write speeds in a compact design.',
                149.99,
                DATE '2025-01-09'
            ),
            (
                'Noise-Cancelling Headphones',
                'Over-ear wireless headphones with active noise cancellation, high-fidelity sound, and 30-hour battery life.',
                249.00,
                DATE '2025-01-10'
            ),
            (
                'Webcam Full HD 1080p',
                'Full HD 1080p webcam with built-in microphone, autofocus, and low-light correction for video conferencing.',
                69.99,
                DATE '2025-01-11'
            ),
            (
                'Gaming Laptop Backpack',
                'Water-resistant backpack designed for gaming laptops up to 17 inches, featuring padded compartments and USB charging port.',
                59.95,
                DATE '2025-01-12'
            ),
            (
                'Wi-Fi 6 Router',
                'Dual-band Wi-Fi 6 router offering high-speed wireless connectivity, improved range, and support for multiple devices.',
                199.00,
                DATE '2025-01-13'
            ),
            (
                'Portable Laser Printer',
                'Compact monochrome laser printer suitable for small offices, offering fast printing speeds and wireless connectivity.',
                289.99,
                DATE '2025-01-14'
            );
        ", connection);

        await insertCmd.ExecuteNonQueryAsync();
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running.");

app.MapGet("/products",
    ([FromServices] NpgsqlConnection connection) =>
    {
        connection.Open();

        var command = new NpgsqlCommand(@"
            SELECT
                title,
                summary,
                price
            FROM products
            ORDER BY id;", connection);

        var products = new List<ProductDto>();

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                products.Add(new ProductDto(
                    Title: reader.GetString(0),
                    Summary: reader.GetString(1),
                    Price: reader.GetDecimal(2)
                ));
            }
        }

        return products.ToArray();
    });


app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
