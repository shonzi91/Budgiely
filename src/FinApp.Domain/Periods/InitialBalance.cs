using FinApp.Domain.Common;

namespace FinApp.Domain.Periods;

/// <summary>
/// The opening balance of a fund at the start of a period. When <see cref="Informative"/> is true the
/// balance belongs to a sub-fund — a purely informational breakdown of its parent that does NOT count
/// toward the period's real totals or reconciliation (only the parent's real balance does).
/// </summary>
public sealed class InitialBalance : Entity
{
    public Guid FundId { get; }
    public Money Amount { get; private set; }
    public bool Informative { get; private set; }

    public InitialBalance(Guid fundId, Money amount, bool informative = false)
    {
        FundId = fundId;
        Amount = amount;
        Informative = informative;
    }

    public void Set(Money amount) => Amount = amount;
    public void Set(Money amount, bool informative) { Amount = amount; Informative = informative; }
}
