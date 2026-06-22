using FinApp.Domain.Common;

namespace FinApp.Domain.Funds;

/// <summary>
/// A place money physically lives (Bank, Cash, a digital wallet…). Account-level and user-managed:
/// stored flat on the <c>Account</c> and referenced by id from expenses, opening balances and transfers
/// — the same pattern as budget categories. Replaces the old fixed <c>FundType</c> enum.
///
/// A fund may be nested under a parent via <see cref="ParentId"/>. Sub-funds are purely informational
/// labels for grouping (e.g. "Bank" → "Joint", "Personal"); all money lives on top-level funds and every
/// balance calculation stays on the parent, so sub-funds never carry their own balance.
/// </summary>
public sealed class Fund : Entity
{
    public string Name { get; private set; }

    /// <summary>The parent fund this is nested under, or null for a top-level fund.</summary>
    public Guid? ParentId { get; private set; }

    public Fund(string name, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Fund name is required.", nameof(name));
        Name = name.Trim();
        ParentId = parentId;
    }

    public bool IsRoot => ParentId is null;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Fund name is required.", nameof(name));
        Name = name.Trim();
    }
}
