using FinApp.Domain.Common;

namespace FinApp.Domain.Sharing;

public enum InvitationStatus { Pending, Accepted, Declined }

/// <summary>
/// An offer to join a domain account as a contributor. Created by an existing contributor (any of them)
/// for a target user; the target confirms in their own session. Accepting adds the invitee to the
/// account's members (a contributor); declining closes it. The state machine only moves out of
/// <see cref="InvitationStatus.Pending"/> once.
/// </summary>
public sealed class Invitation : Entity
{
    public Guid AccountId { get; }
    public Guid InvitedUserId { get; }
    public Guid InvitedByUserId { get; }
    public InvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public Invitation(Guid accountId, Guid invitedUserId, Guid invitedByUserId, DateTimeOffset createdAt = default)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account is required.", nameof(accountId));
        if (invitedUserId == Guid.Empty) throw new ArgumentException("Invited user is required.", nameof(invitedUserId));
        if (invitedByUserId == Guid.Empty) throw new ArgumentException("Inviting user is required.", nameof(invitedByUserId));
        if (invitedUserId == invitedByUserId) throw new ArgumentException("You cannot invite yourself.");

        AccountId = accountId;
        InvitedUserId = invitedUserId;
        InvitedByUserId = invitedByUserId;
        Status = InvitationStatus.Pending;
        CreatedAt = createdAt == default ? DateTimeOffset.UtcNow : createdAt;
    }

    public bool IsPending => Status == InvitationStatus.Pending;

    public void Accept(DateTimeOffset? at = null) => Resolve(InvitationStatus.Accepted, at);
    public void Decline(DateTimeOffset? at = null) => Resolve(InvitationStatus.Declined, at);

    private void Resolve(InvitationStatus status, DateTimeOffset? at)
    {
        if (!IsPending)
            throw new InvalidOperationException($"Invitation has already been {Status.ToString().ToLowerInvariant()}.");
        Status = status;
        RespondedAt = at ?? DateTimeOffset.UtcNow;
    }
}
