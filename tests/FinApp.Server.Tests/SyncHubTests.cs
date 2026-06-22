using System.Net.Http.Json;
using FinApp.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinApp.Server.Tests;

public class SyncHubTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public SyncHubTests(FinAppServerFactory factory) => _factory = factory;

    private static async Task<AccountSummaryDto> CreateAccount(HttpClient client, string name) =>
        (await (await client.PostAsJsonAsync("/accounts", new CreateAccountRequest(name, "EUR")))
            .Content.ReadFromJsonAsync<AccountSummaryDto>())!;

    private static async Task<T> WaitFor<T>(TaskCompletionSource<T> tcs)
    {
        var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(done == tcs.Task, "Timed out waiting for a SignalR push.");
        return await tcs.Task;
    }

    [Fact]
    public async Task Invitee_receives_a_live_invitation_push()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("hub_alice");
        var (bob, bobAuth) = await _factory.RegisterAndAuthAsync("hub_bob");
        var account = await CreateAccount(alice, "LiveShare");

        await using var bobHub = _factory.CreateHubConnection(bobAuth.Token);
        var received = new TaskCompletionSource<InvitationReceivedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobHub.On<InvitationReceivedEvent>(SyncEvents.InvitationReceived, e => received.TrySetResult(e));
        await bobHub.StartAsync();

        await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("hub_bob"));

        var evt = await WaitFor(received);
        Assert.Equal(account.Id, evt.AccountId);
        Assert.Equal("LiveShare", evt.AccountName);
        Assert.Equal("hub_alice", evt.InvitedByUsername);
    }

    [Fact]
    public async Task Contributors_receive_a_live_account_changed_push()
    {
        var (alice, aliceAuth) = await _factory.RegisterAndAuthAsync("hub2_alice");
        var (bob, bobAuth) = await _factory.RegisterAndAuthAsync("hub2_bob");
        var account = await CreateAccount(alice, "Joint");

        // Bob joins the account first, then connects (so OnConnectedAsync subscribes him to its group).
        await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("hub2_bob"));
        var pending = await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending");
        await bob.PostAsync($"/invitations/{pending![0].Id}/accept", null);

        await using var bobHub = _factory.CreateHubConnection(bobAuth.Token);
        var changed = new TaskCompletionSource<AccountChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobHub.On<AccountChangedEvent>(SyncEvents.AccountChanged, e => changed.TrySetResult(e));
        await bobHub.StartAsync();
        // Deterministically confirm group membership before triggering the change (avoids the
        // OnConnectedAsync race where StartAsync returns before group adds complete).
        await bobHub.InvokeAsync("Subscribe", account.Id);

        await alice.PutAsJsonAsync($"/accounts/{account.Id}/name", new RenameAccountRequest("Joint Household"));

        var evt = await WaitFor(changed);
        Assert.Equal(account.Id, evt.AccountId);
        Assert.Equal(aliceAuth.UserId, evt.ChangedByUserId);
    }
}
