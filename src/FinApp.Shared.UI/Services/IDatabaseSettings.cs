namespace FinApp.Shared.UI.Services;

/// <summary>
/// Supplies the encrypted database location and key. Implemented per platform (e.g. the MAUI host
/// uses the app-data directory + an OS-protected key from SecureStorage), keeping this UI library
/// free of platform-specific dependencies.
/// </summary>
public interface IDatabaseSettings
{
    Task<(string DbPath, string Key)> GetAsync();
}
