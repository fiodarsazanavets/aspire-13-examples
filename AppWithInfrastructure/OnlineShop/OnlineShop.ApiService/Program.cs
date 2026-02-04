using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

builder.AddSqlServerDbContext<ProductsDbContext>("sqldb");
builder.AddMongoDBClient("mongodb");
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureQueueServiceClient("queues");
builder.AddRedisDistributedCache(connectionName: "cache");

builder.Services.AddSingleton<LocationUpdater>();
builder.Services.AddHostedService(
    sp => sp
        .GetRequiredService<LocationUpdater>());
builder.Services.AddSingleton<DataSeederService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataSeederService>());


builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
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
    async ([FromServices] ProductsDbContext db,
           [FromServices] IDistributedCache cache,
           CancellationToken ct) =>
    {
        const string cacheKey = "Products";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            var cachedProducts =
                JsonSerializer.Deserialize<ProductDto[]>(cached);

            return Results.Ok(cachedProducts ?? Array.Empty<ProductDto>());
        }

        var products = await db.Products
            .AsNoTracking()
            .Select(p => new ProductDto(
                Id: p.Id,
                Title: p.Title,
                Summary: p.Summary,
                Price: p.Price
            ))
            .ToArrayAsync(ct);

        var serializedProducts = JsonSerializer.Serialize(products);

        await cache.SetStringAsync(
            cacheKey,
            serializedProducts,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            },
            ct);

        return Results.Ok(products);
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
    [FromServices] ProductsDbContext dbContext,
    [FromServices] QueueServiceClient queueServiceClient,
    [FromServices] IConnectionMultiplexer redis,
    CancellationToken ct) =>
{
    if (basket is null || basket.Count == 0)
        return Results.BadRequest("Basket is empty.");

    var items = basket
        .Where(kvp => kvp.Value > 0)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    if (items.Count == 0)
        return Results.BadRequest("Basket contains no items with quantity > 0.");

    IDatabase redisDb = redis.GetDatabase();
    List<string> lockKeys = [];

    try
    {
        foreach (var productId in items.Keys)
        {
            string lockKey = $"product_lock_{productId}";

            bool lockAcquired = await redisDb.LockTakeAsync(
                lockKey,
                Environment.MachineName,
                TimeSpan.FromSeconds(10));

            if (!lockAcquired)
                return Results.StatusCode(423);

            lockKeys.Add(lockKey);
        }

        int orderId;
        decimal totalAmount;

        await using var tx = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            var productIds = items.Keys.ToArray();

            var priceLookup = await dbContext.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Price })
                .ToDictionaryAsync(x => x.Id, x => x.Price, ct);

            var missing = productIds.Where(id => !priceLookup.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
                return Results.BadRequest($"Unknown product ids: {string.Join(", ", missing)}");

            totalAmount = items.Sum(i => priceLookup[i.Key] * i.Value);

            var order = new OnlineShop.ApiService.Model.Order
            {
                TotalAmount = (double)totalAmount,
                Items = items.Select(i => new OrderItem
                {
                    ProductId = i.Key,
                    Quantity = i.Value
                }).ToList()
            };

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(ct);
            orderId = order.Id;

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Results.Problem($"Order creation failed: {ex.Message}");
        }

        var queueClient = queueServiceClient.GetQueueClient("orders-created");
        await queueClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var payload = JsonSerializer.Serialize(new { orderId });
        await queueClient.SendMessageAsync(payload, cancellationToken: ct);

        return Results.Created($"/api/orders/{orderId}", new
        {
            OrderId = orderId,
            TotalAmount = totalAmount
        });
    }
    finally
    {
        foreach (var lockKey in lockKeys)
        {
            await redisDb.LockReleaseAsync(lockKey, Environment.MachineName);
        }
    }
});

app.MapDefaultEndpoints();

app.MapHub<LocationHub>("/locationHub");

app.Run();