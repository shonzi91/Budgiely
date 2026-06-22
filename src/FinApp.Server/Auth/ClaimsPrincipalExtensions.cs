using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinApp.Server.Infrastructure;

namespace FinApp.Server.Auth;

/// <summary>Reads FinApp identity claims off the authenticated principal (inbound claim mapping is disabled, so names are raw).</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedException("Token is missing a valid user id.");
    }

    public static string Username(this ClaimsPrincipal user) =>
        user.FindFirstValue("username") ?? "";

    public static string Email(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email) ?? "";
}
