using FinApp.Domain.Accounts;
using FinApp.Domain.Common;
using Xunit;

namespace FinApp.Domain.Tests;

public class ContributionsTests
{
    private const string Eur = "EUR";
    private static Money M(decimal v) => new(v, Eur);

    [Fact]
    public void Contribution_categories_reject_dupes_and_block_removal_when_referenced()
    {
        var account = new Account("Home", Eur);
        account.AddDefaultFunds();
        var member = account.AddMember(Guid.NewGuid(), "A").UserId;
        var salary = account.AddContributionCategory("Salary");
        Assert.Throws<InvalidOperationException>(() => account.AddContributionCategory(" salary "));

        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        period.Deposit(member, M(500), salary.Id, account.FundId("Bank"), new DateOnly(2026, 1, 5));

        Assert.Equal("deposits reference it", account.ContributionCategoryRemovalBlocker(salary.Id));
        Assert.Throws<InvalidOperationException>(() => account.RemoveContributionCategory(salary.Id));
    }

    [Fact]
    public void Deposit_attributed_to_a_fund_raises_that_fund_balance()
    {
        var account = new Account("Home", Eur);
        account.AddDefaultFunds();
        var member = account.AddMember(Guid.NewGuid(), "A").UserId;
        var salary = account.AddContributionCategory("Salary");
        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        period.Deposit(member, M(800), salary.Id, account.FundId("Bank"), new DateOnly(2026, 1, 3));

        Assert.Equal(M(800), period.FundBalance(account.FundId("Bank")));
        Assert.Equal(M(0), period.FundBalance(account.FundId("Cash")));
        Assert.Equal(M(800), period.ContributionsPaidTotal);
    }

    [Fact]
    public void Same_member_category_fund_merges_other_combos_are_separate()
    {
        var account = new Account("Home", Eur);
        account.AddDefaultFunds();
        var member = account.AddMember(Guid.NewGuid(), "A").UserId;
        var bank = account.FundId("Bank");
        var salary = account.AddContributionCategory("Salary");
        var vouchers = account.AddContributionCategory("Vouchers");
        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        period.Deposit(member, M(100), salary.Id, bank, default);
        period.Deposit(member, M(50), salary.Id, bank, default);    // merges into the Salary→Bank row
        period.Deposit(member, M(30), vouchers.Id, bank, default);  // different category → separate row

        Assert.Equal(2, period.Contributions.Count);
        Assert.Equal(M(150), period.Contributions.First(c => c.CategoryId == salary.Id).Paid);
        Assert.Equal(M(180), period.ContributionsPaidTotal);
    }
}
