using FinApp.Domain.Common;
using Xunit;

namespace FinApp.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Add_and_subtract_same_currency()
    {
        var a = new Money(10.50m, "EUR");
        var b = new Money(4.25m, "EUR");

        Assert.Equal(new Money(14.75m, "EUR"), a + b);
        Assert.Equal(new Money(6.25m, "EUR"), a - b);
    }

    [Fact]
    public void Rounds_to_two_decimals_banker_rounding()
    {
        Assert.Equal(2.46m, new Money(2.455m, "EUR").Amount); // 2.455 -> 2.46
        Assert.Equal(2.46m, new Money(2.465m, "EUR").Amount); // banker's: 2.465 -> 2.46
    }

    [Fact]
    public void Mixing_currencies_throws()
    {
        var eur = new Money(1m, "EUR");
        var usd = new Money(1m, "USD");

        Assert.Throws<InvalidOperationException>(() => eur + usd);
    }

    [Fact]
    public void RatioOf_returns_null_on_zero_denominator()
    {
        Assert.Null(new Money(5m, "EUR").RatioOf(Money.Zero("EUR")));
        Assert.Equal(0.5m, new Money(5m, "EUR").RatioOf(new Money(10m, "EUR")));
    }

    [Fact]
    public void Currency_is_normalised_to_upper()
    {
        Assert.Equal("EUR", new Money(1m, "eur").Currency);
    }
}
