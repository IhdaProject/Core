using System.ComponentModel;
using System.Reflection;
using Core.Domain.Permissions;

namespace Core.Infrastructure.Permissions;

/// <summary>
/// Mikroservisning ruxsatlar enum'lari yoki sinflaridan avtomatik ravishda
/// barcha ruxsat ta'riflarini (<see cref="PermissionDefinition"/>) yig'ib oluvchi yordamchi vosita.
/// </summary>
public static class PermissionDiscoveryHelper
{
    /// <summary>
    /// Berilgan Enum turidan barcha ruxsatlarni ajratib oladi.
    /// Enum ustidagi <see cref="PermissionsModuleAttribute"/> hamda a'zolar ustidagi
    /// <see cref="DescriptionAttribute"/> izohlarini avtomatik o'qiydi.
    /// </summary>
    /// <typeparam name="TEnum">Ruxsatlar enum turi.</typeparam>
    /// <returns>Ruxsat ta'riflari ro'yxati.</returns>
    public static List<PermissionDefinition> DiscoverFromEnum<TEnum>()
        where TEnum : struct, Enum
    {
        return DiscoverFromEnum(typeof(TEnum));
    }

    /// <summary>
    /// Berilgan Enum turidan barcha ruxsatlarni ajratib oladi.
    /// </summary>
    /// <param name="enumType">Enum turi.</param>
    /// <returns>Ruxsat ta'riflari ro'yxati.</returns>
    public static List<PermissionDefinition> DiscoverFromEnum(Type enumType)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException($"'{enumType.Name}' turi Enum bo'lishi kerak.", nameof(enumType));

        var moduleAttr = enumType.GetCustomAttribute<PermissionsModuleAttribute>();
        var serviceName = moduleAttr?.ServiceName ?? enumType.Name.Replace("Permissions", "").Replace("Permission", "");
        var moduleName = moduleAttr?.ModuleName;

        var list = new List<PermissionDefinition>();
        var values = Enum.GetValues(enumType);

        foreach (var val in values)
        {
            var code = Convert.ToInt32(val);
            var name = val.ToString()!;
            var fieldInfo = enumType.GetField(name);

            var descAttr = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
            var description = descAttr?.Description;

            list.Add(new PermissionDefinition(
                code: code,
                name: name,
                description: description,
                moduleName: moduleName,
                serviceName: serviceName));
        }

        return list;
    }

    /// <summary>
    /// Bir nechta Enum turlaridan ruxsatlarni bitta ro'yxatga yig'adi.
    /// </summary>
    /// <param name="enumTypes">Enum turlari massivi.</param>
    /// <returns>Barcha yig'ilgan ruxsatlar ro'yxati.</returns>
    public static List<PermissionDefinition> DiscoverFromEnums(params Type[] enumTypes)
    {
        var result = new List<PermissionDefinition>();
        foreach (var type in enumTypes)
        {
            result.AddRange(DiscoverFromEnum(type));
        }
        return result;
    }
}
