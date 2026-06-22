using FinApp.Contracts;
using FinApp.Persistence;
using FinApp.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Accounts;

/// <summary>
/// Stores and serves the opaque full-account snapshot for shared accounts. Any contributor may read or
/// write it; writes use optimistic concurrency on <see cref="AccountSnapshotRow.Version"/> so concurrent
/// editors can't silently clobber each other. The payload is never interpreted here.
/// </summary>
public sealed class SnapshotService(FinAppDbContext db)
{
    public async Task<AccountSnapshot> GetAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        await EnsureContributorAsync(userId, accountId, ct);
        var row = await db.AccountSnapshots.FindAsync([accountId], ct);
        return new AccountSnapshot(accountId, row?.Version ?? 0, row?.Payload ?? "");
    }

    public async Task<long> SaveAsync(Guid userId, Guid accountId, SaveAccountRequest request, CancellationToken ct = default)
    {
        await EnsureContributorAsync(userId, accountId, ct);
        if (string.IsNullOrEmpty(request.Payload))
            throw new BadRequestException("Snapshot payload is required.");

        var row = await db.AccountSnapshots.FindAsync([accountId], ct);
        if (row is null)
        {
            if (request.ExpectedVersion != 0)
                throw new ConflictException("Snapshot is new (version 0).");
            row = new AccountSnapshotRow { AccountId = accountId, Version = 1, Payload = request.Payload, UpdatedAt = DateTimeOffset.UtcNow };
            db.AccountSnapshots.Add(row);
        }
        else
        {
            if (row.Version != request.ExpectedVersion)
                throw new ConflictException($"Snapshot is at version {row.Version}; you sent {request.ExpectedVersion}. Reload and retry.");
            row.Version++;
            row.Payload = request.Payload;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return row.Version;
    }

    private async Task EnsureContributorAsync(Guid userId, Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts.Include(a => a.Members).FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null || !account.IsContributor(userId))
            throw new NotFoundException("Account not found.");
    }
}
