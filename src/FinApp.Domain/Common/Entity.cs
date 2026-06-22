namespace FinApp.Domain.Common;

/// <summary>Base class for entities identified by a stable Guid (works well with offline-first id generation and event sync).</summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
