using System.Net;
using System.Net.Http.Json;
using FinApp.Contracts;

namespace FinApp.Server.Tests;

public class AccountsApiTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public AccountsApiTests(FinAppServerFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_account_makes_caller_the_owner_and_a_contributor()
    {
        var (client, auth) = await _factory.RegisterAndAuthAsync("owner1");

        var created = await (await client.PostAsJsonAsync("/accounts",
            new CreateAccountRequest("Family", "EUR"))).Content.ReadFromJsonAsync<AccountSummaryDto>();

        Assert.NotNull(created);
        Assert.True(created!.IsOwner);
        Assert.Equal(auth.UserId, created.OwnerUserId);
        Assert.Contains(created.Members, m => m.UserId == auth.UserId);

        var list = await client.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts");
        Assert.Single(list!);
        Assert.Equal(created.Id, list![0].Id);
    }

    [Fact]
    public async Task Accounts_are_scoped_to_the_caller()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("scopeA");
        await alice.PostAsJsonAsync("/accounts", new CreateAccountRequest("A's money", "USD"));

        var (bob, _) = await _factory.RegisterAndAuthAsync("scopeB");
        var bobList = await bob.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts");

        Assert.Empty(bobList!);
    }

    [Fact]
    public async Task Owner_can_rename_and_delete()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("owner2");
        var created = await (await client.PostAsJsonAsync("/accounts",
            new CreateAccountRequest("Shared", "EUR"))).Content.ReadFromJsonAsync<AccountSummaryDto>();

        var rename = await client.PutAsJsonAsync($"/accounts/{created!.Id}/name", new RenameAccountRequest("Household"));
        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);

        var afterRename = await client.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts");
        Assert.Equal("Household", afterRename!.Single().Name);

        var delete = await client.DeleteAsync($"/accounts/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<List<AccountSummaryDto>>("/accounts");
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task A_stranger_cannot_see_rename_or_delete_anothers_account()
    {
        var (alice, _) = await _factory.RegisterAndAuthAsync("strangerA");
        var created = await (await alice.PostAsJsonAsync("/accounts",
            new CreateAccountRequest("Private", "EUR"))).Content.ReadFromJsonAsync<AccountSummaryDto>();

        var (bob, _) = await _factory.RegisterAndAuthAsync("strangerB");

        var rename = await bob.PutAsJsonAsync($"/accounts/{created!.Id}/name", new RenameAccountRequest("Hacked"));
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);

        var delete = await bob.DeleteAsync($"/accounts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
