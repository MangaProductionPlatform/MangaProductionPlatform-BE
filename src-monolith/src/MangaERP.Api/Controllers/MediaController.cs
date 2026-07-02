using MangaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly ICloudinaryService _cloudinary;
    private readonly SamServiceClient _samServiceClient;
    private readonly ILogger<MediaController> _logger;

    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

    public MediaController(
        ICloudinaryService cloudinary, 
        SamServiceClient samServiceClient, 
        ILogger<MediaController> logger)
    {
        _cloudinary = cloudinary;
        _samServiceClient = samServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file (cover image, page artwork, or any image asset) lên Cloudinary.
    /// Trả về secure_url (HTTPS CDN URL công khai) và publicId.
    /// </summary>
    /// <remarks>
    /// **Response:**
    /// ```json
    /// { "url": "https://res.cloudinary.com/...", "fileKey": "manga-platform/abc123" }
    /// ```
    /// </remarks>
    [HttpPost("upload")]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30MB
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Không tìm thấy tệp tải lên hoặc tệp bị rỗng." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            return BadRequest(new { message = $"Định dạng file không hỗ trợ. Cho phép: {string.Join(", ", AllowedExtensions)}" });

        // Validate Magic Bytes — bảo vệ chống extension spoofing
        if (!await IsValidImageMagicBytesAsync(file))
            return BadRequest(new { message = "Tệp tin hình ảnh không hợp lệ (sai Magic Bytes header)." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _cloudinary.UploadImageAsync(stream, file.FileName, ct);

            _logger.LogInformation(
                "File {OriginalName} uploaded to Cloudinary → {PublicId}",
                file.FileName, result.PublicId);

            // Gọi SamServiceClient.GetEmbeddingAsync(fileKey) để validate ảnh trước khi cho phép client sử dụng cho publishing
            try
            {
                // NOTE: basic validity check only, not full content moderation
                await _samServiceClient.GetEmbeddingAsync(result.PublicId, ct);
            }
            catch (Exception ex)
            {
                // Nếu gọi Colab fail (timeout/exception) → log mức Warning (không phải Error), không chặn luồng publish chính, tiếp tục xử lý bình thường.
                _logger.LogWarning(ex, "SAM service embedding check failed for fileKey: {FileKey}. Continuing publish flow normally.", result.PublicId);
            }

            return Ok(new
            {
                url     = result.SecureUrl,
                fileKey = result.PublicId
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Cloudinary upload failed for {FileName}", file.FileName);
            return StatusCode(500, new { message = "Lỗi khi tải ảnh lên Cloudinary.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during upload of {FileName}", file.FileName);
            return StatusCode(500, new { message = "Lỗi máy chủ không mong đợi.", details = ex.Message });
        }
    }

    /// <summary>
    /// Đọc các byte đầu (magic bytes) của file để đảm bảo là PNG, JPEG hoặc WEBP thực sự.
    /// </summary>
    private static async Task<bool> IsValidImageMagicBytesAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, 12));

        if (read < 4) return false;

        // PNG: 89 50 4E 47
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

        // JPEG: FF D8 FF
        bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

        // WEBP: RIFF....WEBP
        bool isWebp = read >= 12 &&
            header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P';

        return isPng || isJpeg || isWebp;
    }
}
