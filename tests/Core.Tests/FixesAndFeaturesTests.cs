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
}
