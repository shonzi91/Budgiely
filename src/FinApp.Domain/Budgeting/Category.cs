using FinApp.Domain.Common;

namespace FinApp.Domain.Budgeting;

/// <summary>
/// A budget/expense category (Food, Bills, Car...). Categories form a tree via <see cref="ParentId"/>,
/// but are stored flat on the <c>Account</c> (which owns tree navigation). Flat storage round-trips
/// cleanly through the relational store. Categories are reused across periods.
/// </summary>
public sealed class Category : Entity
{
    public string Name { get; private set; }
    public Guid? ParentId { get; private set; }

    public Category(string name, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));
        Name = name.Trim();
        ParentId = parentId;
    }

    public bool IsRoot => ParentId is null;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));
        Name = name.Trim();
    }
}
