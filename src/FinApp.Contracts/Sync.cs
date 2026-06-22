namespace FinApp.Contracts;

/// <summary>Well-known SignalR client method + sync event names exchanged over the hub.</summary>
public static class SyncEvents
{
    /// <summary>An account a contributor belongs to changed; clients should re-pull its snapshot.</summary>
    public const string AccountChanged = "AccountChanged";

    /// <summary>The current user received a new invitation; clients should refresh pending invites.</summary>
    public const string InvitationReceived = "InvitationReceived";
}

/// <summary>Relay signal that a shared account changed. Deliberately carries no account contents (privacy-first); receivers re-pull the snapshot.</summary>
public record AccountChangedEvent(Guid AccountId, long Version, Guid ChangedByUserId, DateTimeOffset At);

/// <summary>Relay signal that the current user has a new pending invitation.</summary>
public record InvitationReceivedEvent(Guid InvitationId, Guid AccountId, string AccountName, string InvitedByUsername);
