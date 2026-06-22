using FinApp.Domain.Accounts;
using FinApp.Domain.Common;
using FinApp.Domain.Periods;

namespace FinApp.Domain.Services;

/// <summary>How much of a budget is used, for charts and threshold alerts (feature 6).</summary>
public sealed record BudgetCoverage(Money Allocated, Money Spent, decimal AlertThreshold)
{
    public Money Remaining => Allocated - Spent;
    public bool IsOverBudget => Spent > Allocated;

    /// <summary>Fraction spent (0..1+). Null when nothing is allocated (avoids divide-by-zero).</summary>
    public decimal? Ratio => Spent.RatioOf(Allocated);

    /// <summary>Percent spent rounded to whole numbers, or null when nothing is allocated.</summary>
    public int? Percent => Ratio is { } r ? (int)decimal.Round(r * 100m, 0, MidpointRounding.AwayFromZero) : null;

    /// <summary>True once spending reaches the configured alert threshold (or any overspend on a zero budget).</summary>
    public bool ThresholdReached => Ratio is { } r ? r >= AlertThreshold : IsOverBudget;
}

/// <summary>
/// Computes budget usage by rolling up every expense in a category and its sub-categories
/// against the period's allocation for that category.
/// </summary>
public sealed class BudgetCoverageService
{
    public BudgetCoverage ForCategory(Account account, Period period, Guid categoryId)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(period);

        var budget = period.FindBudget(categoryId)
            ?? throw new InvalidOperationException("No budget exists for this category in the period.");

        if (account.FindCategory(categoryId) is null)
            throw new InvalidOperationException("Category not found in the account.");

        var categoryIds = account.CategoryWithDescendantIds(categoryId).ToHashSet();

        var spent = period.Expenses
            .Where(e => categoryIds.Contains(e.CategoryId))
            .Select(e => e.Amount)
            .Aggregate(Money.Zero(period.Currency), (acc, m) => acc + m);

        return new BudgetCoverage(budget.Allocated, spent, budget.AlertThreshold);
    }
}
