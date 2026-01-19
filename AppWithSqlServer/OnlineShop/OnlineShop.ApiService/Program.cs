using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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

builder.AddSqlServerClient("sqldb");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
    connection.Open();

    var createDbCommand = new SqlCommand(@"
        IF NOT EXISTS (SELECT * 
            FROM sys.databases
            WHERE name = 'Shop')
        BEGIN
              CREATE DATABASE Shop;
        END;", connection);

    createDbCommand.ExecuteNonQuery();

    var createTableCommand = new SqlCommand(@"
        USE Shop;
        IF NOT EXISTS (SELECT *
            FROM sysobjects
            WHERE name='Products' and xtype='U')
        CREATE TABLE Products (
            Id INT PRIMARY KEY IDENTITY,
	        Title VARCHAR(100) NOT NULL,
            Summary NVARCHAR(2100) NOT NULL,
            Price DECIMAL(18,2) NOT NULL,
            DateAdded DATE NOT NULL,
        )", connection);

    createTableCommand.ExecuteNonQuery();

    var checkDataCommand =
    new SqlCommand(
        "SELECT COUNT(*) FROM Products",
        connection);

    var count = (int)checkDataCommand
        .ExecuteScalar();

    if (count == 0)
    {
        var insertCommand =
            new SqlCommand("""
            INSERT INTO Products (Title, Summary, Price, DateAdded)
            VALUES
            (
                'Wireless Optical Mouse',
                N'Ergonomic wireless optical mouse with adjustable DPI and long battery life, suitable for everyday office and home use.',
                24.99,
                '2025-01-05'
            ),
            (
                'Mechanical Gaming Keyboard',
                N'RGB backlit mechanical keyboard with blue switches, anti-ghosting keys, and durable aluminum frame.',
                129.99,
                '2025-01-06'
            ),
            (
                '27-inch 4K Monitor',
                N'27-inch UHD 4K monitor with IPS panel, 3840x2160 resolution, HDR support, and ultra-thin bezels.',
                399.00,
                '2025-01-07'
            ),
            (
                'USB-C Docking Station',
                N'Multi-port USB-C docking station with HDMI, DisplayPort, Ethernet, USB 3.0 ports, and 100W power delivery.',
                179.50,
                '2025-01-08'
            ),
            (
                'External SSD 1TB',
                N'Portable 1TB external SSD with USB 3.2 Gen 2 support, delivering fast read/write speeds in a compact design.',
                149.99,
                '2025-01-09'
            ),
            (
                'Noise-Cancelling Headphones',
                N'Over-ear wireless headphones with active noise cancellation, high-fidelity sound, and 30-hour battery life.',
                249.00,
                '2025-01-10'
            ),
            (
                'Webcam Full HD 1080p',
                N'Full HD 1080p webcam with built-in microphone, autofocus, and low-light correction for video conferencing.',
                69.99,
                '2025-01-11'
            ),
            (
                'Gaming Laptop Backpack',
                N'Water-resistant backpack designed for gaming laptops up to 17 inches, featuring padded compartments and USB charging port.',
                59.95,
                '2025-01-12'
            ),
            (
                'Wi-Fi 6 Router',
                N'Dual-band Wi-Fi 6 router offering high-speed wireless connectivity, improved range, and support for multiple devices.',
                199.00,
                '2025-01-13'
            ),
            (
                'Portable Laser Printer',
                N'Compact monochrome laser printer suitable for small offices, offering fast printing speeds and wireless connectivity.',
                289.99,
                '2025-01-14'
            );
            """,
                connection);

        insertCommand.ExecuteNonQuery();
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running.");

app.MapGet("/products",
    ([FromServices] SqlConnection connection) =>
    {
        connection.Open();

        var command = new SqlCommand(@"
            USE Shop;
            SELECT
                Title,
                Summary,
                Price
            FROM Products", connection);
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

            return products.ToArray();
        }
    });

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
