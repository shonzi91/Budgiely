using FinApp.Domain.Common;
using FinApp.Domain.Periods;

namespace FinApp.Domain.Budgeting;

/// <summary>
/// An immutable ledger entry: money spent in a category, from a fund, by a member, on a date.
/// Append-only — corrections are made by adding a reversing entry, which keeps multi-user
/// sync conflict-free and makes period reconciliation auditable.
/// When <see cref="SourceSavingCategoryId"/> is set, the expense was paid from a savings bucket
/// (a "saving → expense" conversion) and also draws down that saving earmark.
/// </summary>
public sealed class Expense : Entity
{
    public Guid CategoryId { get; }
    public Money Amount { get; }
    public DateOnly Date { get; }
    public Guid MemberId { get; }
    public Guid FundId { get; }
    public string? Note { get; }
    public Guid? SourceSavingCategoryId { get; }

    /// <summary>
    /// When true, this expense was paid here but is (partly or wholly) on behalf of another account, so it can
    /// later be settled — the user pushes a chosen amount of it onto another account as that account's expense,
    /// and this account records a matching reimbursement deposit. Purely a flag that surfaces the "settle" action.
    /// </summary>
    public bool OnBehalfOfOtherAccount { get; }

    public Expense(
        Guid categoryId,
        Money amount,
        DateOnly date,
        Guid memberId,
        Guid fundId,
        string? note = null,
        Guid? sourceSavingCategoryId = null,
        bool onBehalfOfOtherAccount = false)
    {
        if (amount.IsNegative)
            throw new ArgumentException("Expense amount cannot be negative.", nameof(amount));
        CategoryId = categoryId;
        Amount = amount;
        Date = date;
        MemberId = memberId;
        FundId = fundId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        SourceSavingCategoryId = sourceSavingCategoryId;
        OnBehalfOfOtherAccount = onBehalfOfOtherAccount;
    }

    public bool IsFromSavings => SourceSavingCategoryId is not null;
}
