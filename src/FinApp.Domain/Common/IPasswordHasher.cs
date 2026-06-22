namespace FinApp.Domain.Common;

/// <summary>
/// Hashes and verifies user passwords. The domain stores only the opaque hash string on
/// <see cref="Users.User"/>; the actual algorithm (PBKDF2 etc.) lives in infrastructure so the
/// domain stays free of crypto/platform concerns.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produce a self-describing hash (salt + parameters embedded) for a plaintext password.</summary>
    string Hash(string password);

    /// <summary>True if <paramref name="password"/> matches a hash previously produced by <see cref="Hash"/>.</summary>
    bool Verify(string password, string hash);
}
