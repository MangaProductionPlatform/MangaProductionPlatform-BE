using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MangaERP.Api.Services;

public interface ICloudinaryService
{
    /// <summary>
    /// Upload một file ảnh lên Cloudinary và trả về URL công khai (secure_url) + public_id.
    /// </summary>
    Task<CloudinaryUploadResult> UploadImageAsync(Stream stream, string fileName, CancellationToken ct = default);
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
}
