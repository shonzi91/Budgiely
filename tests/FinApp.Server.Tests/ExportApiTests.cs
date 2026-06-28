using System.Net;
using System.Net.Http.Json;
using FinApp.Contracts;
using FinApp.Domain.Accounts;
using FinApp.Domain.Budgeting;
using FinApp.Domain.Common;

namespace FinApp.Server.Tests;

public class ExportApiTests : IClassFixture<FinAppServerFactory>
{
    private readonly FinAppServerFactory _factory;

    public ExportApiTests(FinAppServerFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_returns_a_real_xlsx_for_an_account_with_data()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("export_user");
        var account = (await (await client.PostAsJsonAsync("/accounts", new CreateAccountRequest("Exporters", "EUR")))
            .Content.ReadFromJsonAsync<AccountSummaryDto>())!;

        // Build a small aggregate (one period, a deposit + an expense) and save it as the snapshot.
        var agg = new Account("Exporters", "EUR");
        agg.AddDefaultFunds();
        var food = agg.AddCategory("Food");
        var member = agg.AddMember(Guid.NewGuid(), "A");
        var period = agg.StartPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        period.SetInitialBalance(agg.FundId("Bank"), new Money(500, "EUR"));
        period.Deposit(member.UserId, new Money(1000, "EUR"));
        period.AddExpense(new Expense(food.Id, new Money(50, "EUR"), new DateOnly(2026, 1, 5), member.UserId, agg.FundId("Bank")));

        await client.PutAsJsonAsync($"/accounts/{account.Id}/snapshot",
            new SaveAccountRequest(AccountSnapshotSerializer.Serialize(agg), 0));

        var resp = await client.GetAsync($"/accounts/{account.Id}/export");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'P', bytes[0]); // .xlsx is a zip — starts with "PK"
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Export_of_an_empty_account_is_rejected()
    {
        var (client, _) = await _factory.RegisterAndAuthAsync("export_empty");
        var account = (await (await client.PostAsJsonAsync("/accounts", new CreateAccountRequest("Empty", "EUR")))
            .Content.ReadFromJsonAsync<AccountSummaryDto>())!;

        var resp = await client.GetAsync($"/accounts/{account.Id}/export");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode); // no snapshot yet → nothing to export
    }
}
