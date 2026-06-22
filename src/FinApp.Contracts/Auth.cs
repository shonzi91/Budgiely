namespace FinApp.Contracts;

/// <summary>Sign-up payload. The server hashes <see cref="Password"/> and never stores it in plaintext.</summary>
public record RegisterRequest(string Username, string Email, string Password);

/// <summary>Sign-in payload; <see cref="UsernameOrEmail"/> accepts either identifier.</summary>
public record LoginRequest(string UsernameOrEmail, string Password);

/// <summary>Result of register/login: a bearer token plus the authenticated user's identity.</summary>
public record AuthResponse(string Token, Guid UserId, string Username, string Email, DateTimeOffset ExpiresAt);

/// <summary>A user as seen over the wire (never includes the password hash).</summary>
public record UserDto(Guid Id, string Username, string Email);
