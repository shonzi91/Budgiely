namespace FinApp.Contracts;

/// <summary>Whether bank sync is configured server-side, and this account's current connection (if any).</summary>
public record BankSyncStatusDto(bool Enabled, bool Connected, string? InstitutionName, DateTimeOffset? ConsentExpiresAt, DateTimeOffset? LastSyncedAt);

/// <summary>A bank the aggregator knows about (Enable Banking identifies an ASPSP by name + country).</summary>
public record BankInstitutionDto(string Name, string Country);

/// <summary>Start linking an account to a bank. The server builds the consent return URL itself (like OAuth).</summary>
public record StartBankLinkRequest(string InstitutionName, string Country);

/// <summary>Where to send the browser to complete the bank's consent flow.</summary>
public record StartBankLinkResponse(string LinkUrl);

/// <summary>A bank transaction fetched but not yet turned into (or dismissed from becoming) a FinApp expense.</summary>
public record PendingBankTransactionDto(string ExternalId, decimal Amount, DateOnly Date, string Description);

/// <summary>Mark a staged transaction as handled: <see cref="Confirmed"/> = turned into an expense, else dismissed.</summary>
public record BankTransactionAck(string ExternalId, bool Confirmed);
