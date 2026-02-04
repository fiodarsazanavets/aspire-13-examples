using System.ComponentModel.DataAnnotations;

namespace OnlineShop.ApiService.Model;

public class Order
{
    [Key]
    public int Id { get; set; }

    public double TotalAmount { get; set; }
    public string? Address { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
