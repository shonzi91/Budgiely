using System.Linq;
using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Funds;
using FinApp.Domain.Periods;
using Xunit;

namespace FinApp.Domain.Tests;

/// <summary>
/// A fund marked <see cref="Fund.IsSynced"/> mirrors a real bank balance, so entries created while it is
/// synced carry a per-entry marker that keeps them out of the fund's balance math (cases 4.1–4.4). Markers are
/// baked at creation, so toggling the flag never rewrites history and unsynced funds behave exactly as before.
/// </summary>
public class SyncedFundTests
{
    private const string Eur = "EUR";
    private static Money M(decimal v) => new(v, Eur);

    private static Account Acc(out Period period)
    {
        var account = new Account("Home", Eur);
        account.AddDefaultFunds();
        period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        return account;
    }

    private static Fund FundOf(Account a, string name) => a.Funds.First(f => f.Id == a.FundId(name));

    // 4.1 / 4.2 — an expense paid from a synced fund is logged but doesn't reduce the fund.
    [Fact]
    public void Expense_on_synced_fund_does_not_reduce_its_balance()
    {
        var a = Acc(out var p);
        var bank = a.FundId("Bank");
        p.SetInitialBalance(bank, M(1000));
        FundOf(a, "Bank").SetSynced(true);

        var e = new Expense(Guid.NewGuid(), M(40), new DateOnly(2026, 1, 4), Guid.NewGuid(), bank);
        e.SetFundSynced(true);
        p.AddExpense(e);

        Assert.Equal(M(1000), p.FundBalance(bank));   // untouched — real bank balance is authoritative
        Assert.Equal(M(40), p.ExpensesTotal);          // but the spend is still recorded
    }

    [Fact]
    public void Expense_on_unsynced_fund_still_reduces_it()
    {
        var a = Acc(out var p);
        var bank = a.FundId("Bank");
        p.SetInitialBalance(bank, M(1000));

        p.AddExpense(new Expense(Guid.NewGuid(), M(40), new DateOnly(2026, 1, 4), Guid.NewGuid(), bank));

        Assert.Equal(M(960), p.FundBalance(bank));
    }

    // 4.3 / 4.4 — a transfer moves only the unsynced side; the synced side is left to its real balance.
    [Fact]
    public void Transfer_from_synced_moves_only_the_unsynced_destination()
    {
        var a = Acc(out var p);
        var bank = a.FundId("Bank");
        var cash = a.FundId("Cash");
        p.SetInitialBalance(bank, M(1000));
        p.SetInitialBalance(cash, M(100));
        FundOf(a, "Bank").SetSynced(true);

        var t = p.TransferFunds(bank, cash, M(250), new DateOnly(2026, 1, 5));
        t.SetSyncedSides(fromSynced: true, toSynced: false);

        Assert.Equal(M(1000), p.FundBalance(bank));   // synced source unchanged
        Assert.Equal(M(350), p.FundBalance(cash));    // unsynced destination increased
    }

    [Fact]
    public void Transfer_to_synced_moves_only_the_unsynced_source()
    {
        var a = Acc(out var p);
        var bank = a.FundId("Bank");
        var cash = a.FundId("Cash");
        p.SetInitialBalance(bank, M(1000));
        p.SetInitialBalance(cash, M(100));
        FundOf(a, "Cash").SetSynced(true);

        var t = p.TransferFunds(bank, cash, M(250), new DateOnly(2026, 1, 5));
        t.SetSyncedSides(fromSynced: false, toSynced: true);

        Assert.Equal(M(750), p.FundBalance(bank));    // unsynced source decreased
        Assert.Equal(M(100), p.FundBalance(cash));    // synced destination unchanged
    }

    [Fact]
    public void Deposit_into_synced_fund_does_not_increase_it()
    {
        var a = Acc(out var p);
        var owner = Guid.NewGuid();
        var bank = a.FundId("Bank");
        p.SetInitialBalance(bank, M(1000));
        FundOf(a, "Bank").SetSynced(true);

        var c = p.Deposit(owner, M(500), fundId: bank, date: new DateOnly(2026, 1, 3));
        c.SetFundSynced(true);

        Assert.Equal(M(1000), p.FundBalance(bank));
    }

    // Rollback / history: toggling the flag afterwards must not change already-recorded balances.
    [Fact]
    public void Toggling_sync_later_does_not_change_existing_entries()
    {
        var a = Acc(out var p);
        var bank = a.FundId("Bank");
        p.SetInitialBalance(bank, M(1000));
        p.AddExpense(new Expense(Guid.NewGuid(), M(40), new DateOnly(2026, 1, 4), Guid.NewGuid(), bank));  // unsynced at creation
        Assert.Equal(M(960), p.FundBalance(bank));

        FundOf(a, "Bank").SetSynced(true);   // flip on now
        Assert.Equal(M(960), p.FundBalance(bank));   // the historical expense still counts
    }
}
