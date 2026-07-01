using System.Net;
using System.Net.Http.Json;
using FinApp.Contracts;

namespace FinApp.Server.Tests;

/// <summary>
/// Bank sync is inert until GoCardless credentials are configured (like external sign-in). These tests run
/// against the default, unconfigured server, so they assert the feature reports itself off and that calls
/// needing the provider fail cleanly rather than crashing — and that the staging tables exist and enforce
/// access scoping regardless.
/// </summary>
public class BankSyncApiTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public BankSyncApiTests(FinAppServerFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid AccountId)> AccountAsync(string user)
    {
        var (client, _) = await _factory.RegisterAndAuthAsync(user);
        var created = await (await client.PostAsJsonAsync("/accounts",
            new CreateAccountRequest("Main", "GBP"))).Content.ReadFromJsonAsync<AccountSummaryDto>();
        return (client, created!.Id);
    }

    [Fact]
    public async Task Status_reports_disabled_when_provider_unconfigured()
    {
        var (client, accountId) = await AccountAsync("bank1");

        var status = await client.GetFromJsonAsync<BankSyncStatusDto>($"/accounts/{accountId}/bank/status");

        Assert.NotNull(status);
        Assert.False(status!.Enabled);
        Assert.False(status.Connected);
        Assert.Null(status.InstitutionName);
    }

    [Fact]
    public async Task Pending_is_empty_before_any_sync()
    {
        var (client, accountId) = await AccountAsync("bank2");

        var pending = await client.GetFromJsonAsync<List<PendingBankTransactionDto>>($"/accounts/{accountId}/bank/pending");

        Assert.NotNull(pending);
        Assert.Empty(pending!);
    }

    [Fact]
    public async Task Sync_without_a_linked_bank_is_rejected()
    {
        var (client, accountId) = await AccountAsync("bank3");

        var resp = await client.PostAsync($"/accounts/{accountId}/bank/sync", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Bank_endpoints_are_scoped_to_contributors()
    {
        var (_, accountId) = await AccountAsync("bankOwner");
        var (stranger, _) = await _factory.RegisterAndAuthAsync("bankStranger");

        var resp = await stranger.GetAsync($"/accounts/{accountId}/bank/status");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
