using MangaERP.Api.Models.Sam;
using MangaERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// Upload an image and receive its SAM embedding for later use in /predict.
    /// </summary>
    /// <remarks>
    /// **Request**: multipart/form-data with a single image file field named "file".<br/>
    /// **Response**: Embedding tensor (base-64), shape, dtype, and original image size.
    /// </remarks>
    [HttpPost("embedding")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EmbeddingResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(502)] // SAM service unreachable
    public async Task<IActionResult> GetEmbedding(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType?.ToLower()))
            return BadRequest(new { message = "Only PNG, JPEG, or WebP images are supported." });

        try
        {
            var result = await _samClient.GetEmbeddingAsync(file, ct);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[SAM] Embedding request failed.");
            return StatusCode(502, new { message = "SAM service is unavailable.", detail = ex.Message });
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
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[SAM] Predict request failed.");
            return StatusCode(502, new { message = "SAM service is unavailable.", detail = ex.Message });
        }
    }
}
