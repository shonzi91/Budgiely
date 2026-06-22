using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Periods;
using Xunit;

namespace FinApp.Domain.Tests;

public class FundsTests
{
    private const string Eur = "EUR";
    private static Money M(decimal v) => new(v, Eur);

    private static Account AccountWithFunds(out Period period)
    {
        var account = new Account("Home", Eur);
        account.AddDefaultFunds();
        period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        return account;
    }

    [Fact]
    public void Default_funds_are_seeded_and_findable_by_name()
    {
        var account = AccountWithFunds(out _);
        Assert.Equal(4, account.Funds.Count);
        Assert.Equal("Bank", account.FundName(account.FundId("bank")));
    }

    [Fact]
    public void Transfer_moves_money_between_funds_without_changing_the_total()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var cash = account.FundId("Cash");
        period.SetInitialBalance(bank, M(1000));
        period.SetInitialBalance(cash, M(100));

        period.TransferFunds(bank, cash, M(250), new DateOnly(2026, 1, 5));

        Assert.Equal(M(750), period.FundBalance(bank));
        Assert.Equal(M(350), period.FundBalance(cash));
        Assert.Equal(M(1100), period.InitialTotal);            // total untouched
        Assert.Equal(M(1100), period.ExpectedClosingBalance);  // reconciliation unaffected
    }

    [Fact]
    public void Fund_balance_nets_opening_transfers_and_spending()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var cash = account.FundId("Cash");
        period.SetInitialBalance(bank, M(500));
        period.TransferFunds(bank, cash, M(100), new DateOnly(2026, 1, 3));
        period.AddExpense(new Expense(Guid.NewGuid(), M(40), new DateOnly(2026, 1, 4), Guid.NewGuid(), bank));

        Assert.Equal(M(360), period.FundBalance(bank)); // 500 - 100 transfer - 40 spend
        Assert.Equal(M(100), period.FundBalance(cash));
    }

    [Fact]
    public void Transfer_can_be_edited_and_removed()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var cash = account.FundId("Cash");
        period.SetInitialBalance(bank, M(1000));
        var t = period.TransferFunds(bank, cash, M(200), new DateOnly(2026, 1, 5));

        period.EditFundTransfer(t.Id, bank, cash, M(250), "topup");
        Assert.Equal(M(750), period.FundBalance(bank));   // 1000 - 250
        Assert.Equal(M(250), period.FundBalance(cash));
        Assert.Equal("topup", period.FundTransfers.Single().Note);
        Assert.Equal(new DateOnly(2026, 1, 5), period.FundTransfers.Single().Date); // original date kept

        period.RemoveFundTransfer(period.FundTransfers.Single().Id);
        Assert.Empty(period.FundTransfers);
        Assert.Equal(M(1000), period.FundBalance(bank));
    }

    [Fact]
    public void Cannot_transfer_to_the_same_fund()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        Assert.Throws<ArgumentException>(() => period.TransferFunds(bank, bank, M(10), new DateOnly(2026, 1, 2)));
    }

    [Fact]
    public void Fund_in_use_cannot_be_removed()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        period.AddExpense(new Expense(Guid.NewGuid(), M(10), new DateOnly(2026, 1, 5), Guid.NewGuid(), bank));

        Assert.NotNull(account.FundRemovalBlocker(bank));
        Assert.Throws<InvalidOperationException>(() => account.RemoveFund(bank));
    }

    [Fact]
    public void Unused_fund_can_be_renamed_and_removed()
    {
        var account = AccountWithFunds(out _);
        var other = account.FundId("Other");
        account.RenameFund(other, "Savings jar");
        Assert.Equal("Savings jar", account.FundName(other));

        account.RemoveFund(other);
        Assert.Null(account.FindFund(other));
        Assert.Equal(3, account.Funds.Count);
    }

    [Fact]
    public void Sub_fund_nests_under_a_parent_and_is_informational_only()
    {
        var account = AccountWithFunds(out _);
        var bank = account.FundId("Bank");

        var pocket = account.AddFund("Pocket", bank);
        Assert.False(pocket.IsRoot);
        Assert.Equal(bank, pocket.ParentId);
        Assert.Contains(pocket.Id, account.ChildFundsOf(bank).Select(f => f.Id));
        Assert.DoesNotContain(pocket.Id, account.RootFunds.Select(f => f.Id));
    }

    [Fact]
    public void Sub_fund_initial_value_is_informative_and_excluded_from_totals()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var pocket = account.AddFund("Pocket", bank);

        period.SetInitialBalance(bank, M(1000));
        period.SetInitialBalance(pocket.Id, M(400), informative: true);

        Assert.Equal(M(1000), period.InitialTotal);                 // informative sub-fund value excluded
        Assert.Equal(M(400), period.OpeningBalanceOf(pocket.Id));   // but still tracked for display
        Assert.Equal(M(1000), period.ExpectedClosingBalance);       // reconciliation sees only the real total
    }

    [Fact]
    public void Parent_with_sub_funds_cannot_be_removed_until_children_go()
    {
        var account = AccountWithFunds(out _);
        var bank = account.FundId("Bank");
        var pocket = account.AddFund("Pocket", bank);

        Assert.Equal("it has sub-funds", account.FundRemovalBlocker(bank));
        Assert.Throws<InvalidOperationException>(() => account.RemoveFund(bank));

        account.RemoveFund(pocket.Id); // a leaf sub-fund removes cleanly
        Assert.Null(account.FundRemovalBlocker(bank));
    }

    [Fact]
    public void Sub_funds_cannot_nest_more_than_one_level()
    {
        var account = AccountWithFunds(out _);
        var bank = account.FundId("Bank");
        var pocket = account.AddFund("Pocket", bank);
        Assert.Throws<InvalidOperationException>(() => account.AddFund("Deeper", pocket.Id));
    }

    [Fact]
    public void Removing_a_fund_moves_its_opening_balance_to_another_fund()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var cash = account.FundId("Cash");
        period.SetInitialBalance(bank, M(1000));
        period.SetInitialBalance(cash, M(200));

        Assert.True(account.FundHasOpeningBalance(bank));
        Assert.Null(account.FundRemovalBlocker(bank)); // opening balance is not a hard blocker

        account.RemoveFund(bank, moveOpeningBalancesTo: cash);

        Assert.Null(account.FindFund(bank));
        Assert.Equal(M(1200), period.FundBalance(cash));   // 200 + 1000 moved over
        Assert.Equal(M(1200), period.InitialTotal);         // total preserved → reconciliation unaffected
    }

    [Fact]
    public void Removing_a_fund_without_a_target_drops_its_opening_balance()
    {
        var account = AccountWithFunds(out var period);
        var bank = account.FundId("Bank");
        var cash = account.FundId("Cash");
        period.SetInitialBalance(bank, M(500));
        period.SetInitialBalance(cash, M(100));

        account.RemoveFund(bank); // no target → the 500 is discarded along with the fund

        Assert.Null(account.FindFund(bank));
        Assert.Equal(M(100), period.InitialTotal); // only cash's 100 remains
    }

    [Fact]
    public void The_only_fund_cannot_be_removed()
    {
        var account = new Account("Solo", Eur);
        var only = account.AddFund("Bank");
        account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal("it's the only fund", account.FundRemovalBlocker(only.Id));
        Assert.Throws<InvalidOperationException>(() => account.RemoveFund(only.Id));
    }
}
