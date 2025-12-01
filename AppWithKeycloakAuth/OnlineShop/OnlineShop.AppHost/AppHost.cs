var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.OnlineShop_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.OnlineShop_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
