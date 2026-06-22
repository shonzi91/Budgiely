namespace FinApp.Shared.UI.Services;

/// <summary>Where the FinApp sync server lives. Set once at startup by the host (MAUI/WASM).</summary>
public sealed class ClientOptions
{
    public required string BaseUrl { get; init; }
}

/// <summary>Persists the auth token across launches. Implemented per host (MAUI uses SecureStorage).</summary>
public interface ITokenStore
{
    Task<string?> GetAsync();
    Task SetAsync(string token);
    Task ClearAsync();
}
