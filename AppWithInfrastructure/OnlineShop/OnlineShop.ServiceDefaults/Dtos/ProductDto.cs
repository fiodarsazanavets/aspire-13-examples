namespace OnlineShop.ServiceDefaults.Dtos;

public record ProductDto(
    int Id,
    string Title,
    string Summary,
    decimal Price
);
