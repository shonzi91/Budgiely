using FinApp.Contracts;
using FinApp.Domain.Accounts;
using FinApp.Persistence;
using FinApp.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Accounts;

/// <summary>
/// Server-authoritative CRUD for domain accounts, scoped to the calling user. A user sees accounts they
/// are a contributor of (owned or joined). Rename/delete are owner-only; everything else inside an account
/// is editable by any contributor (enforced by the per-resource endpoints/snapshots).
/// </summary>
public sealed class AccountService(FinAppDbContext db)
{
    public async Task<List<AccountSummaryDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var accounts = await db.Accounts
            .Include(a => a.Members)
            .Where(a => a.Members.Any(m => m.UserId == userId))
            .ToListAsync(ct);

        return accounts.Select(a => ToSummary(a, userId)).ToList();
    }

    public async Task<AccountSummaryDto> CreateAsync(Guid userId, string displayName, CreateAccountRequest request, CancellationToken ct = default)
    {
        Account account;
        try
        {
            account = new Account(request.Name, request.Currency);
            account.AssignOwner(userId, string.IsNullOrWhiteSpace(displayName) ? "Me" : displayName);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return ToSummary(account, userId);
    }

    public async Task RenameAsync(Guid userId, Guid accountId, string name, CancellationToken ct = default)
    {
        var account = await LoadOwnedAsync(userId, accountId, ct);
        try
        {
            account.Rename(name);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        var account = await LoadOwnedAsync(userId, accountId, ct);
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Load an account the user can see, or 404; throw 403 unless they own it (rename/delete gate).</summary>
    private async Task<Account> LoadOwnedAsync(Guid userId, Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts
            .Include(a => a.Members)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null || !account.IsContributor(userId))
            throw new NotFoundException("Account not found.");
        if (!account.IsOwner(userId))
            throw new ForbiddenException("Only the account owner can do that.");

        return account;
    }

    private static AccountSummaryDto ToSummary(Account a, Guid userId) =>
        new(a.Id, a.Name, a.Currency, a.OwnerUserId, a.IsOwner(userId),
            a.Members.Select(m => new MemberDto(m.UserId, m.DisplayName)).ToList());
}
