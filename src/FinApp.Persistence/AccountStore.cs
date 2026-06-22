using FinApp.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Persistence;

/// <summary>
/// Loads and persists the account aggregate. The UI mutates the loaded aggregate through domain
/// methods and calls <see cref="SaveAsync"/>; EF change-tracking turns that into the right writes.
/// </summary>
public sealed class AccountStore(FinAppDbContext db)
{
    public FinAppDbContext Db => db;

    /// <summary>
    /// Bring the database schema up to date by applying any pending EF Core migrations
    /// (creating the file and schema on first run). Runs the migration DDL over the open,
    /// SQLCipher-keyed connection, so it works against the encrypted file unchanged.
    /// </summary>
    public void Migrate() => db.Database.Migrate();

    private IQueryable<Account> FullGraph() =>
        db.Accounts
            .Include(a => a.Members)
            .Include(a => a.Categories)
            .Include(a => a.SavingCategories)
            .Include(a => a.Funds)
            .Include(a => a.Periods).ThenInclude(p => p.InitialBalances)
            .Include(a => a.Periods).ThenInclude(p => p.Contributions)
            .Include(a => a.Periods).ThenInclude(p => p.Budgets)
            .Include(a => a.Periods).ThenInclude(p => p.Expenses)
            .Include(a => a.Periods).ThenInclude(p => p.SavingAllocations)
            .Include(a => a.Periods).ThenInclude(p => p.FundTransfers)
            .AsSplitQuery();

    /// <summary>Load the full aggregate (first account), or null if the database is empty.</summary>
    public Task<Account?> LoadFirstAsync(CancellationToken ct = default) => FullGraph().FirstOrDefaultAsync(ct);

    /// <summary>Load every account with its full graph.</summary>
    public async Task<List<Account>> LoadAllAsync(CancellationToken ct = default) =>
        await FullGraph().ToListAsync(ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Account account, CancellationToken ct = default)
    {
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
