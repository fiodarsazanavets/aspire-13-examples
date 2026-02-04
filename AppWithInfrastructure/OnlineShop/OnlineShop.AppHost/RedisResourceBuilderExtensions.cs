using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace OnlineShop.AppHost;

public static class RedisResourceBuilderExtensions
{
    public static IResourceBuilder<RedisResource> WithClearCacheCommand(
        this IResourceBuilder<RedisResource> builder)
    {
        var options = new CommandOptions
        {
            IconName = "Broom",
            IconVariant = IconVariant.Filled,
            UpdateState = ctx =>
                ctx.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled
        };

        builder.WithCommand(
            name: "clear-cache",
            displayName: "Clear cache",
            executeCommand: _ => RunAsync(builder),
            commandOptions: options);

        return builder;
    }


    private static async Task<ExecuteCommandResult> RunAsync(
        IResourceBuilder<RedisResource> builder)
    {
        var connStr = await builder.Resource.GetConnectionStringAsync()
            ?? throw new InvalidOperationException(
                "Could not resolve Redis connection string.");

        await using var mux = await ConnectionMultiplexer.ConnectAsync(connStr);
        var db = mux.GetDatabase();

        // Clear everything across all DBs. Prefer FLUSHDB if you only want the current DB.
        await db.ExecuteAsync("FLUSHALL");

        return CommandResults.Success();
    }

}
