using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MongoDB.Driver;
using OnlineShop.ApiService;
using OnlineShop.ApiService.Model;
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
builder.AddMongoDBClient("mongodb");
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");

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

    var mongoClient = scope.ServiceProvider
        .GetRequiredService<IMongoClient>();

    var database = mongoClient
        .GetDatabase("ShopDB");

    var collection = database
        .GetCollection<ProductReviewsDocument>("ProductReviews");

    var docCount = collection.CountDocuments(FilterDefinition<ProductReviewsDocument>.Empty);

    if (docCount == 0)
    {
        var productReviews = new List<ProductReviewsDocument>();

        foreach (var productId in Enumerable.Range(1, 10))
        {
            var reviews = new List<ProductReview>();

            var numberOfReviews = Random.Shared.Next(1, 5);

            for (var i = 0; i < numberOfReviews; i++)
            {
                reviews.Add(new ProductReview
                {
                    UserId = $"user-{Random.Shared.Next(1, 100)}",
                    Rating = Random.Shared.Next(3, 6), // 3–5
                    Comment = "Great product, works exactly as expected.",
                    CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                    VerifiedPurchase = Random.Shared.Next(0, 2) == 1,
                    Status = ReviewStatus.Approved
                });
            }

            var averageRating = reviews.Average(r => r.Rating);

            productReviews.Add(new ProductReviewsDocument
            {
                ProductId = productId, // matches SQL Products.Id
                AverageRating = Math.Round(averageRating, 2),
                TotalReviews = reviews.Count,
                Reviews = reviews
            });
        }

        collection.InsertMany(productReviews);
    }

    var tableServiceClient =
        scope.ServiceProvider.GetRequiredService<TableServiceClient>();

    var tableClient = tableServiceClient
        .GetTableClient("ProductMetadata");

    await tableClient.CreateIfNotExistsAsync();

    var existing = tableClient
        .Query<ProductMetadataEntity>(x => x.PartitionKey == "Product")
        .Take(1)
        .Any();

    if (!existing)
    {
        var entities = new List<ProductMetadataEntity>();

        foreach (var productId in Enumerable.Range(1, 10))
        {
            entities.Add(new ProductMetadataEntity
            {
                PartitionKey = "Product",
                RowKey = productId.ToString(),

                ReviewsEnabled = true,
                Featured = productId % 2 == 0,
                MaxReviewsPerUser = 1
            });
        }

        foreach (var entity in entities)
        {
            await tableClient.AddEntityAsync(entity);
        }
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

app.MapGet("/product-reviews",
    ([FromServices] IMongoClient mongoClient) =>
    {
        var database = mongoClient
            .GetDatabase("ShopDB");

        var collection = database
            .GetCollection<ProductReviewsDocument>(
                "ProductReviews");

        var productReviews = collection
            .Find(FilterDefinition<ProductReviewsDocument>.Empty)
            .ToList();

        return productReviews.ToArray();
    });

app.MapGet("/product-metadata",
   async (TableServiceClient tableServiceClient) =>
   {
       var tableClient = tableServiceClient
           .GetTableClient("ProductMetadata");

       var metadata = new List<ProductMetadataEntity>();

       var entities = tableClient
           .QueryAsync<ProductMetadataEntity>(
               x => x.PartitionKey == "Product");

       await foreach (var entity in entities)
       {
           metadata.Add(entity);
       }

       return metadata.ToArray();
   });

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
