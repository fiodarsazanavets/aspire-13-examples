using Microsoft.Extensions.Hosting;
using OnlineShop.AppHost.Extensions;
using OnlineShop.MailDev.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var maildev = builder.AddMailDev("maildev");

var idp = builder.AddKeycloakContainer(
    "idp", tag: "23.0")
    .ImportRealms("Keycloak")
    .WithExternalHttpEndpoints();

var cache = builder.AddRedis("cache");

var sql = builder.AddSqlServer("sql").WithLifetime(ContainerLifetime.Persistent);
var sqldb = sql.AddDatabase("sqldb");

var mongo = builder.AddMongoDB("mongo").WithLifetime(ContainerLifetime.Persistent);
var mongodb = mongo.AddDatabase("mongodb");

var storage = builder.AddAzureStorage("storage")
   .RunAsEmulator();

var tables = storage
   .AddTables("tables");

var blobs = storage
   .AddBlobContainer("blobs");

var apiService = builder.AddProject<Projects.OnlineShop_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(tables)
    .WaitFor(tables)
    .WithReference(mongodb)
    .WaitFor(mongodb)
    .WithReference(idp)
    .WaitFor(idp)
    .WaitFor(sqldb)
    .WithReference(sqldb);

var webFrontend = builder
    .AddProject<Projects.OnlineShop_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(idp, env: "Identity__ClientSecret")
    .WaitFor(idp)
    .WithReference(cache)
    .WaitFor(cache);

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
