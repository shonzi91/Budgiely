using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;
using FinApp.Domain.Periods;

namespace FinApp.Domain.Services;

/// <summary>
/// Feature: move the unspent part of a budget (allocated − actual expense) into a savings bucket
/// or another budget. Moves are capped at the source budget's leftover so a budget can never be
/// cut below what's already been spent.
/// </summary>
public sealed class BudgetReallocationService
{
    private readonly BudgetCoverageService _coverage = new();

    /// <summary>Unspent amount of a budget (can be negative when overspent).</summary>
    public Money Leftover(Account account, Period period, Guid categoryId) =>
        _coverage.ForCategory(account, period, categoryId).Remaining;

    public void ToSavings(Account account, Period period, Guid sourceCategoryId, Guid savingCategoryId, Money amount, DateOnly date)
    {
        var source = Validate(account, period, sourceCategoryId, amount);
        // Reduce the budget first so the savings headroom (contributed − budgeted) opens up for this move.
        source.SetAllocation(source.Allocated - amount);
        period.AllocateToSavings(savingCategoryId, amount, date, "Reallocated from budget");
    }

    public void ToBudget(Account account, Period period, Guid sourceCategoryId, Guid targetCategoryId, Money amount)
    {
        if (sourceCategoryId == targetCategoryId)
            throw new InvalidOperationException("Source and target budgets must differ.");
        var source = Validate(account, period, sourceCategoryId, amount);
        var target = period.FindBudget(targetCategoryId)
            ?? throw new InvalidOperationException("No budget exists for the target category.");

        source.SetAllocation(source.Allocated - amount);
        target.SetAllocation(target.Allocated + amount);
    }

    private Budget Validate(Account account, Period period, Guid sourceCategoryId, Money amount)
    {
        if (amount.IsNegative || amount.IsZero)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        var source = period.FindBudget(sourceCategoryId)
            ?? throw new InvalidOperationException("No budget exists for the source category.");

        var leftover = Leftover(account, period, sourceCategoryId);
        if (amount > leftover)
            throw new InvalidOperationException($"Only {leftover} is unspent in that budget.");

        return source;
    }
}
