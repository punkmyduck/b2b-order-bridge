using OrderBridge.Web.Components;
using OrderBridge.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
foreach (var (name, section) in new[] { ("Orders", "OrdersApi"), ("Integrations", "IntegrationsApi") })
{
    builder.Services.AddHttpClient(name, client =>
    {
        var address = builder.Configuration[$"{section}:BaseUrl"]
            ?? throw new InvalidOperationException($"{section}:BaseUrl is required.");
        client.BaseAddress = new Uri(address.TrimEnd('/') + "/", UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(name == "Orders" ? 30 : 5);
    });
}
builder.Services.AddScoped(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    return new OrderBridgeApiClient(factory.CreateClient("Orders"), factory.CreateClient("Integrations"),
        provider.GetRequiredService<ILogger<OrderBridgeApiClient>>());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
