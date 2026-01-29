using Azure;
using Azure.Data.Tables;

namespace OnlineShop.ApiService.Model;

public sealed class ProductMetadataEntity : ITableEntity
{
    // Required by Table Storage
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Business metadata
    public bool ReviewsEnabled { get; set; }
    public bool Featured { get; set; }
    public int MaxReviewsPerUser { get; set; }
}
