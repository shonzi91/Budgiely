using FinApp.App.Web;
using FinApp.Shared.UI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Where the FinApp sync server lives. Read from wwwroot/appsettings.json ("ApiBaseUrl"),
// falling back to the local dev server so a plain `dotnet run` works out of the box.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5179";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddSingleton(new ClientOptions { BaseUrl = apiBaseUrl });
builder.Services.AddScoped<FinAppApiClient>();
builder.Services.AddScoped<ITokenStore, WebTokenStore>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<SyncClient>();
builder.Services.AddScoped<BudgetingState>();

await builder.Build().RunAsync();
