using System.Globalization;
using FinApp.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinApp.Persistence;

/// <summary>
/// Stores <see cref="Money"/> as a single text column ("amount|CURRENCY"). Mapping Money to one
/// scalar column (rather than an owned type) keeps every entity constructor-bindable by EF.
/// </summary>
public sealed class MoneyConverter : ValueConverter<Money, string>
{
    public MoneyConverter() : base(m => Serialize(m), s => Deserialize(s)) { }

    private static string Serialize(Money m) =>
        $"{m.Amount.ToString(CultureInfo.InvariantCulture)}|{m.Currency}";

    private static Money Deserialize(string value)
    {
        var sep = value.IndexOf('|');
        var amount = decimal.Parse(value[..sep], CultureInfo.InvariantCulture);
        var currency = value[(sep + 1)..];
        return new Money(amount, currency);
    }
}
