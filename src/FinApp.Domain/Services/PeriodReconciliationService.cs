using FinApp.Domain.Common;
using FinApp.Domain.Periods;

namespace FinApp.Domain.Services;

/// <summary>Outcome of comparing a closed period's expected carry-over against the next period's opening balance.</summary>
public sealed record ReconciliationResult(Money ExpectedInitial, Money ActualInitial)
{
    /// <summary>Actual − Expected. Positive = more money on hand than explained; negative = unexplained shortfall.</summary>
    public Money Discrepancy => ActualInitial - ExpectedInitial;

    public bool IsReconciled => Discrepancy.IsZero;
}

/// <summary>
/// Feature 4: validates that a new period's opening balance matches the previous period's
/// closing balance (opening + paid contributions − expenses). Discrepancies must be cleared
/// before the new period can accept contributions.
/// </summary>
public sealed class PeriodReconciliationService
{
    public ReconciliationResult Reconcile(Period previous, Period current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Currency != current.Currency)
            throw new InvalidOperationException("Cannot reconcile periods in different currencies.");

        return new ReconciliationResult(previous.ExpectedClosingBalance, current.InitialTotal);
    }

    /// <summary>True when <paramref name="current"/> may start accepting contributions — i.e. no prior period, or the prior period reconciles.</summary>
    public bool CanAcceptContributions(Period? previous, Period current)
    {
        if (previous is null) return true;
        return Reconcile(previous, current).IsReconciled;
    }
}
