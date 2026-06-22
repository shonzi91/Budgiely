namespace FinApp.Persistence;

/// <summary>
/// Server-side storage of a shared account's full aggregate as an opaque snapshot blob, keyed by account.
/// The server never parses <see cref="Payload"/> (it's produced by <see cref="AccountSnapshotSerializer"/>
/// on the client) — so this can later hold an end-to-end-encrypted ciphertext unchanged. <see cref="Version"/>
/// drives optimistic concurrency between contributors.
/// </summary>
public sealed class AccountSnapshotRow
{
    public Guid AccountId { get; set; }
    public string Payload { get; set; } = "";
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
