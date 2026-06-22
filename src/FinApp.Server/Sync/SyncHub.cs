using FinApp.Persistence;
using FinApp.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Sync;

/// <summary>
/// Live-sync hub. On connect a client joins its personal channel (for invitations) and a channel per
/// account it contributes to (for change relays). The server pushes signals only — never account
/// contents — so receivers re-pull, keeping the door open for end-to-end-encrypted snapshots later.
/// </summary>
[Authorize]
public sealed class SyncHub(FinAppDbContext db) : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId}";
    public static string AccountGroup(Guid accountId) => $"account:{accountId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.UserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        var accountIds = await db.Accounts
            .Where(a => a.Members.Any(m => m.UserId == userId))
            .Select(a => a.Id)
            .ToListAsync();
        foreach (var id in accountIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(id));

        await base.OnConnectedAsync();
    }

    /// <summary>Subscribe to an account joined after connecting (e.g. just-accepted invitation).</summary>
    public async Task Subscribe(Guid accountId)
    {
        var userId = Context.User!.UserId();
        var member = await db.Accounts.AnyAsync(a => a.Id == accountId && a.Members.Any(m => m.UserId == userId));
        if (member)
            await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));
    }
}
