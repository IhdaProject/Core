using System.Security.Cryptography;
using System.Text;

namespace Core.Domain.Helpers;

public class PasswordHelper
{
    private const int KeySize = 32;
    public const int DefaultIterationsCount = 600_000;
    private const int LegacyIterationsCount = 1000;

    public static string Encrypt(string password, string salt, int iterations = DefaultIterationsCount)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: Encoding.UTF8.GetBytes(salt),
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string passwordHash, string password, string salt)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
            return false;

        byte[] expectedHashBytes;
        try
        {
            expectedHashBytes = Convert.FromBase64String(passwordHash);
        }
        catch (FormatException)
        {
            return false;
        }

        // Try standard 600,000 iterations first
        var computedHashBytes = Convert.FromBase64String(Encrypt(password, salt, DefaultIterationsCount));
        if (CryptographicOperations.FixedTimeEquals(computedHashBytes, expectedHashBytes))
            return true;

        // Fallback for legacy 1,000-iteration hashes so existing users are not locked out
        var legacyHashBytes = Convert.FromBase64String(Encrypt(password, salt, LegacyIterationsCount));
        return CryptographicOperations.FixedTimeEquals(legacyHashBytes, expectedHashBytes);
    }
}
