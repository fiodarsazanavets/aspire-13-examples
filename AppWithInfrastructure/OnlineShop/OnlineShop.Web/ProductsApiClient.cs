using Microsoft.AspNetCore.Components.Authorization;
using OnlineShop.ServiceDefaults.Dtos;
using System.Net.Http.Headers;

namespace OnlineShop.Web;

public class ProductsApiClient(HttpClient httpClient, AuthenticationStateProvider authStateProvider)
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

    public async Task<HttpResponseMessage> MakeOrder(Dictionary<int, int> basket)
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var token = user.FindFirst("access_token")?.Value;

            if (!string.IsNullOrWhiteSpace(token))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await httpClient.PostAsJsonAsync("/api/orders", basket);
    }
}
