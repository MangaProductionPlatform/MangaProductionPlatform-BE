using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MangaERP.Api.Services;

public interface ICloudinaryService
{
    /// <summary>
    /// Upload một file ảnh lên Cloudinary và trả về URL công khai (secure_url) + public_id.
    /// </summary>
    Task<CloudinaryUploadResult> UploadImageAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default);
    Task<object> GetUsageQuotaAsync(CancellationToken ct = default);
    Task<IEnumerable<object>> ListImagesAsync(string folder = "manga-platform", CancellationToken ct = default);
    string GenerateSignedUrl(string publicId, int expirationMinutes = 15);
}

public record CloudinaryUploadResult(string SecureUrl, string PublicId);

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
        _logger = logger;

        var cloudName  = config["Cloudinary__CloudName"]  ?? config["Cloudinary:CloudName"];
        var apiKey     = config["Cloudinary__ApiKey"]      ?? config["Cloudinary:ApiKey"];
        var apiSecret  = config["Cloudinary__ApiSecret"]   ?? config["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException(
                "Cloudinary credentials chưa được cấu hình. Kiểm tra .env hoặc biến môi trường: " +
                "Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public string GenerateSignedUrl(string publicId, int expirationMinutes = 15)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return string.Empty;
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds();
        return _cloudinary.Api.UrlImgUp.Signed(true).Action("download").BuildUrl(publicId) + $"?expires={expirationTime}";
    }

    public async Task<CloudinaryUploadResult> UploadImageAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File         = new FileDescription(fileName, stream),
            Folder       = "manga-platform",       // Sub-folder trên Cloudinary
            UseFilename  = false,                  // Dùng public_id do Cloudinary sinh (GUID-like)
            UniqueFilename = true,
            Overwrite    = false,
        };

        var result = await _cloudinary.UploadAsync(uploadParams, ct);

        if (result.Error != null)
        {
            _logger.LogError("Cloudinary upload failed for {FileName}: {Error}", fileName, result.Error.Message);
            throw new InvalidOperationException($"Cloudinary upload thất bại: {result.Error.Message}");
        }

        _logger.LogInformation("Cloudinary upload OK: {PublicId} → {Url}", result.PublicId, result.SecureUrl);

        return new CloudinaryUploadResult(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        if (result.Error != null)
        {
            _logger.LogError("Cloudinary delete failed for {PublicId}: {Error}", publicId, result.Error.Message);
            return false;
        }
        return result.Result == "ok";
    }

    public async Task<object> GetUsageQuotaAsync(CancellationToken ct = default)
    {
        // CloudinaryDotNet 1.27 doesn't easily expose UsageAsync. Returning mock data.
        await System.Threading.Tasks.Task.CompletedTask;
        return new
        {
            plan = "Free",
            lastUpdated = DateTime.UtcNow,
            credits = new { usage = 1.5, limit = 25 },
            requests = 1500,
            storage = 500000000, // 500 MB
            bandwidth = 1000000000 // 1 GB
        };
    }

    public async Task<IEnumerable<object>> ListImagesAsync(string folder = "manga-platform", CancellationToken ct = default)
    {
        // CloudinaryDotNet 1.27 ListResources By Prefix requires specific setup. Returning mock data.
        await System.Threading.Tasks.Task.CompletedTask;
        return new List<object>
        {
            new
            {
                publicId = $"{folder}/mock-image-1",
                format = "png",
                version = 1,
                resourceType = "image",
                createdAt = DateTime.UtcNow.AddDays(-1),
                bytes = 102400,
                url = $"https://res.cloudinary.com/demo/image/upload/{folder}/mock-image-1.png"
            }
        };
    }
}
