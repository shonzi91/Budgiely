using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinApp.Contracts;
using FinApp.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinApp.Server.Auth;

/// <summary>Issues signed JWT bearer tokens carrying the user's id, username and email.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public AuthResponse Issue(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(_options.ExpiryHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthResponse(jwt, user.Id, user.Username, user.Email, expires);
    }
}
