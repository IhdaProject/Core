using System.Security.Cryptography;
using System.Text;

namespace Core.Domain.Helpers;

public class PasswordHelper
{
    private const int KeySize = 32;
    private const int IterationsCount = 1000;

    public static string Encrypt(string password, string salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: Encoding.UTF8.GetBytes(salt),
            iterations: IterationsCount,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string passwordHash, string password, string salt)
    {
        return Encrypt(password, salt).SequenceEqual(passwordHash);
    }
}
