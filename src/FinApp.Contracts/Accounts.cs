namespace FinApp.Contracts;

/// <summary>A contributor on a domain account (unified with the money-side "member").</summary>
public record MemberDto(Guid UserId, string DisplayName);

/// <summary>
/// Lightweight view of a domain account the caller can see (owns or was invited to). The full
/// budgeting graph travels as an <see cref="AccountSnapshot"/>; this is the list/header shape.
/// </summary>
public record AccountSummaryDto(
    Guid Id,
    string Name,
    string Currency,
    Guid OwnerUserId,
    bool IsOwner,
    IReadOnlyList<MemberDto> Members);

public record CreateAccountRequest(string Name, string Currency);
public record RenameAccountRequest(string Name);

/// <summary>
/// Opaque-friendly full snapshot of a domain account aggregate, exchanged when a contributor loads or
/// saves a shared account. <see cref="Payload"/> is the serialized aggregate; keeping it a single blob
/// lets a later milestone swap it for an end-to-end-encrypted ciphertext without changing the contract.
/// </summary>
public record AccountSnapshot(Guid Id, long Version, string Payload);

/// <summary>Push a locally-edited snapshot back to the server. <see cref="ExpectedVersion"/> enables optimistic concurrency.</summary>
public record SaveAccountRequest(string Payload, long ExpectedVersion);
