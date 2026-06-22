using FinApp.Contracts;
using FinApp.Domain.Common;
using FinApp.Domain.Users;
using FinApp.Persistence;
using FinApp.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinApp.Server.Auth;

/// <summary>Registers and authenticates users, returning a bearer token on success.</summary>
public sealed class AuthService(FinAppDbContext db, IPasswordHasher hasher, JwtTokenService tokens)
{
    private const int MinPasswordLength = 8;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var username = (request.Username ?? "").Trim();
        var email = (request.Email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required.");
        if ((request.Password ?? "").Length < MinPasswordLength)
            throw new BadRequestException($"Password must be at least {MinPasswordLength} characters.");

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            throw new ConflictException("That username is already taken.");
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("That email is already registered.");

        User user;
        try
        {
            user = new User(username, email, hasher.Hash(request.Password!));
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return tokens.Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = (request.UsernameOrEmail ?? "").Trim();
        var identifierLower = identifier.ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == identifierLower || u.Email == identifierLower, ct);

        if (user is null || !hasher.Verify(request.Password ?? "", user.PasswordHash))
            throw new UnauthorizedException("Invalid username or password.");

        return tokens.Issue(user);
    }
}
