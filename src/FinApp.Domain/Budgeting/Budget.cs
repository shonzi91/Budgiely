using FinApp.Domain.Common;

namespace FinApp.Domain.Budgeting;

/// <summary>
/// The amount allocated to a category for one period. Expenses in that category (and its
/// sub-categories) consume the budget; coverage and alerts are computed against <see cref="Allocated"/>.
/// </summary>
public sealed class Budget : Entity
{
    public Guid CategoryId { get; }
    public Money Allocated { get; private set; }

    /// <summary>Fraction (0..1) of the budget at which to raise an alert, e.g. 0.80 = warn at 80%.</summary>
    public decimal AlertThreshold { get; private set; }

    /// <summary>If true, notify members on every expense; otherwise only when the threshold is crossed.</summary>
    public bool NotifyOnEveryExpense { get; private set; }

    public Budget(Guid categoryId, Money allocated, decimal alertThreshold = 0.80m, bool notifyOnEveryExpense = false)
    {
        if (allocated.IsNegative)
            throw new ArgumentException("Allocated amount cannot be negative.", nameof(allocated));
        if (alertThreshold is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(alertThreshold), "Threshold must be between 0 and 1.");
        CategoryId = categoryId;
        Allocated = allocated;
        AlertThreshold = alertThreshold;
        NotifyOnEveryExpense = notifyOnEveryExpense;
    }

    public void SetAllocation(Money allocated)
    {
        if (allocated.IsNegative)
            throw new ArgumentException("Allocated amount cannot be negative.", nameof(allocated));
        Allocated = allocated;
    }

    public void Configure(decimal alertThreshold, bool notifyOnEveryExpense)
    {
        if (alertThreshold is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(alertThreshold), "Threshold must be between 0 and 1.");
        AlertThreshold = alertThreshold;
        NotifyOnEveryExpense = notifyOnEveryExpense;
    }
}
