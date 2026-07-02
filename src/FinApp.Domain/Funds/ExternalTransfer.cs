using FinApp.Domain.Common;

namespace FinApp.Domain.Funds;

/// <summary>
/// Money leaving one account's fund for another account (e.g. Personal → Shared). On the source side it's a
/// real outflow — it reduces the fund's position and the period's closing balance (unlike a same-account
/// <see cref="FundTransfer"/>, which is total-preserving). On the destination side the money arrives as a
/// normal member contribution, recorded separately in that account's current period.
/// </summary>
public sealed class ExternalTransfer : Entity
{
    public Guid FundId { get; }
    public Money Amount { get; }
    public DateOnly Date { get; }

    /// <summary>The account the money was sent to (informational; the matching deposit lives in that account).</summary>
    public Guid? ToAccountId { get; }
    public string? Note { get; }

    /// <summary>True when the source fund was synced (bank-mirrored) at creation, so this outflow doesn't
    /// reduce the fund's balance (the real bank balance handles it). See <see cref="Fund.IsSynced"/>.</summary>
    public bool FundSynced { get; private set; }

    public ExternalTransfer(Guid fundId, Money amount, DateOnly date, Guid? toAccountId = null, string? note = null)
    {
        if (amount.IsNegative || amount.IsZero)
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        FundId = fundId;
        Amount = amount;
        Date = date;
        ToAccountId = toAccountId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void SetFundSynced(bool synced) => FundSynced = synced;
}
