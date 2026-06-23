using FinApp.App.Web;
using FinApp.Shared.UI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Where the FinApp sync server lives. Read from wwwroot/appsettings[.Development].json ("ApiBaseUrl").
// When unset, fall back to this app's own origin — the one-origin deployment where the server hosts
// both the API and these static files. Local cross-origin dev sets it in appsettings.Development.json.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    apiBaseUrl = builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddSingleton(new ClientOptions { BaseUrl = apiBaseUrl });
builder.Services.AddScoped<FinAppApiClient>();
builder.Services.AddScoped<ITokenStore, WebTokenStore>();
builder.Services.AddScoped<Localizer>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<SyncClient>();
builder.Services.AddScoped<BudgetingState>();

await builder.Build().RunAsync();
