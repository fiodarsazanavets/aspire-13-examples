using OnlineShop.Web.Dtos;

namespace OnlineShop.Web;

public class ProductsApiClient(HttpClient httpClient)
{
    public async Task<ProductDto[]> GetProductsAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<ProductDto>? products = null;

        await foreach (var product in httpClient.GetFromJsonAsAsyncEnumerable<ProductDto>("/products", cancellationToken))
        {
            if (products?.Count >= maxItems)
            {
                break;
            }
            if (product is not null)
            {
                products ??= [];
                products.Add(product);
            }
        }

        return products?.ToArray() ?? [];
    }
}
