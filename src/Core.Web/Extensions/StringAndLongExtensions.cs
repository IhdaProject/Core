using Core.Domain.Exceptions;
using Core.Domain.Helpers;
using Core.Web.Constants;

namespace Core.Web.Extensions;

public static class StringAndLongExtensions
{
    private const int AddMinutes = 60;

    public static string EncryptId(this long id, string type, long userId)
    {
        return EncryptionHelper.Encrypt(type, id, userId, StaticCache.SymmetricKey);
    }

    public static List<string> EncryptId(this List<long> ids, string type, long userId)
    {
        return [..ids.Select(id => EncryptionHelper.Encrypt(type, id, userId, StaticCache.SymmetricKey))];
    }

    public static string EncryptUrl(this string url, long userId)
    {
        return EncryptionHelper.EncryptString(url, userId, StaticCache.SymmetricKey);
    }

    public static long DecryptId(this string encryptedInfo, string type, long currentUserId)
    {
        try
        {
            var decrypt = EncryptionHelper.Decrypt(encryptedInfo, StaticCache.SymmetricKey);
            if (string.IsNullOrEmpty(decrypt.Type) ||
                decrypt.Type != type || decrypt.Id == 0 ||
                decrypt.UserId != currentUserId ||
                decrypt.Timestamp.AddMinutes(AddMinutes) < DateTime.UtcNow)
                throw new AlreadyExistsException("Forbidden");

            return decrypt.Id;
        }
        catch (Exception)
        {
            throw new AlreadyExistsException("Forbidden");
        }
    }

    public static List<long> DecryptId(this List<string> encryptedData, string type, long currentUserId)
    {
        return
        [
            ..encryptedData.Select(encryptedInfo =>
            {
                try
                {
                    var decrypt = EncryptionHelper.Decrypt(encryptedInfo, StaticCache.SymmetricKey);
                    if (string.IsNullOrEmpty(decrypt.Type) ||
                        decrypt.Type != type || decrypt.Id == 0 ||
                        decrypt.UserId != currentUserId ||
                        decrypt.Timestamp.AddMinutes(AddMinutes) < DateTime.UtcNow)
                        throw new AlreadyExistsException("Forbidden");

                    return decrypt.Id;
                }
                catch (Exception)
                {
                    throw new AlreadyExistsException("Forbidden");
                }
            })
        ];
    }

    public static string DecryptUrl(this string encryptedData, long currentUserId)
    {
        try
        {
            var decrypt = EncryptionHelper.DecryptString(encryptedData, StaticCache.SymmetricKey);
            if (decrypt.url == null || decrypt.UserId != currentUserId)
                throw new AlreadyExistsException("Forbidden");

            return decrypt.url;
        }
        catch (Exception)
        {
            throw new AlreadyExistsException("Forbidden");
        }
    }

    public static (long id, string str) DecryptString(this string encryptedData)
    {
        try
        {
            var decrypt = EncryptionHelper.DecryptString(encryptedData, StaticCache.SymmetricKey);
            return (decrypt.UserId, decrypt.url);
        }
        catch (Exception)
        {
            throw new AlreadyExistsException("Forbidden");
        }
    }
}
