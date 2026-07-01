using System.Data;
using FinApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Auth;

/// <summary>
/// Records which users were provisioned through an external sign-in provider (Google/Facebook) and never
/// chose a password. Kept in a standalone <c>ExternalIdentities</c> table created idempotently with
/// <c>CREATE TABLE IF NOT EXISTS</c> — same reasoning as <see cref="AvatarService"/>: the <c>Users</c> table
/// is built via <c>EnsureCreated</c> in prod, which never ALTERs it, so a new mapped column would be invisible
/// there. Raw ADO keeps the SQL working on both SQLite and Postgres. Used to hide the "change password" UI for
/// external users (they have an unusable random hash).
/// </summary>
public sealed class ExternalIdentityService(FinAppDbContext db)
{
    public Task EnsureSchemaAsync(CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"ExternalIdentities\" (\"UserId\" text PRIMARY KEY, \"Provider\" text NOT NULL)", ct);

    public async Task MarkAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        var opened = await OpenAsync(conn, ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO \"ExternalIdentities\" (\"UserId\", \"Provider\") VALUES (@uid, @prov) " +
                "ON CONFLICT (\"UserId\") DO UPDATE SET \"Provider\" = @prov";
            AddParam(cmd, "@uid", userId.ToString());
            AddParam(cmd, "@prov", provider);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    /// <summary>The provider a user signed up with, or null if they're a local (password) user.</summary>
    public async Task<string?> GetProviderAsync(Guid userId, CancellationToken ct = default)
    {
        var conn = db.Database.GetDbConnection();
        var opened = await OpenAsync(conn, ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Provider\" FROM \"ExternalIdentities\" WHERE \"UserId\" = @uid";
            AddParam(cmd, "@uid", userId.ToString());
            return (await cmd.ExecuteScalarAsync(ct)) as string;
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
