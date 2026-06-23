using FinApp.Domain.Common;

namespace FinApp.Domain.Periods;

/// <summary>
/// An account-level category for contributions/deposits (e.g. Salary, Vouchers, Rent share).
/// Lets income be classified by source. The automatic "From previous period" leftover is not a
/// contribution and keeps its own pseudo-category, so it's unaffected by these.
/// </summary>
public sealed class ContributionCategory : Entity
{
    public string Name { get; private set; }

    public ContributionCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contribution category name is required.", nameof(name));
        Name = name.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contribution category name is required.", nameof(name));
        Name = name.Trim();
    }
}
