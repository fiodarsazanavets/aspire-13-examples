using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OnlineShop.Web;
using OnlineShop.Web.Components;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.AddRedisOutputCache("cache");

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient(
    "OidcBackchannel", o => o.BaseAddress = new("http://idp"));

builder.Services.AddHttpClient<ProductsApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
        OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(
    CookieAuthenticationDefaults.AuthenticationScheme)
.AddOpenIdConnect()
.ConfigureWebAppOpenIdConnect();

builder.Services.AddHttpClient();

builder.Services.AddHttpClient("ollama", c =>
{
    c.BaseAddress = new Uri("http://ollama");
})
.AddServiceDiscovery();

var phiConnectionString = builder.Configuration.GetConnectionString("phi35");

DbConnectionStringBuilder csBuilder = new()
{
    ConnectionString = phiConnectionString
};

if (!csBuilder.TryGetValue("Endpoint", out var ollamaEndpoint))
{
    throw new InvalidDataException(
        "Ollama connection string is not properly configured.");
}

builder.Services.AddSingleton(sp =>
{
    IKernelBuilder kb = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070
    kb.AddOllamaChatCompletion(
        modelId: "phi3.5",
        endpoint: new Uri((string)ollamaEndpoint)
    );
#pragma warning restore SKEXP0070

    return kb.Build();
});

builder.Services.AddSingleton<IChatHistoryService, ChatHistoryService>();

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Kernel>()
        .GetRequiredService<IChatCompletionService>());

builder.Services.AddSignalR();

builder.Services.AddSignalR()
    .AddHubOptions<ChatHub>(o => o.EnableDetailedErrors = true);

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chat");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.MapGet("/login", async (HttpContext ctx, string? returnUrl) =>
{
    returnUrl ??= "/";

    await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = returnUrl });
});

app.MapPost("/logout", async (HttpContext ctx, string? returnUrl) =>
{
    returnUrl ??= "/";

    // sign out of the local cookie and the OIDC provider
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = returnUrl });
});

app.Run();
