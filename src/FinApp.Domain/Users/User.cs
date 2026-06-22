using FinApp.Domain.Common;

namespace FinApp.Domain.Users;

/// <summary>
/// A person who signs in to FinApp. Distinct from a <see cref="Accounts.Account"/> (a "domain account"
/// holding funds/budgets): a user owns and contributes to domain accounts. Credentials are stored as an
/// opaque <see cref="PasswordHash"/> only — hashing/verification is done by an <see cref="IPasswordHasher"/>.
/// Username/email uniqueness is enforced by the store (unique index), not here.
/// </summary>
public sealed class User : Entity
{
    public string Username { get; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }

    public User(string username, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (!email.Contains('@'))
            throw new ArgumentException("Email is not valid.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        Username = username.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
    }

    public void ChangeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Email is not valid.", nameof(email));
        Email = email.Trim().ToLowerInvariant();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
    }
}
