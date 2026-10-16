using System.Security.Cryptography;
using Core.Domain.Common;
using Core.Domain.Enums;
using Core.Domain.Events;
using Core.Domain.Exceptions;
using Core.Domain.Helpers;
using Core.Domain.Models.Api;
using Core.Infrastructure.Resilience;
using Core.Web.Constants;
using Xunit;

namespace Core.Tests;

public class FixesAndFeaturesTests
{
    [Fact]
    public void MetaQueryModel_NoStackOverflow_WhenNullOrEmptyOrWhitespace()
    {
        var model = new MetaQueryModel
        {
            FilteringExpressionsJson = null
        };
        Assert.Null(model.FilteringExpressions);

        model.FilteringExpressionsJson = "";
        Assert.Null(model.FilteringExpressions);

        model.FilteringExpressionsJson = "   ";
        Assert.Null(model.FilteringExpressions);

        model.SortingExpressionsJson = null;
        Assert.Null(model.SortingExpressions);

        model.SortingExpressionsJson = "";
        Assert.Null(model.SortingExpressions);

        model.SortingExpressionsJson = "invalid-json";
        Assert.Null(model.SortingExpressions);
    }

    [Fact]
    public void EncryptionHelper_HandlesUrlsWithColonsAndRandomIv()
    {
        var key = "MySecretKeyForTestingEncryption32!";
        var testUrl = "https://example.com/api/v1/resource?id=123:456&token=abc:def";
        long userId = 998877;

        var encrypted1 = EncryptionHelper.EncryptString(testUrl, userId, key);
        var encrypted2 = EncryptionHelper.EncryptString(testUrl, userId, key);

        // Random IV ensures same plaintext produces different ciphertexts
        Assert.NotEqual(encrypted1, encrypted2);

        var (decryptedUrl, decryptedUserId) = EncryptionHelper.DecryptString(encrypted1, key);
        Assert.Equal(testUrl, decryptedUrl);
        Assert.Equal(userId, decryptedUserId);

        var (decryptedUrl2, decryptedUserId2) = EncryptionHelper.DecryptString(encrypted2, key);
        Assert.Equal(testUrl, decryptedUrl2);
        Assert.Equal(userId, decryptedUserId2);
    }

    [Fact]
    public void PasswordHelper_EncryptAndVerify_WorksWithFixedTimeEquals()
    {
        var password = "SecurePassword123!";
        var salt = "random_salt_123";

        var hash = PasswordHelper.Encrypt(password, salt);
        Assert.NotNull(hash);

        var isCorrect = PasswordHelper.Verify(hash, password, salt);
        Assert.True(isCorrect);

        var isWrong = PasswordHelper.Verify(hash, "WrongPassword!", salt);
        Assert.False(isWrong);
    }

    [Fact]
    public void PasswordHelper_VerifiesLegacyHash()
    {
        var password = "OldUserPassword456";
        var salt = "legacy_salt_456";

        // Generate legacy 1,000 iteration hash
        var legacyHash = PasswordHelper.Encrypt(password, salt, iterations: 1000);

        // Verify should successfully validate legacy hash
        var isLegacyValid = PasswordHelper.Verify(legacyHash, password, salt);
        Assert.True(isLegacyValid);
    }

    [Fact]
    public void Exceptions_HaveCorrectStatusCodes()
    {
        var alreadyExists = new AlreadyExistsException("Resource exists");
        Assert.Equal(409, alreadyExists.StatusCode);
        Assert.Equal(ErrorTypes.Conflict, alreadyExists.ErrorCode);

        var forbidden = new ForbiddenException("Access denied");
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal(ErrorTypes.Forbidden, forbidden.ErrorCode);

        var unauthorized = new UnauthorizedException("Not authenticated");
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public void StaticCache_Permissions_IsThreadSafeConcurrentDictionary()
    {
        StaticCache.Permissions.Clear();

        Parallel.For(0, 100, i =>
        {
            StaticCache.Permissions.TryAdd(i, [i, i + 1]);
        });

        Assert.Equal(100, StaticCache.Permissions.Count);
    }

    private class TestGuidAuditableModel : AuditableModelBase<Guid, Guid>
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void AuditableModelBase_SupportsGuidAndGenericUserIds()
    {
        var entity = new TestGuidAuditableModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid(),
            IsDelete = false
        };

        Assert.IsAssignableFrom<ISoftDelete>(entity);
        Assert.IsAssignableFrom<IAuditable<Guid>>(entity);
        Assert.False(entity.IsDelete);
    }

    [Fact]
    public void EventBus_IntegrationEvent_GeneratesProperDefaults()
    {
        var @event = new IntegrationEvent();
        Assert.NotEqual(Guid.Empty, @event.Id);
        Assert.True(@event.CreationDate <= DateTime.UtcNow);
    }

    [Fact]
    public void Resilience_PipelineBuildsProperly()
    {
        var pipeline = ResiliencePipelineExtensions.CreateDefaultHttpResiliencePipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void NullTenantContext_BehavesCorrectly()
    {
        var context = Core.Domain.Common.Context.NullTenantContext.Instance;
        Assert.Null(context.TenantId);
        Assert.Null(context.TenantCode);
        Assert.Equal(Core.Domain.Common.Context.TenantScope.Global, context.Scope);
        Assert.True(context.IsCrossTenant);
        Assert.True(context.IsTenantFilterDisabled);

        // Change and DisableTenantFilter should return safe no-op disposables
        using var disposable1 = context.Change(123);
        Assert.NotNull(disposable1);

        using var disposable2 = context.DisableTenantFilter();
        Assert.NotNull(disposable2);
    }

    [Fact]
    public void AppHeaderNames_ContainRequiredKeys()
    {
        Assert.Equal("X-Correlation-Id", Core.Domain.Constants.AppHeaderNames.CorrelationId);
        Assert.Equal("X-Idempotency-Key", Core.Domain.Constants.AppHeaderNames.IdempotencyKey);
        Assert.Equal("X-Tenant-Id", Core.Domain.Constants.AppHeaderNames.TenantId);
        Assert.Equal("X-User-Id", Core.Domain.Constants.AppHeaderNames.UserId);
        Assert.Equal("X-Cache", Core.Domain.Constants.AppHeaderNames.XCache);
    }

    private class SampleTestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [Fact]
    public void QueryableExtensions_FilterByExpressions_WorksInMemory()
    {
        var items = new List<SampleTestItem>
        {
            new() { Id = 1, Name = "Alice", Age = 25 },
            new() { Id = 2, Name = "Bob", Age = 30 },
            new() { Id = 3, Name = "Charlie", Age = 35 }
        }.AsQueryable();

        var query = new MetaQueryModel
        {
            FilteringExpressions =
            [
                new MetaQueryFilterModel { PropertyName = "Age", Type = ">=", Value = "30" }
            ]
        };

        var filtered = Core.Infrastructure.Data.Extensions.QueryableExtensions.FilterByExpressions(items, query).ToList();
        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, x => x.Name == "Bob");
        Assert.Contains(filtered, x => x.Name == "Charlie");
    }

    [Core.Domain.Permissions.PermissionsModule("SampleService", 88, "SampleModule")]
    private enum SampleCustomPermissions
    {
        [System.ComponentModel.Description("Create sample item description")]
        CreateSample = 88100001,
        [System.ComponentModel.Description("Delete sample item description")]
        DeleteSample = 88100002
    }

    [Fact]
    public void PermissionDiscoveryHelper_ExtractsAllMetadataProperly()
    {
        var permissions = Core.Infrastructure.Permissions.PermissionDiscoveryHelper.DiscoverFromEnum<SampleCustomPermissions>();

        Assert.Equal(2, permissions.Count);

        var createPerm = permissions.First(p => p.Name == "CreateSample");
        Assert.Equal(88100001, createPerm.Code);
        Assert.Equal("Create sample item description", createPerm.Description);
        Assert.Equal("SampleModule", createPerm.ModuleName);
        Assert.Equal("SampleService", createPerm.ServiceName);
    }

    [Fact]
    public void PermissionAuthorizeAttribute_AcceptsCustomEnum_AndExtractsCorrectIntCodes()
    {
        // 1. With custom enum
        var customAttr = new Core.Web.Attributes.PermissionAuthorizeAttribute(SampleCustomPermissions.CreateSample, SampleCustomPermissions.DeleteSample);
        Assert.NotNull(customAttr.Arguments);
        var customCodes = Assert.IsType<int[]>(customAttr.Arguments[0]);
        Assert.Equal([88100001, 88100002], customCodes);

        // 2. With legacy UserPermissions enum
        var legacyAttr = new Core.Web.Attributes.PermissionAuthorizeAttribute(UserPermissions.LogOut);
        var legacyCodes = Assert.IsType<int[]>(legacyAttr.Arguments![0]);
        Assert.Equal([10100001], legacyCodes);

        // 3. With raw integers
        var rawAttr = new Core.Web.Attributes.PermissionAuthorizeAttribute(88100001);
        var rawCodes = Assert.IsType<int[]>(rawAttr.Arguments![0]);
        Assert.Equal([88100001], rawCodes);
    }
}
