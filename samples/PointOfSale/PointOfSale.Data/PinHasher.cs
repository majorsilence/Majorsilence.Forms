using System.Security.Cryptography;

namespace PointOfSale.Data;

/// <summary>
/// PBKDF2 PIN hashing. Shared by the API (login) and the seeder (demo accounts).
/// Illustrative for a sample app — not a substitute for a vetted auth stack in production.
/// </summary>
public static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static (string Hash, string Salt) HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPin(string pin, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
