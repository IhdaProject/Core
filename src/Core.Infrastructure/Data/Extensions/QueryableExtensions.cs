using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Core.Domain.Models.Api;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure.Data.Extensions;

/// <summary>
/// IQueryable kengaytmalari.
/// Dinamik filtrlash, saralash, sahifalash hamda xotirani tejovchi read-only so'rovlar uchun xizmat qiladi.
/// Parametrlashtirilgan Dynamic LINQ orqali EF Core ning CompiledQueryCache va
/// PostgreSQL server-side AutoPrepare keshini 100% qayta ishlatilishini ta'minlaydi.
/// </summary>
public static class QueryableExtensions
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

    /// <summary>
    /// Model xususiyatlarini keshdan oladi (har bir so'rovda qimmat reflection ishlatilmasligi uchun).
    /// </summary>
    private static Dictionary<string, PropertyInfo> GetProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead)
             .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// EF Core so'rovlarida qiymatlarni SQL parametri (@p0, $1) sifatida generatsiya qilish uchun closure o'rami.
    /// Bu EF Core'ning CompiledQueryCache'ini qayta ishlatishini va SQL execution plan keshlanishini kafolatlaydi.
    /// </summary>
    private static Expression CreateParameterExpression(object? value, Type targetType)
    {
        var closure = new ParameterClosure(value);
        var constant = Expression.Constant(closure);
        var property = Expression.Property(constant, nameof(ParameterClosure.Value));
        return Expression.Convert(property, targetType);
    }

    private sealed class ParameterClosure
    {
        public object? Value { get; }
        public ParameterClosure(object? value) => Value = value;
    }

    /// <summary>
    /// MetaQueryModel ichidagi filtrlash ifodalari asosida IQueryable ga dinamik WHERE shartlarini qo'llaydi.
    /// Qo'llab-quvvatlanadigan operatorlar: ==, !=, &lt;&gt;, &gt;=, &lt;=, &gt;&gt;, &lt;&lt;, $$ (ILike).
    /// </summary>
    /// <typeparam name="T">Domen yoki DTO entity turi.</typeparam>
    /// <param name="queryable">Boshlang'ich IQueryable to'plami.</param>
    /// <param name="query">Filtrlash qoidalarini o'z ichiga olgan MetaQueryModel.</param>
    /// <returns>Filtrlar qo'llanilgan IQueryable.</returns>
    public static IQueryable<T> FilterByExpressions<T>(this IQueryable<T> queryable, MetaQueryModel query)
    {
        if (query.FilteringExpressions is null || query.FilteringExpressions.Count == 0)
            return queryable;

        var entityType = typeof(T);
        var properties = GetProperties(entityType);

        foreach (var expression in query.FilteringExpressions)
        {
            if (string.IsNullOrWhiteSpace(expression.PropertyName) || expression.Value is null)
                continue;

            if (!properties.TryGetValue(expression.PropertyName, out var property))
                throw new InvalidOperationException($"'{expression.PropertyName}' nomli maydon '{entityType.Name}' turida topilmadi.");

            var parameter = Expression.Parameter(entityType, "x");
            var left = Expression.MakeMemberAccess(parameter, property);

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var convertedValue = ConvertValue(expression.Value, targetType);

            var right = CreateParameterExpression(convertedValue, property.PropertyType);

            Expression switchedExpression = expression.Type switch
            {
                "!=" or "<>" => Expression.NotEqual(left, right),
                ">=" => Expression.GreaterThanOrEqual(left, right),
                "<=" => Expression.LessThanOrEqual(left, right),
                "<<" => Expression.LessThan(left, right),
                ">>" => Expression.GreaterThan(left, right),
                "$$" => BuildILikeExpression(left, expression.Value),
                "==" => Expression.Equal(left, right),
                _ => Expression.Equal(left, right)
            };

            var predicate = Expression.Lambda<Func<T, bool>>(switchedExpression, parameter);
            queryable = queryable.Where(predicate);
        }

        return queryable;
    }

    private static Expression BuildILikeExpression(Expression left, string rawValue)
    {
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var patternExpr = CreateParameterExpression($"%{rawValue}%", typeof(string));

        var iLikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;

        return Expression.Call(null, iLikeMethod, efFunctions, left, patternExpr);
    }

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (value is null)
            return null;

        if (targetType == typeof(string))
            return value;

        if (targetType.IsEnum)
            return Enum.TryParse(targetType, value, true, out var enumResult) ? enumResult : null;

        if (targetType == typeof(Guid))
            return Guid.TryParse(value, out var g) ? g : null;

        if (targetType == typeof(DateTimeOffset))
            return DateTimeOffset.TryParse(value, out var dto) ? dto : null;

        if (targetType == typeof(DateTime))
            return DateTime.TryParse(value, out var dt) ? dt : null;

        try
        {
            var converter = TypeDescriptor.GetConverter(targetType);
            return converter.CanConvertFrom(typeof(string))
                ? converter.ConvertFromInvariantString(value)
                : Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// MetaQueryModel dagi saralash qoidalari bo'yicha IQueryable to'plamini saralaydi (OrderBy / OrderByDescending).
    /// </summary>
    /// <typeparam name="T">Entity turi.</typeparam>
    /// <param name="queryable">Boshlang'ich to'plam.</param>
    /// <param name="query">Saralash qoidalari mavjud bo'lgan MetaQueryModel.</param>
    /// <returns>Saralangan IQueryable.</returns>
    public static IQueryable<T> Sort<T>(this IQueryable<T> queryable, MetaQueryModel query)
    {
        if (query.SortingExpressions is null || query.SortingExpressions.Count == 0)
            return queryable;

        var entityType = typeof(T);
        var properties = GetProperties(entityType);
        var one = true;

        foreach (var sortData in query.SortingExpressions.Where(sort => !string.IsNullOrEmpty(sort.SortFieldName)))
        {
            if (!properties.TryGetValue(sortData.SortFieldName, out var property))
                throw new InvalidOperationException($"'{sortData.SortFieldName}' nomli maydon saralash uchun '{entityType.Name}' turida topilmadi.");

            var parameter = Expression.Parameter(entityType, "x");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var lambda = Expression.Lambda(propertyAccess, parameter);

            string methodName;
            if (one)
            {
                one = false;
                methodName = string.Equals(sortData.Direction, "asc", StringComparison.OrdinalIgnoreCase)
                    ? "OrderBy"
                    : "OrderByDescending";
            }
            else
            {
                methodName = string.Equals(sortData.Direction, "asc", StringComparison.OrdinalIgnoreCase)
                    ? "ThenBy"
                    : "ThenByDescending";
            }

            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                [entityType, property.PropertyType],
                queryable.Expression,
                Expression.Quote(lambda));

            queryable = queryable.Provider.CreateQuery<T>(resultExpression);
        }

        return queryable;
    }

    /// <summary>
    /// Sahifalashni (Skip va Take) qo'llaydi.
    /// </summary>
    /// <typeparam name="T">Entity turi.</typeparam>
    /// <param name="queryable">IQueryable to'plami.</param>
    /// <param name="query">Skip va Take qiymatlari mavjud bo'lgan MetaQueryModel.</param>
    /// <returns>Sahifalangan IQueryable.</returns>
    public static IQueryable<T> Paging<T>(this IQueryable<T> queryable, MetaQueryModel query)
    {
        return queryable
            .Skip(query.Skip)
            .Take(query.Take);
    }

    /// <summary>
    /// Change Tracker xotirasini tejash uchun so'rovni AsNoTracking bilan read-only ro'yxat shaklida oladi.
    /// </summary>
    /// <typeparam name="T">Entity turi.</typeparam>
    /// <param name="query">IQueryable to'plami.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Read-only ro'yxat.</returns>
    public static async Task<List<T>> ToReadOnlyListAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
    {
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
