using FinApp.Shared.UI.Services;
using Microsoft.Maui.Storage;

namespace FinApp.App.Maui;

/// <summary>Persists the auth bearer token in the OS-protected <see cref="SecureStorage"/> (DPAPI on Windows).</summary>
public sealed class MauiTokenStore : ITokenStore
{
    private const string Key = "finapp-auth-token";

    public async Task<string?> GetAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch { return null; }
    }

    public async Task SetAsync(string token)
    {
        try { await SecureStorage.Default.SetAsync(Key, token); }
        catch { /* SecureStorage unavailable — token simply won't persist across launches */ }
    }

    public Task ClearAsync()
    {
        try { SecureStorage.Default.Remove(Key); }
        catch { /* ignore */ }
        return Task.CompletedTask;
    }
}
