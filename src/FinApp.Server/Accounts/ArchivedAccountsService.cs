using System.Data;
using System.Globalization;
using FinApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Accounts;

/// <summary>
/// Soft-deletion for accounts: when the last member leaves an account we archive it for a grace period
/// (<see cref="RetentionDays"/>) instead of deleting outright, so it can be reactivated. Backed by a
/// standalone <c>ArchivedAccounts</c> table created idempotently with <c>CREATE TABLE IF NOT EXISTS</c>
/// (same migration-free pattern as <see cref="FinApp.Server.Auth.AvatarService"/>: prod builds its schema via
/// <c>EnsureCreated</c>, which never ALTERs existing tables). The account row itself stays intact (members
/// included) so the leaver keeps access to restore it; it's just filtered out of the active account list.
/// </summary>
public sealed class ArchivedAccountsService(FinAppDbContext db)
{
    public const int RetentionDays = 30;

    public Task EnsureSchemaAsync(CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"ArchivedAccounts\" (\"AccountId\" text PRIMARY KEY, \"ArchivedAt\" text NOT NULL)", ct);

    public Task ArchiveAsync(Guid accountId, CancellationToken ct = default) =>
        WriteAsync(
            "INSERT INTO \"ArchivedAccounts\" (\"AccountId\", \"ArchivedAt\") VALUES (@id, @at) " +
            "ON CONFLICT (\"AccountId\") DO UPDATE SET \"ArchivedAt\" = @at",
            cmd => { AddParam(cmd, "@id", accountId.ToString()); AddParam(cmd, "@at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); },
            ct);

    public Task UnarchiveAsync(Guid accountId, CancellationToken ct = default) =>
        WriteAsync("DELETE FROM \"ArchivedAccounts\" WHERE \"AccountId\" = @id",
            cmd => AddParam(cmd, "@id", accountId.ToString()), ct);

    /// <summary>The set of archived account ids (used to filter the active list).</summary>
    public async Task<HashSet<Guid>> ArchivedIdsAsync(CancellationToken ct = default)
    {
        var result = new HashSet<Guid>();
        var conn = db.Database.GetDbConnection();
        var opened = await OpenAsync(conn, ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"AccountId\" FROM \"ArchivedAccounts\"";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                if (Guid.TryParse(reader.GetString(0), out var id)) result.Add(id);
        }
        finally { if (opened) await conn.CloseAsync(); }
        return result;
    }

    /// <summary>When each archived account was archived (for showing the remaining grace period).</summary>
    public async Task<Dictionary<Guid, DateTimeOffset>> ArchivedAtAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, DateTimeOffset>();
        var conn = db.Database.GetDbConnection();
        var opened = await OpenAsync(conn, ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"AccountId\", \"ArchivedAt\" FROM \"ArchivedAccounts\"";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                if (Guid.TryParse(reader.GetString(0), out var id))
                    result[id] = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
        }
        finally { if (opened) await conn.CloseAsync(); }
        return result;
    }

    /// <summary>Hard-delete accounts whose grace period has elapsed. Safe to call at startup.</summary>
    public async Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);
        var expired = (await ArchivedAtAsync(ct)).Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
        if (expired.Count == 0) return;

        foreach (var id in expired)
        {
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (account is not null) db.Accounts.Remove(account);   // cascades to the account's owned rows
            await UnarchiveAsync(id, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task WriteAsync(string sql, Action<System.Data.Common.DbCommand> bind, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var opened = await OpenAsync(conn, ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            bind(cmd);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    private static async Task<bool> OpenAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        if (conn.State == ConnectionState.Open) return false;
        await conn.OpenAsync(ct);
        return true;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
