using System.Net;
using System.Net.Http.Json;
using FinApp.Contracts;

namespace FinApp.Server.Tests;

public class SnapshotApiTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public SnapshotApiTests(FinAppServerFactory factory) => _factory = factory;

    private static async Task<AccountSummaryDto> CreateAccount(HttpClient client, string name) =>
        (await (await client.PostAsJsonAsync("/accounts", new CreateAccountRequest(name, "EUR")))
            .Content.ReadFromJsonAsync<AccountSummaryDto>())!;

    [Fact]
    public async Task New_account_has_an_empty_versionless_snapshot()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("snap_new");
        var account = await CreateAccount(client, "Fresh");

        var snap = await client.GetFromJsonAsync<AccountSnapshot>($"/accounts/{account.Id}/snapshot");
        Assert.Equal(0, snap!.Version);
        Assert.Equal("", snap.Payload);
    }

    [Fact]
    public async Task Save_then_get_round_trips_payload_and_bumps_version()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("snap_rw");
        var account = await CreateAccount(client, "Data");

        var saved = await (await client.PutAsJsonAsync($"/accounts/{account.Id}/snapshot",
            new SaveAccountRequest("{\"hello\":\"world\"}", 0))).Content.ReadFromJsonAsync<AccountSnapshot>();
        Assert.Equal(1, saved!.Version);

        var got = await client.GetFromJsonAsync<AccountSnapshot>($"/accounts/{account.Id}/snapshot");
        Assert.Equal(1, got!.Version);
        Assert.Equal("{\"hello\":\"world\"}", got.Payload);
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_conflict()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("snap_conflict");
        var account = await CreateAccount(client, "Race");

        await client.PutAsJsonAsync($"/accounts/{account.Id}/snapshot", new SaveAccountRequest("v1", 0));

        // Sending the now-stale expected version 0 again must conflict.
        var stale = await client.PutAsJsonAsync($"/accounts/{account.Id}/snapshot", new SaveAccountRequest("v2", 0));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Correct version succeeds.
        var ok = await client.PutAsJsonAsync($"/accounts/{account.Id}/snapshot", new SaveAccountRequest("v2", 1));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Invited_contributor_can_read_and_write_the_snapshot_but_a_stranger_cannot()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("snap_alice");
        var (bob, _) = await _factory.RegisterAndAuthAsync("snap_bob");
        var (stranger, _) = await _factory.RegisterAndAuthAsync("snap_stranger");
        var account = await CreateAccount(alice, "Joint");

        await alice.PutAsJsonAsync($"/accounts/{account.Id}/snapshot", new SaveAccountRequest("owner-data", 0));

        // Bob joins.
        await alice.PostAsJsonAsync($"/accounts/{account.Id}/invitations", new CreateInvitationRequest("snap_bob"));
        var pending = await bob.GetFromJsonAsync<List<InvitationDto>>("/invitations/pending");
        await bob.PostAsync($"/invitations/{pending![0].Id}/accept", null);

        // Bob (contributor) reads and writes.
        var bobGot = await bob.GetFromJsonAsync<AccountSnapshot>($"/accounts/{account.Id}/snapshot");
        Assert.Equal("owner-data", bobGot!.Payload);
        var bobSave = await bob.PutAsJsonAsync($"/accounts/{account.Id}/snapshot", new SaveAccountRequest("bob-data", bobGot.Version));
        Assert.Equal(HttpStatusCode.OK, bobSave.StatusCode);

        // Stranger is refused (account invisible to them).
        var strangerGet = await stranger.GetAsync($"/accounts/{account.Id}/snapshot");
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
    }
}
