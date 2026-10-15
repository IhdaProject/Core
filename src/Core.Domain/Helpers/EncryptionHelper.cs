using System.Security.Cryptography;
using System.Text;

namespace Core.Domain.Helpers;

public static class EncryptionHelper
{
    public static string Encrypt(string type, long id, long userId, string key)
    {
        var timestamp = DateTime.UtcNow.Ticks;
        var data = $"{type}:{id}:{userId}:{timestamp}";
        using var aesAlg = Aes.Create();
        aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
        aesAlg.IV = GenerateRandomBytes(aesAlg.BlockSize / 8);

        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            using var swEncrypt = new StreamWriter(csEncrypt);
            swEncrypt.Write(data);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public static string EncryptString(string url, long userId, string key)
    {
        var data = $"{url}:{userId}";
        using var aesAlg = Aes.Create();
        aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
        aesAlg.IV = GenerateRandomBytes(aesAlg.BlockSize / 8);

        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            using var swEncrypt = new StreamWriter(csEncrypt);
            swEncrypt.Write(data);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public static string EncryptStringWithTime(string token, long id, string key, DateTime? expire = null)
    {
        var timestamp = (expire ?? DateTime.UtcNow).Ticks;
        var data = $"{id}:{token}:{timestamp}";
        using var aesAlg = Aes.Create();
        aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
        aesAlg.IV = GenerateRandomBytes(aesAlg.BlockSize / 8);

        var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            using var swEncrypt = new StreamWriter(csEncrypt);
            swEncrypt.Write(data);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public static (string Type, long Id, long UserId, DateTime Timestamp) Decrypt(string encrypted, string key)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(encrypted);
            using var aesAlg = Aes.Create();
            aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
            var iv = new byte[aesAlg.BlockSize / 8];
            if (fullCipher.Length <= iv.Length) return default;

            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, iv);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            var decrypted = srDecrypt.ReadToEnd();

            var parts = decrypted.Split(':');
            if (parts.Length < 4) return default;

            var lastPart = parts[^1];
            var secondLast = parts[^2];
            var thirdLast = parts[^3];
            var type = string.Join(':', parts[..^3]);

            if (!long.TryParse(thirdLast, out var id) ||
                !long.TryParse(secondLast, out var userId) ||
                !long.TryParse(lastPart, out var ticks))
                return default;

            return (type, id, userId, new DateTime(ticks));
        }
        catch
        {
            return default;
        }
    }

    public static (string url, long UserId) DecryptString(string encrypted, string key)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(encrypted);
            using var aesAlg = Aes.Create();
            aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
            var iv = new byte[aesAlg.BlockSize / 8];
            if (fullCipher.Length <= iv.Length) return default;

            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, iv);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            var decrypted = srDecrypt.ReadToEnd();

            var lastColonIndex = decrypted.LastIndexOf(':');
            if (lastColonIndex <= 0 || lastColonIndex == decrypted.Length - 1) return default;

            var url = decrypted[..lastColonIndex];
            if (!long.TryParse(decrypted[(lastColonIndex + 1)..], out var userId)) return default;

            return (url, userId);
        }
        catch
        {
            return default;
        }
    }

    public static (long Id, string Token, DateTime Timestamp) DecryptStringWithTime(string encrypted, string key)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(encrypted);
            using var aesAlg = Aes.Create();
            aesAlg.Key = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
            var iv = new byte[aesAlg.BlockSize / 8];
            if (fullCipher.Length <= iv.Length) return default;

            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, iv);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            var decrypted = srDecrypt.ReadToEnd();

            var firstColon = decrypted.IndexOf(':');
            var lastColon = decrypted.LastIndexOf(':');
            if (firstColon <= 0 || lastColon <= firstColon || lastColon == decrypted.Length - 1) return default;

            if (!long.TryParse(decrypted[..firstColon], out var id)) return default;
            var token = decrypted.Substring(firstColon + 1, lastColon - firstColon - 1);
            if (!long.TryParse(decrypted[(lastColon + 1)..], out var ticks)) return default;

            return (id, token, new DateTime(ticks));
        }
        catch
        {
            return default;
        }
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
