using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinApp.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinApp.Server.Tests;

/// <summary>
/// Hosts the real server against an isolated, temporary SQLite file (migrated on startup), so each test
/// class gets a clean database. Provides helpers to register users and obtain authenticated clients.
/// </summary>
public sealed class FinAppServerFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"finapp-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FinApp", $"Data Source={_dbPath}");
    }

    /// <summary>Register a new user and return a client with its bearer token attached.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndAuthAsync(
        string username, string? email = null, string password = "password123")
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest(username, email ?? $"{username}@example.com", password));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    /// <summary>A SignalR client wired through the in-memory test server (long polling), authenticated with the token.</summary>
    public HubConnection CreateHubConnection(string token)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hubs/sync"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}
