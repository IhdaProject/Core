using Core.Domain.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Core.Infrastructure.Storage;

public class MinioService : IMinioService
{
    private readonly string _bucketName;
    private readonly IMinioClient _client;

    public MinioService(IOptions<MinIoConfiguration> configuration)
    {
        _bucketName = configuration.Value.BucketName;
        _client = new MinioClient()
            .WithEndpoint(configuration.Value.Endpoint)
            .WithCredentials(configuration.Value.AccessKey, configuration.Value.SecretKey)
            .WithSSL(configuration.Value.UseSSL)
            .Build();
    }

    public async Task EnsureBucketExistsAsync()
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
    }

    public async Task UploadFileAsync(string objectName, string filePath)
    {
        await EnsureBucketExistsAsync();
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithFileName(filePath));
    }

    public async Task<string> UploadFileFromFormFileAsync(IFormFile file, string objectName)
    {
        var filePath = $"{DateTime.Today:yyyy/MM/dd}";
        objectName = $"{filePath}/{objectName}";
        await EnsureBucketExistsAsync();
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length));

        return objectName;
    }

    public async Task UploadFileFromBase64Async(string objectName, string base64Data)
    {
        await EnsureBucketExistsAsync();
        var bytes = Convert.FromBase64String(base64Data);
        using var ms = new MemoryStream(bytes);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length));
    }

    public async Task UpdateFileAsync(string objectName, string filePath)
    {
        await UploadFileAsync(objectName, filePath);
    }

    public async Task DeleteFileAsync(string objectName)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName));
    }

    public async Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600)
    {
        await EnsureBucketExistsAsync();
        var url = await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds));

        var uri = new Uri(url);
        return "/api-cloud" + uri.PathAndQuery;
    }

    public async Task<string> GetFileAsBase64Async(string objectName)
    {
        await EnsureBucketExistsAsync();
        using var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithCallbackStream(s => s.CopyTo(ms)));
        return Convert.ToBase64String(ms.ToArray());
    }
}
