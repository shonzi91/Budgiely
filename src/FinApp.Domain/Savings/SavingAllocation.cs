using FinApp.Domain.Common;

namespace FinApp.Domain.Savings;

/// <summary>
/// Money set aside into a savings bucket during a period. Positive amounts add to savings;
/// a saving→expense conversion records a matching negative drawdown so the running balance stays correct.
/// </summary>
public sealed class SavingAllocation : Entity
{
    public Guid SavingCategoryId { get; }
    public Money Amount { get; }
    public DateOnly Date { get; }
    public string? Note { get; }

    /// <summary>When this allocation is the drawdown half of a saving→expense conversion, the id of that expense — so removing/editing the expense can cancel the matching drawdown exactly.</summary>
    public Guid? SourceExpenseId { get; }

    /// <summary>When this is a "move savings to a budget" drawdown, the category whose budget it funded — so the move can be reversed (the budget reduced) on remove/edit.</summary>
    public Guid? BudgetCategoryId { get; }

    /// <summary>Shared id linking the two halves (out + in) of a bucket-to-bucket transfer, so the pair can be removed/edited as one movement.</summary>
    public Guid? TransferPairId { get; }

    public SavingAllocation(Guid savingCategoryId, Money amount, DateOnly date, string? note = null,
        Guid? sourceExpenseId = null, Guid? budgetCategoryId = null, Guid? transferPairId = null)
    {
        SavingCategoryId = savingCategoryId;
        Amount = amount;
        Date = date;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        SourceExpenseId = sourceExpenseId;
        BudgetCategoryId = budgetCategoryId;
        TransferPairId = transferPairId;
    }
}
