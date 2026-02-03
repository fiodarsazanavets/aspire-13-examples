using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using OnlineShop.ApiService;
using OnlineShop.ApiService.Model;
using OnlineShop.ServiceDefaults.Dtos;
using StackExchange.Redis;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddSignalR();

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
builder.AddAzureQueueServiceClient("queues");
builder.AddRedisDistributedCache(connectionName: "cache");

builder.Services.AddSingleton<LocationUpdater>();
builder.Services.AddHostedService(
    sp => sp
        .GetRequiredService<LocationUpdater>());

builder.Services.AddAuthorization();

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
        
        -- Products table
        IF NOT EXISTS (
            SELECT *
            FROM sysobjects
            WHERE name='Products' AND xtype='U'
        )
        BEGIN
            CREATE TABLE Products (
                Id INT PRIMARY KEY IDENTITY,
                Title VARCHAR(100) NOT NULL,
                Summary NVARCHAR(2100) NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                DateAdded DATE NOT NULL
            );
        END
        
        -- Orders table
        IF NOT EXISTS (
            SELECT *
            FROM sysobjects
            WHERE name='Orders' AND xtype='U'
        )
        BEGIN
            CREATE TABLE Orders (
                Id INT IDENTITY PRIMARY KEY,
                TotalAmount DECIMAL(18,2) NOT NULL
            );
        END
        
        -- OrderItems table
        IF NOT EXISTS (
            SELECT *
            FROM sysobjects
            WHERE name='OrderItems' AND xtype='U'
        )
        BEGIN
            CREATE TABLE OrderItems (
                OrderId INT NOT NULL,
                ProductId INT NOT NULL,
                Quantity INT NOT NULL,
        
                CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, ProductId),
        
                CONSTRAINT FK_OrderItems_Orders
                    FOREIGN KEY (OrderId) REFERENCES Orders(Id),
        
                CONSTRAINT FK_OrderItems_Products
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );
        END
        ", connection);

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

    var blobServiceClient =
        scope.ServiceProvider
        .GetRequiredService<BlobServiceClient>();

    var containerClient = blobServiceClient
       .GetBlobContainerClient("products");

    // Create the container if it doesn't exist
    await containerClient.CreateIfNotExistsAsync();

    var blobClient = containerClient
       .GetBlobClient("products-specs.csv");

    // Check if the blob already exists
    if (await blobClient.ExistsAsync())
    {
        return;
    }

    var productSpecs = new List<ProductSpecCsvRow>();

    foreach (var productId in Enumerable.Range(1, 10))
    {
        productSpecs.Add(new ProductSpecCsvRow
        {
            ProductId = productId,
            ReviewsEnabled = true,
            Featured = productId % 2 == 0,
            MaxReviewsPerUser = 1,

            Category = productId % 2 == 0 ? "Laptop" : "Peripheral",
            WarrantyMonths = productId % 2 == 0 ? 24 : 12
        });
    }

    using (var memoryStream = new MemoryStream())
    using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
    using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
    {
        csv.WriteRecords(productSpecs);
        writer.Flush();

        memoryStream.Position = 0;
        await blobClient.UploadAsync(memoryStream, overwrite: true);
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "API service is running.");

app.MapGet("/products",
    async ([FromServices] SqlConnection connection,
           [FromServices] IDistributedCache cache) =>
    {
        const string cacheKey = "Products";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            var cachedProducts =
                JsonSerializer.Deserialize<ProductDto[]>(cached);

            return Results.Ok(cachedProducts);
        }

        await connection.OpenAsync();

        await using var command = new SqlCommand(@"
            USE Shop;
            SELECT Id, Title, Summary, Price
            FROM Products;", connection);

        var products = new List<ProductDto>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new ProductDto(
                Id: reader.GetInt32(0),
                Title: reader.GetString(1),
                Summary: reader.GetString(2),
                Price: reader.GetDecimal(3)
            ));
        }

        var serializedProducts = JsonSerializer.Serialize(products.ToArray());

        await cache.SetStringAsync(
            cacheKey,
            serializedProducts,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        return Results.Ok(products.ToArray());
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

app.MapGet("/product-specs", async (
   BlobServiceClient blobServiceClient) =>
{
    var containerClient = blobServiceClient
        .GetBlobContainerClient("products");

    var blobClient = containerClient
        .GetBlobClient("product-specs.csv");

    if (!await blobClient.ExistsAsync())
    {
        return Array.Empty<ProductSpecCsvRow>();
    }

    List<ProductSpecCsvRow> productSpecs;

    // Download the CSV from the blob
    var downloadResponse = await blobClient.DownloadAsync();

    using (var stream = downloadResponse.Value.Content)
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        productSpecs = csv
            .GetRecords<ProductSpecCsvRow>()
            .ToList();
    }

    return productSpecs.ToArray();
});

app.MapPost("/api/orders", async (
    Dictionary<int, int> basket,
    [FromServices] SqlConnection dbConnection,
    [FromServices] QueueServiceClient queueServiceClient,
    [FromServices] IDistributedCache cache,
    [FromServices] IConnectionMultiplexer redis) =>
{
    if (basket is null || basket.Count == 0)
        return Results.BadRequest("Basket is empty.");

    var items = basket
        .Where(kvp => kvp.Value > 0)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (items.Count == 0)
        return Results.BadRequest("Basket contains no items with quantity > 0.");

    IDatabase db = redis.GetDatabase();

    List<string> lockKeys = [];

    foreach (var productId in items.Keys)
    {
        string lockKey = $"product_lock_{productId}";

        bool lockAcquired = await db.LockTakeAsync(
           lockKey,
           Environment.MachineName,
           TimeSpan.FromSeconds(10));

        if (!lockAcquired)
        {
            return Results.StatusCode(423);
        }

        lockKeys.Add($"product_lock_{productId}");
    }

    if (dbConnection.State != ConnectionState.Open)
        await dbConnection.OpenAsync();

    int orderId;
    decimal totalAmount;

    await using var tx =
        (SqlTransaction)await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

    try
    {
        var productIds = items.Keys.ToList();
        var inParams = string.Join(", ", productIds.Select((_, i) => $"@p{i}"));

        var priceLookup = new Dictionary<int, decimal>();

        await using (var cmd = new SqlCommand($@"
            SELECT Id, Price
            FROM Products
            WHERE Id IN ({inParams});",
            dbConnection, tx))
        {
            for (int i = 0; i < productIds.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", productIds[i]);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                priceLookup[reader.GetInt32(0)] = reader.GetDecimal(1);
        }

        totalAmount = items.Sum(i => priceLookup[i.Key] * i.Value);

        await using (var cmd = new SqlCommand(@"
            INSERT INTO Orders (TotalAmount)
            VALUES (@totalAmount);
            SELECT CAST(SCOPE_IDENTITY() AS INT);",
            dbConnection, tx))
        {
            var p = cmd.Parameters.Add("@totalAmount", SqlDbType.Decimal);
            p.Precision = 18;
            p.Scale = 2;
            p.Value = totalAmount;

            orderId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        if (orderId <= 0)
            return Results.Problem("Failed to create order.");

        await using (var cmd = new SqlCommand(@"
            INSERT INTO OrderItems (OrderId, ProductId, Quantity)
            VALUES (@orderId, @productId, @quantity);",
            dbConnection, tx))
        {
            cmd.Parameters.Add("@orderId", SqlDbType.Int).Value = orderId;
            var pProductId = cmd.Parameters.Add("@productId", SqlDbType.Int);
            var pQuantity = cmd.Parameters.Add("@quantity", SqlDbType.Int);

            foreach (var (productId, qty) in items)
            {
                pProductId.Value = productId;
                pQuantity.Value = qty;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await tx.CommitAsync();
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.Problem($"Order creation failed: {ex.Message}");
    }
    finally
    {
        foreach (var lockKey in lockKeys)
        {
            await db.LockReleaseAsync(
               lockKey,
               Environment.MachineName);
        }
    }

    var queueClient = queueServiceClient
       .GetQueueClient("orders-created");
    queueClient.CreateIfNotExists();

    var message = JsonSerializer.Serialize(new { orderId });

    queueClient.SendMessage(
       JsonSerializer.Serialize(message));

    return Results.Created($"/api/orders/{orderId}", new
    {
        OrderId = orderId,
        TotalAmount = totalAmount
    });
});

app.MapDefaultEndpoints();

app.MapHub<LocationHub>("/locationHub");

app.Run();