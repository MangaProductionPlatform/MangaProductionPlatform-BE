using MangaERP.Api.Models.Sam;
using MangaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;

namespace MangaERP.Api.Controllers;

/// <summary>
/// Proxies segmentation requests to the external SAM Python service.
/// These endpoints are intentionally lightweight — they simply forward data
/// to/from the Python service and do not perform any business logic.
/// </summary>
[ApiController]
[Route("api/segmentation")]
[Authorize]
public class SegmentationController : ControllerBase
{
    private readonly SamServiceClient _samClient;
    private readonly ILogger<SegmentationController> _logger;

    public SegmentationController(SamServiceClient samClient, ILogger<SegmentationController> logger)
    {
        _samClient = samClient;
        _logger = logger;
    }

    /// <summary>
    /// Check if SAM service is available. Returns health details without leaking backend URL.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        try
        {
            await _samClient.CheckHealthAsync(ct);
            return Ok(new { sam = "healthy" });
        }
        catch (BrokenCircuitException)
        {
            return Ok(new { sam = "unavailable", reason = "circuit_open" });
        }
        catch (OperationCanceledException)
        {
            return Ok(new { sam = "unavailable", reason = "timeout" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SAM] Health check request failed.");
            return Ok(new { sam = "unavailable", reason = "failed_health_check" });
        }
    }

    /// <summary>
    /// Upload an image and receive its SAM embedding for later use in /predict.
    /// </summary>
    /// <remarks>
    /// **Request**: multipart/form-data with a single image file field named "file".<br/>
    /// **Response**: Embedding tensor (base-64), shape, dtype, and original image size.
    /// </remarks>
    [HttpPost("embedding")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(15 * 1024 * 1024)] // Hạn chế upload quá lớn ở host (guardrail 1.1)
    [ProducesResponseType(typeof(EmbeddingResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(502)] // SAM service unreachable
    [ProducesResponseType(503)] // Circuit breaker open
    [ProducesResponseType(504)] // Timeout
    public async Task<IActionResult> GetEmbedding(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        const long maxBytes = 15 * 1024 * 1024; // 15MB max (guardrail 1.1)
        if (file.Length > maxBytes)
            return BadRequest(new { message = "File too large. Max 15MB limit exceeded." });

        // Guardrail 6.4: Validate Magic Bytes để tránh giả mạo Content-Type
        if (!await IsValidImageMagicBytesAsync(file))
            return BadRequest(new { message = "Unsupported image format or invalid file signature." });

        try
        {
            var result = await _samClient.GetEmbeddingAsync(file, ct);
            return Ok(result);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "[SAM] Circuit is OPEN. Embedding request blocked.");
            return StatusCode(503, new { message = "SAM service tạm ngưng do lỗi liên tục (có thể Colab đã hết phiên), vui lòng thử lại sau 2 phút." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[SAM] Embedding request failed.");
            return StatusCode(502, new { message = "SAM service is unavailable.", detail = ex.Message });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "[SAM] Embedding request timed out.");
            return StatusCode(504, new { message = "SAM service request timed out." });
        }
    }

    /// <summary>
    /// Given a pre-computed embedding and a click coordinate, predict a segmentation mask.
    /// </summary>
    /// <remarks>
    /// **Request**: JSON body with the embedding fields (from /embedding response) plus x, y click point.<br/>
    /// **Response**: Mask in RLE format, confidence score, and bounding box.
    /// </remarks>
    [HttpPost("predict")]
    [ProducesResponseType(typeof(MaskResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(502)]
    [ProducesResponseType(503)]
    [ProducesResponseType(504)]
    public async Task<IActionResult> PredictMask([FromBody] PredictRequest request, CancellationToken ct)
    {
        if (request.Shape is null || request.Shape.Length == 0)
            return BadRequest(new { message = "Embedding shape is required." });

        if (string.IsNullOrWhiteSpace(request.Embedding))
            return BadRequest(new { message = "Embedding data is required." });

        try
        {
            var result = await _samClient.PredictMaskAsync(request, ct);
            return Ok(result);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "[SAM] Circuit is OPEN. Predict request blocked.");
            return StatusCode(503, new { message = "SAM service tạm ngưng do lỗi liên tục (có thể Colab đã hết phiên), vui lòng thử lại sau 2 phút." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[SAM] Predict request failed.");
            return StatusCode(502, new { message = "SAM service is unavailable.", detail = ex.Message });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "[SAM] Predict request timed out.");
            return StatusCode(504, new { message = "SAM service request timed out." });
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
        stream.Position = 0; // Reset để stream đọc lại được từ đầu ở SamServiceClient

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
