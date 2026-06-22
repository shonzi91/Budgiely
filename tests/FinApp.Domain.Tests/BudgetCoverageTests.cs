using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Periods;
using FinApp.Domain.Services;
using Xunit;

namespace FinApp.Domain.Tests;

public class BudgetCoverageTests
{
    private const string Eur = "EUR";
    private static Money M(decimal v) => new(v, Eur);

    [Fact]
    public void Rolls_up_sub_category_expenses_into_parent_budget()
    {
        var account = new Account("Family", Eur);
        var member = account.AddMember(Guid.NewGuid(), "Stoyan");
        var kids = account.AddCategory("Kids");
        var kid1 = account.AddCategory("Kid1", kids.Id);

        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        period.AddBudget(kids.Id, M(200), alertThreshold: 0.80m);

        // Spend 100 directly on Kids, 80 on the Kid1 sub-category => 180 of 200.
        period.AddExpense(new Expense(kids.Id, M(100), new DateOnly(2026, 1, 5), member.UserId, Guid.NewGuid()));
        period.AddExpense(new Expense(kid1.Id, M(80), new DateOnly(2026, 1, 6), member.UserId, Guid.NewGuid()));

        var coverage = new BudgetCoverageService().ForCategory(account, period, kids.Id);

        Assert.Equal(M(180), coverage.Spent);
        Assert.Equal(M(20), coverage.Remaining);
        Assert.Equal(90, coverage.Percent);
        Assert.True(coverage.ThresholdReached);  // 90% >= 80%
        Assert.False(coverage.IsOverBudget);
    }

    [Fact]
    public void Flags_overspend()
    {
        var account = new Account("Personal", Eur);
        var member = account.AddMember(Guid.NewGuid(), "Stoyan");
        var food = account.AddCategory("Food");

        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        period.AddBudget(food.Id, M(100));
        period.AddExpense(new Expense(food.Id, M(130), new DateOnly(2026, 1, 5), member.UserId, Guid.NewGuid()));

        var coverage = new BudgetCoverageService().ForCategory(account, period, food.Id);

        Assert.True(coverage.IsOverBudget);
        Assert.Equal(M(-30), coverage.Remaining);
        Assert.Equal(130, coverage.Percent);
    }

    [Fact]
    public void Below_threshold_does_not_alert()
    {
        var account = new Account("Personal", Eur);
        var member = account.AddMember(Guid.NewGuid(), "Stoyan");
        var fun = account.AddCategory("Entertainment");

        var period = account.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        period.AddBudget(fun.Id, M(100), alertThreshold: 0.80m);
        period.AddExpense(new Expense(fun.Id, M(50), new DateOnly(2026, 1, 5), member.UserId, Guid.NewGuid()));

        var coverage = new BudgetCoverageService().ForCategory(account, period, fun.Id);

        Assert.False(coverage.ThresholdReached);
        Assert.Equal(50, coverage.Percent);
    }
}
