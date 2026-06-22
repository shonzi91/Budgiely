using FinApp.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace FinApp.Server.Sync;

/// <summary>Centralises the live-sync pushes so endpoints/services don't touch the hub directly.</summary>
public sealed class SyncNotifier(IHubContext<SyncHub> hub)
{
    /// <summary>Tell everyone on a shared account that it changed; they re-pull the latest snapshot.</summary>
    public Task AccountChangedAsync(Guid accountId, Guid changedByUserId, long version = 0) =>
        hub.Clients.Group(SyncHub.AccountGroup(accountId))
            .SendAsync(SyncEvents.AccountChanged,
                new AccountChangedEvent(accountId, version, changedByUserId, DateTimeOffset.UtcNow));

    /// <summary>Tell a user they have a new pending invitation.</summary>
    public Task InvitationReceivedAsync(Guid inviteeUserId, Guid invitationId, Guid accountId, string accountName, string invitedByUsername) =>
        hub.Clients.Group(SyncHub.UserGroup(inviteeUserId))
            .SendAsync(SyncEvents.InvitationReceived,
                new InvitationReceivedEvent(invitationId, accountId, accountName, invitedByUsername));
}
