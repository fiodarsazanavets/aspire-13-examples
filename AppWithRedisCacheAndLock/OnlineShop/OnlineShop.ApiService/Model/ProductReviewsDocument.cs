using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OnlineShop.ApiService.Model;

public sealed class ProductReviewsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = default!;

    // Matches Product.Id from SQL Server
    public int ProductId { get; init; }

    public double AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public List<ProductReview> Reviews { get; init; } = new();
}

public sealed class ProductReview
{
    public string UserId { get; init; } = default!;

    public int Rating { get; init; }

    public string Comment { get; init; } = default!;

    public DateTime CreatedAt { get; init; }

    public bool VerifiedPurchase { get; init; }

    public ReviewStatus Status { get; init; } = ReviewStatus.Approved;
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}