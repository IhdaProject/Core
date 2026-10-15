using System.Linq.Expressions;
using System.Security.Claims;
using Core.Domain.Common;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure.Data.Context;

public class CoreDbContext : DbContext
{
    private readonly IServiceProvider? _serviceProvider;

    public CoreDbContext(DbContextOptions options, IServiceProvider? serviceProvider = null)
        : base(options)
    {
        _serviceProvider = serviceProvider;
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    protected virtual long GetCurrentUserId()
    {
        var httpContextAccessor = _serviceProvider?.GetService<IHttpContextAccessor>();
        var userIdClaim = httpContextAccessor?.HttpContext?.User?.FindFirst(CustomClaimNames.UserId)
                          ?? httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            return userId;

        return -1;
    }

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

    public override int SaveChanges()
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProcessSoftDeleteAndAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filter for soft-deleted entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDelete));
                var notDeleted = Expression.Not(property);
                var lambda = Expression.Lambda(notDeleted, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // Configuring all MultiLanguageField properties over entities to jsonb
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
