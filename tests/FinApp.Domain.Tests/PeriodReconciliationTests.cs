using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Periods;
using FinApp.Domain.Services;
using Xunit;

namespace FinApp.Domain.Tests;

public class PeriodReconciliationTests
{
    private const string Eur = "EUR";
    private static Money M(decimal v) => new(v, Eur);

    private static Period PreviousPeriod()
    {
        var p = new Period(Eur, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        p.SetInitialBalance(Guid.NewGuid(), M(100));
        var memberId = Guid.NewGuid();
        p.Deposit(memberId, M(50));
        p.AddExpense(new Expense(Guid.NewGuid(), M(30), new DateOnly(2026, 1, 10), memberId, Guid.NewGuid()));
        return p; // closing = 100 + 50 - 30 = 120
    }

    [Fact]
    public void Reconciles_when_opening_matches_previous_closing()
    {
        var prev = PreviousPeriod();
        var current = new Period(Eur, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        current.SetInitialBalance(Guid.NewGuid(), M(120));

        var result = new PeriodReconciliationService().Reconcile(prev, current);

        Assert.True(result.IsReconciled);
        Assert.Equal(M(120), result.ExpectedInitial);
        Assert.True(result.Discrepancy.IsZero);
    }

    [Fact]
    public void Reports_shortfall_as_negative_discrepancy()
    {
        var prev = PreviousPeriod();
        var current = new Period(Eur, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        current.SetInitialBalance(Guid.NewGuid(), M(100)); // 20 missing

        var result = new PeriodReconciliationService().Reconcile(prev, current);

        Assert.False(result.IsReconciled);
        Assert.Equal(M(-20), result.Discrepancy);
    }

    [Fact]
    public void First_ever_period_can_always_accept_contributions()
    {
        var current = new Period(Eur, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.True(new PeriodReconciliationService().CanAcceptContributions(previous: null, current));
    }

    [Fact]
    public void Cannot_accept_contributions_until_previous_reconciled()
    {
        var prev = PreviousPeriod();
        var current = new Period(Eur, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        current.SetInitialBalance(Guid.NewGuid(), M(90)); // discrepancy

        Assert.False(new PeriodReconciliationService().CanAcceptContributions(prev, current));
    }
}
