using FinApp.Domain.Common;

namespace FinApp.Domain.Funds;

/// <summary>
/// A dated movement of money from one fund to another within a period. Total-preserving by
/// construction (it only changes where money sits, never how much), so it never affects a period's
/// closing balance or reconciliation — it only shifts the per-fund positions.
/// </summary>
public sealed class FundTransfer : Entity
{
    public Guid FromFundId { get; }
    public Guid ToFundId { get; }
    public Money Amount { get; }
    public DateOnly Date { get; }
    public string? Note { get; }

    public FundTransfer(Guid fromFundId, Guid toFundId, Money amount, DateOnly date, string? note = null)
    {
        if (fromFundId == toFundId)
            throw new ArgumentException("A transfer must be between two different funds.", nameof(toFundId));
        if (amount.IsNegative || amount.IsZero)
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        FromFundId = fromFundId;
        ToFundId = toFundId;
        Amount = amount;
        Date = date;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
