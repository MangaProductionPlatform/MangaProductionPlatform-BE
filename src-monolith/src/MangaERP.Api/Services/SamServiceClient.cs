using System.Net.Http.Json;
using MangaERP.Api.Models.Sam;

namespace MangaERP.Api.Services;

/// <summary>
/// HTTP client wrapper for the external SAM (Segment Anything Model) Python service.
/// Communicates over HTTP with two endpoints: /embedding and /predict.
/// The base URL is configured via "SamService:Url" in appsettings.json.
/// </summary>
public class SamServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SamServiceClient> _logger;

    public SamServiceClient(HttpClient httpClient, ILogger<SamServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends an image file to the SAM service and retrieves its image embedding.
    /// Calls POST /embedding on the SAM Python service.
    /// </summary>
    /// <param name="file">The image file uploaded by the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Embedding tensor data and image metadata.</returns>
    public async Task<EmbeddingResponse> GetEmbeddingAsync(IFormFile file, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            file.ContentType ?? "image/png");
        form.Add(fileContent, "file", file.FileName);

        _logger.LogInformation("[SAM] Requesting embedding for file: {FileName} ({Size} bytes)",
            file.FileName, file.Length);

        var response = await _httpClient.PostAsync("/embedding", form, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SAM /embedding returned an empty response.");

        _logger.LogInformation("[SAM] Embedding received. Shape: [{Shape}]",
            string.Join(", ", result.Shape));

        return result;
    }

    /// <summary>
    /// Sends a pre-computed embedding and a click point to the SAM service
    /// and retrieves the predicted segmentation mask.
    /// Calls POST /predict on the SAM Python service.
    /// </summary>
    /// <param name="request">Embedding data plus click coordinates (x, y).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Mask RLE, confidence score, and bounding box.</returns>
    public async Task<MaskResponse> PredictMaskAsync(PredictRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[SAM] Requesting mask prediction at ({X}, {Y})", request.X, request.Y);

        var response = await _httpClient.PostAsJsonAsync("/predict", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MaskResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SAM /predict returned an empty response.");

        _logger.LogInformation("[SAM] Mask received. Score: {Score}", result.Score);

        return result;
    }
}
