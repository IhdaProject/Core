using System.Security.Claims;
using Core.Domain.Common;
using Core.Domain.Enums;
using Core.Domain.Models.Auth;
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

    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<Structure> Structures { get; set; } = null!;
    public DbSet<StructurePermission> StructurePermissions { get; set; } = null!;

    private long GetCurrentUserId()
    {
        var httpContextAccessor = _serviceProvider?.GetService<IHttpContextAccessor>();
        var userIdClaim = httpContextAccessor?.HttpContext?.User?.FindFirst(CustomClaimNames.UserId)
                          ?? httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            return userId;

        return -1;
    }

    private void TrackActionsAt()
    {
        var dateTimeNow = DateTime.UtcNow;
        var userId = GetCurrentUserId();

        foreach (var entity in ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Added && x.Entity is AuditableModelBase<long>))
        {
            var model = (AuditableModelBase<long>)entity.Entity;
            model.CreatedAt = dateTimeNow;
            model.CreatedBy = userId;
        }

        foreach (var entity in ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Modified && x.Entity is AuditableModelBase<long>))
        {
            var model = (AuditableModelBase<long>)entity.Entity;
            model.UpdatedAt = dateTimeNow;
            model.UpdatedBy = userId;
        }
    }

    public override int SaveChanges()
    {
        TrackActionsAt();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TrackActionsAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TrackActionsAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        TrackActionsAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
