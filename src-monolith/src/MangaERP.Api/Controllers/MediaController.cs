using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MediaController> _logger;

    // TODO: migrate sang S3 pre-signed URL trước production thật — xem gap_completion_plan.md, guardrail 1.1
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

    public MediaController(IWebHostEnvironment env, ILogger<MediaController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Upload a file (cover image, or page artwork).
    /// All uploads are strictly private by default and saved to App_Data/uploads/private/.
    /// Public movement is handled by the system later (e.g. on publish).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(30 * 1024 * 1024)] // Limit up to 30MB
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Không tìm thấy tệp tải lên hoặc tệp bị rỗng." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = $"Định dạng file không hỗ trợ. Cho phép: {string.Join(", ", AllowedExtensions)}" });
        }

        // Validate Magic Bytes
        if (!await IsValidImageMagicBytesAsync(file))
        {
            return BadRequest(new { message = "Tệp tin hình ảnh không hợp lệ (sai Magic Bytes header)." });
        }

        try
        {
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "private");
            
            var scheme = Request.Scheme;
            var host = Request.Host.ToUriComponent();
            var returnUrl = $"{scheme}://{host}/api/v1/media/{uniqueFileName}/view";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream, ct);
            }

            _logger.LogInformation("File {FileName} uploaded as {UniqueName} (private by default).", file.FileName, uniqueFileName);

            return Ok(new { url = returnUrl, fileKey = uniqueFileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình upload file: {Message}", ex.Message);
            return StatusCode(500, new { message = "Lỗi máy chủ khi lưu trữ file.", details = ex.Message });
        }
    }

    /// <summary>
    /// Serves a private file from App_Data/uploads/private/ via Stream.
    /// </summary>
    [HttpGet("{fileKey}/view")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public IActionResult ViewPrivateFile(string fileKey)
    {
        // TODO: cần bảng metadata fileKey -> uploaderId để check ownership thật, hiện chỉ dựa vào [Authorize] + GUID khó đoán — chấp nhận rủi ro thấp cho demo

        // Simple security check: prevent directory traversal
        if (string.IsNullOrEmpty(fileKey) || fileKey.Contains("..") || Path.GetInvalidFileNameChars().Any(c => fileKey.Contains(c)))
        {
            return BadRequest(new { message = "fileKey không hợp lệ." });
        }

        var privateFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "private");
        var filePath = Path.Combine(privateFolder, fileKey);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Không tìm thấy file yêu cầu." });
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(fileStream, contentType);
    }

    /// <summary>
    /// Đọc các byte đầu (magic bytes) của file để đảm bảo là PNG, JPEG hoặc WEBP thực sự.
    /// </summary>
    private static async Task<bool> IsValidImageMagicBytesAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, 12));
        stream.Position = 0; // Reset để stream đọc lại từ đầu khi lưu file

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
