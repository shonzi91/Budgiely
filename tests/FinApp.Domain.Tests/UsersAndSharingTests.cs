using FinApp.Domain.Accounts;
using FinApp.Domain.Sharing;
using FinApp.Domain.Users;
using Xunit;

namespace FinApp.Domain.Tests;

public class UsersAndSharingTests
{
    // --- User -------------------------------------------------------------

    [Fact]
    public void User_normalizes_email_and_keeps_username()
    {
        var user = new User("Stoyan", "Stoyan@Example.COM", "hash");
        Assert.Equal("Stoyan", user.Username);
        Assert.Equal("stoyan@example.com", user.Email);
    }

    [Theory]
    [InlineData("", "a@b.com", "h")]
    [InlineData("user", "", "h")]
    [InlineData("user", "no-at-sign", "h")]
    [InlineData("user", "a@b.com", "")]
    public void User_requires_valid_fields(string username, string email, string hash) =>
        Assert.Throws<ArgumentException>(() => new User(username, email, hash));

    // --- Account ownership / contributors ---------------------------------

    private static Account NewAccount() => new("Family", "EUR");

    [Fact]
    public void Assign_owner_records_owner_and_adds_them_as_a_contributor()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();

        account.AssignOwner(owner, "Stoyan");

        Assert.True(account.IsOwner(owner));
        Assert.True(account.IsContributor(owner));
        Assert.Single(account.Members);
        Assert.Equal("Stoyan", account.Members[0].DisplayName);
    }

    [Fact]
    public void Owner_cannot_be_assigned_twice()
    {
        var account = NewAccount();
        account.AssignOwner(Guid.NewGuid(), "A");
        Assert.Throws<InvalidOperationException>(() => account.AssignOwner(Guid.NewGuid(), "B"));
    }

    [Fact]
    public void Contributor_is_a_member_but_not_the_owner()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        account.AssignOwner(owner, "Owner");

        account.AddContributor(invitee, "Invitee");

        Assert.True(account.IsContributor(invitee));
        Assert.False(account.IsOwner(invitee));
        Assert.Equal(2, account.Members.Count);
    }

    [Fact]
    public void Empty_user_is_never_owner_or_contributor()
    {
        var account = NewAccount();
        Assert.False(account.IsOwner(Guid.Empty));
        Assert.False(account.IsContributor(Guid.Empty));
    }

    [Fact]
    public void Remove_member_drops_a_contributor()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        account.AssignOwner(owner, "Owner");
        account.AddContributor(invitee, "Invitee");

        account.RemoveMember(invitee);

        Assert.False(account.IsContributor(invitee));
        Assert.Single(account.Members);
    }

    [Fact]
    public void Owner_cannot_be_removed_while_others_remain()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();
        account.AssignOwner(owner, "Owner");
        account.AddContributor(Guid.NewGuid(), "Invitee");

        Assert.Throws<InvalidOperationException>(() => account.RemoveMember(owner));
    }

    [Fact]
    public void Sole_owner_can_be_removed()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();
        account.AssignOwner(owner, "Owner");

        account.RemoveMember(owner);

        Assert.Empty(account.Members);
    }

    [Fact]
    public void Transfer_ownership_moves_owner_to_another_member()
    {
        var account = NewAccount();
        var owner = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        account.AssignOwner(owner, "Owner");
        account.AddContributor(invitee, "Invitee");

        account.TransferOwnership(invitee);

        Assert.True(account.IsOwner(invitee));
        Assert.False(account.IsOwner(owner));
        // After transfer the previous owner is an ordinary member and can now leave.
        account.RemoveMember(owner);
        Assert.False(account.IsContributor(owner));
    }

    [Fact]
    public void Cannot_transfer_ownership_to_a_non_member()
    {
        var account = NewAccount();
        account.AssignOwner(Guid.NewGuid(), "Owner");
        Assert.Throws<InvalidOperationException>(() => account.TransferOwnership(Guid.NewGuid()));
    }

    // --- Invitation state machine -----------------------------------------

    [Fact]
    public void New_invitation_is_pending()
    {
        var inv = new Invitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.True(inv.IsPending);
        Assert.Equal(InvitationStatus.Pending, inv.Status);
        Assert.Null(inv.RespondedAt);
    }

    [Fact]
    public void Cannot_invite_yourself()
    {
        var me = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Invitation(Guid.NewGuid(), me, me));
    }

    [Fact]
    public void Accept_resolves_once_and_stamps_time()
    {
        var inv = new Invitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        inv.Accept();
        Assert.Equal(InvitationStatus.Accepted, inv.Status);
        Assert.NotNull(inv.RespondedAt);
        Assert.Throws<InvalidOperationException>(() => inv.Accept());
        Assert.Throws<InvalidOperationException>(() => inv.Decline());
    }

    [Fact]
    public void Declined_invitation_cannot_be_accepted()
    {
        var inv = new Invitation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        inv.Decline();
        Assert.Equal(InvitationStatus.Declined, inv.Status);
        Assert.Throws<InvalidOperationException>(() => inv.Accept());
    }
}
