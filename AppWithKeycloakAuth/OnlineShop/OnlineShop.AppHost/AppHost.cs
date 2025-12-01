using Microsoft.Extensions.Hosting;
using OnlineShop.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var idp = builder.AddKeycloakContainer(
    "idp", tag: "23.0")
    .ImportRealms("Keycloak")
    .WithExternalHttpEndpoints();


var apiService = builder.AddProject<Projects.OnlineShop_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(idp)
    .WaitFor(idp);
    

var webFrontend = builder
    .AddProject<Projects.OnlineShop_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(idp, env: "Identity__ClientSecret")
    .WaitFor(idp);

if (builder.Environment.IsDevelopment())
{
    var webAppHttp = webFrontend.GetEndpoint("http");
    var webAppHttps = webFrontend.GetEndpoint("https");

    idp.WithEnvironment("WEBAPP_HTTP", () =>
        $"{webAppHttp.Scheme}://{webAppHttp.Host}:{webAppHttp.Port}");

    if (webAppHttps.Exists)
    {
        idp.WithEnvironment("WEBAPP_HTTP_CONTAINERHOST",
            webAppHttps);
        idp.WithEnvironment("WEBAPP_HTTPS", () =>
            $"{webAppHttps.Scheme}://{webAppHttps.Host}:{webAppHttps.Port}");
    }
    else
    {
        idp.WithEnvironment("WEBAPP_HTTP_CONTAINERHOST",
            webAppHttp);
    }
}


builder.Build().Run();
