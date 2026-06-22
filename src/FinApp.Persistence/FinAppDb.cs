using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Persistence;

/// <summary>Builds <see cref="FinAppDbContext"/> options for an encrypted (SQLCipher) SQLite file.</summary>
public static class FinAppDb
{
    /// <summary>
    /// Build options for a SQLite database at <paramref name="dbPath"/>. When <paramref name="password"/>
    /// is supplied the file is encrypted with SQLCipher (PRAGMA key) — financial data is never at rest in plaintext.
    /// </summary>
    public static DbContextOptions<FinAppDbContext> BuildOptions(string dbPath, string? password)
    {
        // Ensure the native SQLCipher provider is registered before any connection is opened.
        SQLitePCL.Batteries_V2.Init();

        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };
        if (!string.IsNullOrEmpty(password))
            builder.Password = password;

        return new DbContextOptionsBuilder<FinAppDbContext>()
            .UseSqlite(builder.ConnectionString)
            .Options;
    }
}
