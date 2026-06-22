using FinApp.Domain.Common;

namespace FinApp.Domain.Periods;

/// <summary>
/// A member's deposit into a period — how much they have actually put in. There are no pledges or
/// due dates: deposits are recorded directly as money comes in.
/// </summary>
public sealed class Contribution : Entity
{
    public Guid MemberId { get; }
    public Money Paid { get; private set; }

    public Contribution(Guid memberId, Money paid)
    {
        if (paid.IsNegative)
            throw new ArgumentException("Deposited amount cannot be negative.", nameof(paid));
        MemberId = memberId;
        Paid = paid;
    }

    public void RecordPayment(Money amount)
    {
        if (amount.IsNegative)
            throw new ArgumentException("Deposit cannot be negative.", nameof(amount));
        Paid += amount;
    }

    /// <summary>Overwrite the deposited total (used when a member edits or clears their deposit).</summary>
    public void SetPaid(Money amount)
    {
        if (amount.IsNegative)
            throw new ArgumentException("Deposited amount cannot be negative.", nameof(amount));
        Paid = amount;
    }
}
