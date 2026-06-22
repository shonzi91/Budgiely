using System.Security.Cryptography;
using FinApp.Shared.UI.Services;
using Microsoft.Maui.Storage;

namespace FinApp.App.Maui;

/// <summary>
/// Supplies the encrypted database path (app-data dir) and an OS-protected SQLCipher key.
/// The key is kept in <see cref="SecureStorage"/> (DPAPI-backed on Windows). If SecureStorage is
/// unavailable it degrades to a key file in the app-data directory so the DB still opens across launches.
/// </summary>
public sealed class MauiDatabaseSettings : IDatabaseSettings
{
    private const string KeyName = "finapp-db-key";

    public async Task<(string DbPath, string Key)> GetAsync()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "finapp.db");
        return (dbPath, await GetOrCreateKeyAsync());
    }

    private static async Task<string> GetOrCreateKeyAsync()
    {
        try
        {
            var key = await SecureStorage.Default.GetAsync(KeyName);
            if (!string.IsNullOrEmpty(key)) return key;

            key = NewKey();
            await SecureStorage.Default.SetAsync(KeyName, key);
            return key;
        }
        catch
        {
            // Degraded fallback: persist the key to a file so data stays readable across launches.
            var keyFile = Path.Combine(FileSystem.AppDataDirectory, "finapp.key");
            if (File.Exists(keyFile)) return await File.ReadAllTextAsync(keyFile);

            var key = NewKey();
            await File.WriteAllTextAsync(keyFile, key);
            return key;
        }
    }

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
