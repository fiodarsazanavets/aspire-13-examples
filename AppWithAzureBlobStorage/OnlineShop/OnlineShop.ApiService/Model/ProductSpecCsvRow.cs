namespace OnlineShop.ApiService.Model;

public sealed class ProductSpecCsvRow
{
    public int ProductId { get; set; }
    public string Category { get; set; } = default!;
    public int WarrantyMonths { get; set; }

    public bool ReviewsEnabled { get; set; }
    public bool Featured { get; set; }
    public int MaxReviewsPerUser { get; set; }
}
