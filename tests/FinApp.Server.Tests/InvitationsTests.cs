using System.Net;
using System.Net.Http.Json;
using FinApp.Contracts;

namespace FinApp.Server.Tests;

public class InvitationsTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public InvitationsTests(FinAppServerFactory factory) => _factory = factory;

    private static async Task<AccountSummaryDto> CreateAccount(HttpClient client, string name) =>
        (await (await client.PostAsJsonAsync("/accounts", new CreateAccountRequest(name, "EUR")))
            .Content.ReadFromJsonAsync<AccountSummaryDto>())!;

    [Fact]
    public async Task Invite_then_accept_shares_the_account_with_the_invitee()
    {
        var (alice, aliceAuth) = await _factory.RegisterAndAuthAsync("inv_alice");
        var (bob, bobAuth) = await _factory.RegisterAndAuthAsync("inv_bob");
        var account = await CreateAccount(alice, "Holiday");

        var invite = await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("inv_bob"));
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var pending = await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending");
        Assert.Single(pending!);
        Assert.Equal("Holiday", pending![0].AccountName);
        Assert.Equal("inv_alice", pending[0].InvitedByUsername);

        var accept = await bob.PostAsync($"/invitations/{pending[0].Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var bobAccounts = await bob.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts");
        var shared = Assert.Single(bobAccounts!);
        Assert.Equal(account.Id, shared.Id);
        Assert.False(shared.IsOwner);
        Assert.Contains(shared.Members, m => m.UserId == aliceAuth.UserId);
        Assert.Contains(shared.Members, m => m.UserId == bobAuth.UserId);

        // Pending list is now empty.
        Assert.Empty((await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending"))!);
    }

    [Fact]
    public async Task Any_contributor_can_invite_but_only_the_owner_can_rename_or_delete()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("chain_alice");
        var (bob, _) = await _factory.RegisterAndAuthAsync("chain_bob");
        var (carol, _) = await _factory.RegisterAndAuthAsync("chain_carol");
        var account = await CreateAccount(alice, "Shared");

        // Alice invites Bob; Bob accepts.
        await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("chain_bob"));
        var bobPending = await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending");
        await bob.PostAsync($"/invitations/{bobPending![0].Id}/accept", null);

        // Bob (a contributor, not owner) can invite Carol.
        var bobInvitesCarol = await bob.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("chain_carol"));
        Assert.Equal(HttpStatusCode.OK, bobInvitesCarol.StatusCode);
        Assert.Single((await carol.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending"))!);

        // But Bob cannot rename or delete the account.
        var rename = await bob.PutAsJsonAsync($"/accounts/{account.Id}/name", new RenameAccountRequest("Bob's"));
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        var delete = await bob.DeleteAsync($"/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Decline_keeps_the_account_private()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("dec_alice");
        var (bob, _) = await _factory.RegisterAndAuthAsync("dec_bob");
        var account = await CreateAccount(alice, "Secret");

        await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("dec_bob"));
        var pending = await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending");

        var decline = await bob.PostAsync($"/invitations/{pending![0].Id}/decline", null);
        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);

        Assert.Empty((await bob.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts"))!);
        Assert.Empty((await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending"))!);
    }

    [Fact]
    public async Task Invite_rejects_strangers_unknown_users_and_duplicates()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("rej_alice");
        var (bob, _) = await _factory.RegisterAndAuthAsync("rej_bob");
        var (stranger, _) = await _factory.RegisterAndAuthAsync("rej_stranger");
        var account = await CreateAccount(alice, "Vault");

        // Stranger (not a contributor) can't see the account to invite into it.
        var byStranger = await stranger.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("rej_bob"));
        Assert.Equal(HttpStatusCode.NotFound, byStranger.StatusCode);

        // Unknown username.
        var unknown = await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("nobody"));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // Duplicate pending invite.
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("rej_bob"))).StatusCode);
        var dup = await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("rej_bob"));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }
}
