namespace FinApp.Contracts;

/// <summary>Invite a user to a domain account by username. Any existing contributor may send one.</summary>
public record CreateInvitationRequest(string Username);

/// <summary>A pending/closed invitation as shown to the invitee in their session.</summary>
public record InvitationDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid InvitedByUserId,
    string InvitedByUsername,
    string Status,
    DateTimeOffset CreatedAt);
