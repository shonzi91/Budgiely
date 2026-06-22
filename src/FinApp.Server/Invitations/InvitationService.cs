using FinApp.Contracts;
using FinApp.Domain.Sharing;
using FinApp.Persistence;
using FinApp.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Invitations;

/// <summary>
/// Creates and resolves invitations to share a domain account. Any existing contributor may invite a
/// user by username; the invitee confirms (accept/decline) in their own session. Accepting unifies them
/// in as a contributor (member).
/// </summary>
public sealed class InvitationService(FinAppDbContext db)
{
    public sealed record CreatedInvitation(Guid InvitationId, Guid AccountId, string AccountName, Guid InviteeUserId, string InviterUsername);

    public async Task<CreatedInvitation> CreateAsync(Guid callerId, Guid accountId, string username, CancellationToken ct = default)
    {
        var account = await db.Accounts.Include(a => a.Members).FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null || !account.IsContributor(callerId))
            throw new NotFoundException("Account not found.");

        var target = (username ?? "").Trim();
        if (target.Length == 0)
            throw new BadRequestException("A username is required.");

        var invitee = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == target.ToLower(), ct)
            ?? throw new NotFoundException($"No user named '{target}'.");

        if (account.IsContributor(invitee.Id))
            throw new ConflictException("That user is already a contributor.");
        if (await db.Invitations.AnyAsync(i => i.AccountId == accountId && i.InvitedUserId == invitee.Id && i.Status == InvitationStatus.Pending, ct))
            throw new ConflictException("That user already has a pending invitation.");

        var invitation = new Invitation(accountId, invitee.Id, callerId);
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var inviter = await db.Users.FindAsync([callerId], ct);
        return new CreatedInvitation(invitation.Id, account.Id, account.Name, invitee.Id, inviter?.Username ?? "");
    }

    public async Task<List<InvitationDto>> PendingForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var pending = await db.Invitations
            .Where(i => i.InvitedUserId == userId && i.Status == InvitationStatus.Pending)
            .ToListAsync(ct);
        if (pending.Count == 0) return [];

        var accountIds = pending.Select(i => i.AccountId).ToHashSet();
        var inviterIds = pending.Select(i => i.InvitedByUserId).ToHashSet();
        var accountNames = await db.Accounts.Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var inviterNames = await db.Users.Where(u => inviterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        return pending
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(
                i.Id, i.AccountId,
                accountNames.GetValueOrDefault(i.AccountId, "(removed)"),
                i.InvitedByUserId,
                inviterNames.GetValueOrDefault(i.InvitedByUserId, "(unknown)"),
                i.Status.ToString(),
                i.CreatedAt))
            .ToList();
    }

    /// <summary>Accept a pending invitation addressed to the caller; returns the now-shared account id.</summary>
    public async Task<Guid> AcceptAsync(Guid userId, Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadOwnPendingAsync(userId, invitationId, ct);
        var account = await db.Accounts.Include(a => a.Members).FirstOrDefaultAsync(a => a.Id == invitation.AccountId, ct)
            ?? throw new NotFoundException("Account no longer exists.");
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("User not found.");

        invitation.Accept();
        if (!account.IsContributor(userId))
            account.AddContributor(userId, user.Username);

        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task DeclineAsync(Guid userId, Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadOwnPendingAsync(userId, invitationId, ct);
        invitation.Decline();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Invitation> LoadOwnPendingAsync(Guid userId, Guid invitationId, CancellationToken ct)
    {
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct);
        // Hide existence from anyone but the invitee.
        if (invitation is null || invitation.InvitedUserId != userId)
            throw new NotFoundException("Invitation not found.");
        if (!invitation.IsPending)
            throw new ConflictException($"Invitation was already {invitation.Status.ToString().ToLowerInvariant()}.");
        return invitation;
    }
}
