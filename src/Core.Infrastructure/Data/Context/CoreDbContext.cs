using System.Linq.Expressions;
using System.Security.Claims;
using Core.Domain.Common;
using Core.Domain.Common.Context;
using Core.Domain.Enums;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure.Data.Context;

/// <summary>
/// Barcha mikroservislar uchun umumiy bazaviy DbContext.
/// Quyidagi imkoniyatlar avtomatik ravishda sozlangan:
/// 1. ISoftDelete: Ma'lumotlarni jismonan o'chirmasdan IsDelete=true ga o'tkazish va Global Query Filter.
/// 2. IAuditable: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy maydonlarini SaveChanges paytida avtomatik to'ldirish.
/// 3. Multi-Tenancy: ITenantEntity entitiylari uchun avtomatik ijarachi izolyatsiyasi va (TenantId, IsDelete) indeksi.
/// 4. Optimistik qulflash: PostgreSQL tizim ustuni "xmin" (xid) orqali Concurrency token.
/// 5. MassTransit Transactional Outbox jadvallarini avtomatik xaritalash.
/// 6. MultiLanguageField maydonlarini jsonb ga o'girish.
/// </summary>
public class CoreDbContext : DbContext
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Joriy so'rov ijarachi konteksti.
    /// </summary>
    public ITenantContext TenantContext => _tenantContext;

    /// <summary>
    /// Ijarachi filtri ayni vaqtda o'chirilganligini ko'rsatadi (Global Query Filter uchun).
    /// </summary>
    public bool IsTenantFilterDisabled => _tenantContext.IsTenantFilterDisabled;

    /// <summary>
    /// Joriy ijarachi identifikatori (Global Query Filter uchun).
    /// </summary>
    public long? CurrentTenantId => _tenantContext.TenantId;

    /// <summary>
    /// Joriy ijarachi qamrovi (Global Query Filter uchun).
    /// </summary>
    public TenantScope CurrentScope => _tenantContext.Scope;

    /// <summary>
    /// <see cref="CoreDbContext"/> konstruktori.
    /// </summary>
    /// <param name="options">DbContext konfiguratsiya parametrlari.</param>
    /// <param name="serviceProvider">Servis provayderi (HttpContextAccessor va claimlarni olish uchun).</param>
    /// <param name="tenantContext">Ijarachi konteksti (ko'p ijarachilik uchun).</param>
    public CoreDbContext(
        DbContextOptions options,
        IServiceProvider? serviceProvider = null,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _serviceProvider = serviceProvider;
        _tenantContext = tenantContext ?? serviceProvider?.GetService<ITenantContext>() ?? NullTenantContext.Instance;
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    /// <summary>
    /// Joriy avtorizatsiyadan o'tgan foydalanuvchi identifikatorini claimlardan aniqlaydi.
    /// </summary>
    /// <returns>Foydalanuvchi ID raqami yoki aniqlanmasa -1.</returns>
    protected virtual long GetCurrentUserId()
    {
        var httpContextAccessor = _serviceProvider?.GetService<IHttpContextAccessor>();
        var userIdClaim = httpContextAccessor?.HttpContext?.User?.FindFirst(CustomClaimNames.UserId)
                          ?? httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            return userId;

        return -1;
    }

    /// <summary>
    /// O'chirilayotgan entitiylarni soft-delete ga o'giradi va yaratilgan/o'zgartirilgan yozuvlarni audit qiladi.
    /// </summary>
    private void ProcessSoftDeleteAndAudit()
    {
        var dateTimeNow = DateTime.UtcNow;
        var userId = GetCurrentUserId();

        // Soft delete processing: convert deleted entities into soft-deleted
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDelete = true;
            }
        }

        // Auditing processing
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = dateTimeNow;
                if (entry.Entity is IAuditable<long> auditableLong)
                {
                    auditableLong.CreatedBy = userId;
                }
                else if (entry.Entity is IAuditable<string> auditableString)
                {
                    auditableString.CreatedBy = userId.ToString();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = dateTimeNow;
                if (entry.Entity is IAuditable<long> auditableLong)
                {
                    auditableLong.UpdatedBy = userId;
                }
                else if (entry.Entity is IAuditable<string> auditableString)
                {
                    auditableString.UpdatedBy = userId.ToString();
                }
            }
        }
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;

            // 1. PostgreSQL optimistik qulflash tokeni: xmin tizim ustuni
            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .Property<uint>("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }

            // 2. Global Query Filter (ISoftDelete va ITenantEntity)
            var parameter = Expression.Parameter(clrType, "e");
            Expression? combinedFilter = null;

            if (typeof(ISoftDelete).IsAssignableFrom(clrType))
            {
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDelete));
                combinedFilter = Expression.Not(property);
            }

            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                var contextExpr = Expression.Constant(this);

                var isDisabledProp = Expression.Property(contextExpr, nameof(IsTenantFilterDisabled));
                var scopeProp = Expression.Property(contextExpr, nameof(CurrentScope));
                var globalConst = Expression.Constant(TenantScope.Global);
                var isGlobal = Expression.Equal(scopeProp, globalConst);

                var tenantIdProp = Expression.Property(contextExpr, nameof(CurrentTenantId));
                var entityTenantId = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var matchTenant = Expression.Equal(entityTenantId, tenantIdProp);

                var combinedTenantScope = Expression.OrElse(isGlobal, matchTenant);
                var tenantFilter = Expression.OrElse(isDisabledProp, combinedTenantScope);

                combinedFilter = combinedFilter is null
                    ? tenantFilter
                    : Expression.AndAlso(combinedFilter, tenantFilter);

                // PostgreSQL sequential scan oldini olish uchun (TenantId, IsDelete) kompozit B-Tree indeksi
                modelBuilder.Entity(clrType)
                    .HasIndex(nameof(ITenantEntity.TenantId), nameof(ISoftDelete.IsDelete));
            }

            if (combinedFilter is not null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(combinedFilter, parameter));
            }
        }

        // 3. MassTransit Transactional Outbox/Inbox entitiylarini ro'yxatdan o'tkazish
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // 4. MultiLanguageField maydonlarini PostgreSQL jsonb ustuni sifatida sozlash
        var multiLanguageFields = modelBuilder
            .Model
            .GetEntityTypes()
            .SelectMany(x => x.ClrType.GetProperties())
            .Where(x => x.PropertyType == typeof(MultiLanguageField));

        foreach (var multiLanguageField in multiLanguageFields)
        {
            modelBuilder
                .Entity(multiLanguageField.DeclaringType!)
                .Property(multiLanguageField.PropertyType, multiLanguageField.Name)
                .HasColumnType("jsonb");
        }
    }
}
