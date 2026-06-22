using FinApp.Domain.Common;

namespace FinApp.Domain.Accounts;

/// <summary>A user who participates in an account: contributes money and receives reminders/notifications.</summary>
public sealed class AccountMember : Entity
{
    public Guid UserId { get; }
    public string DisplayName { get; private set; }

    public AccountMember(Guid userId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        UserId = userId;
        DisplayName = displayName.Trim();
    }
}
