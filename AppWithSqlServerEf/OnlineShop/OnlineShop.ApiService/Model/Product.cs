using System.ComponentModel.DataAnnotations;

namespace OnlineShop.ApiService.Model;

public class Product
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2100)]
    public string Summary { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public DateTime DateAdded { get; set; }
}
