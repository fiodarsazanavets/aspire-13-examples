using Aspire.Hosting.Testing;
using OnlineShop.Tests.Helpers;
using System.Net;

namespace OnlineShop.Tests;

public class WebTests
{
    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.OnlineShop_Web>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        var response = await httpClient.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductsReturnsRightContent()
    {
        // Arrange
        var appHost = await
            DistributedApplicationTestingBuilder
                .CreateAsync<Projects.OnlineShop_Web>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");

        var response = await httpClient
            .GetAsync("/");
        var responseBody = await HtmlHelpers
            .GetDocumentAsync(response);
        var titleElement = responseBody
            .QuerySelector("h1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(titleElement);
        Assert.Equal(
            "Products",
        titleElement.InnerHtml);
    }

}
