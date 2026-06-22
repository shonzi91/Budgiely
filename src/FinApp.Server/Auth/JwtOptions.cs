namespace FinApp.Server.Auth;

/// <summary>JWT signing/validation settings, bound from the "Jwt" config section. Defaults are dev-only.</summary>
public sealed class JwtOptions
{
    public string Key { get; set; } = "dev-only-finapp-signing-key-change-me-in-production-please";
    public string Issuer { get; set; } = "FinApp";
    public string Audience { get; set; } = "FinApp";
    public int ExpiryHours { get; set; } = 24;
}
