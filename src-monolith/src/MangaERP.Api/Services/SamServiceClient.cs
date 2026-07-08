using System.Net.Http.Json;
using MangaERP.Api.Models.Sam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaERP.Api.Services;

/// <summary>
/// HTTP client wrapper for the external SAM (Segment Anything Model) Python service.
/// Communicates over HTTP with endpoints: /embedding, /predict, and /health.
/// URL and API Key are resolved dynamically at runtime from the database (SystemConfigs table)
/// falling back to appsettings.json.
/// </summary>
public class SamServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SamServiceClient> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public SamServiceClient(
        HttpClient httpClient, 
        ILogger<SamServiceClient> logger, 
        IConfiguration config,
        IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Lấy cấu hình SAM động từ Database (bảng SystemConfigs), fallback về config tĩnh.
    /// </summary>
    private async Task<(string Url, string ApiKey)> GetSamConfigAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MangaERP.Shared.Infrastructure.Persistence.AppDbContext>();

        var urlConfig = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "SamService:Url", ct);
        var keyConfig = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "SamService:InternalApiKey", ct);

        string url = urlConfig?.Value ?? _config["SamService:Url"] ?? string.Empty;
        string apiKey = keyConfig?.Value ?? _config["SamService:InternalApiKey"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("SAM service URL is not configured. Please configure 'SamService:Url' in appsettings or system configs.");
        }

        return (url, apiKey);
    }

    private void AttachInternalApiKey(HttpRequestMessage req, string apiKey)
    {
        if (!string.IsNullOrEmpty(apiKey))
        {
            req.Headers.Add("X-Internal-Api-Key", apiKey);
        }
    }

    /// <summary>
    /// Check if the external SAM service is online and healthy.
    /// Throws exceptions on failure (including transient errors, timeout, or circuit break)
    /// to let the controller handle it appropriately.
    /// </summary>
    public async System.Threading.Tasks.Task CheckHealthAsync(CancellationToken ct)
    {
        var (samUrl, apiKey) = await GetSamConfigAsync(ct);
        var targetUri = new Uri(new Uri(samUrl.TrimEnd('/') + "/"), "health");

        using var req = new HttpRequestMessage(HttpMethod.Get, targetUri);
        AttachInternalApiKey(req, apiKey);

        // Spec 3: Timeout riêng ~8s cho health check
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        using var response = await _httpClient.SendAsync(req, cts.Token);
        response.EnsureSuccessStatusCode();
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
        var (samUrl, apiKey) = await GetSamConfigAsync(ct);
        var targetUri = new Uri(new Uri(samUrl.TrimEnd('/') + "/"), "embedding");

        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            file.ContentType ?? "image/png");
        form.Add(fileContent, "file", file.FileName);

        using var req = new HttpRequestMessage(HttpMethod.Post, targetUri) { Content = form };
        AttachInternalApiKey(req, apiKey);

        _logger.LogInformation("[SAM] Requesting embedding for file: {FileName} ({Size} bytes)",
            file.FileName, file.Length);

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SAM /embedding returned an empty response.");

        _logger.LogInformation("[SAM] Embedding received. Shape: [{Shape}]",
            string.Join(", ", result.Shape));

        return result;
    }

    /// <summary>
    /// Checks the validity of an uploaded image by sending it to the SAM service to generate its embedding.
    /// Calls POST /embedding on the SAM Python service using the image downloaded from storage.
    /// </summary>
    /// <param name="fileKey">The public ID/key of the uploaded image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Embedding response if successful.</returns>
    public async Task<EmbeddingResponse> GetEmbeddingAsync(string fileKey, CancellationToken ct = default)
    {
        // NOTE: basic validity check only, not full content moderation
        var (samUrl, apiKey) = await GetSamConfigAsync(ct);
        var targetUri = new Uri(new Uri(samUrl.TrimEnd('/') + "/"), "embedding");

        var cloudName = _config["Cloudinary__CloudName"] ?? _config["Cloudinary:CloudName"];
        if (string.IsNullOrEmpty(cloudName))
        {
            throw new InvalidOperationException("Cloudinary cloud name is not configured.");
        }

        var imageUrl = $"https://res.cloudinary.com/{cloudName}/image/upload/{fileKey}";

        // Download image bytes
        byte[] imageBytes;
        try
        {
            using var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            downloadCts.CancelAfter(TimeSpan.FromSeconds(15));
            imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, downloadCts.Token);
        }
        catch (Exception ex) when (ex is Polly.CircuitBreaker.BrokenCircuitException ||
                                   ex.InnerException is Polly.CircuitBreaker.BrokenCircuitException ||
                                   (ex is HttpRequestException hre && hre.InnerException is Polly.CircuitBreaker.BrokenCircuitException))
        {
            if (ex is Polly.CircuitBreaker.BrokenCircuitException) throw;
            throw ex.InnerException!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SAM] Validation fail - Image download failed for fileKey: {FileKey}. Error: {Error}", fileKey, ex.Message);
            throw new InvalidOperationException($"Failed to download image from storage for key: {fileKey}", ex);
        }

        // Retry logic: timeout 10-15s, retry max 2-3 times
        int maxAttempts = 3; // 1 initial attempt + 2 retries
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(15)); // Timeout 15s per attempt

                using var form = new MultipartFormDataContent();
                using var stream = new MemoryStream(imageBytes);
                using var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                form.Add(fileContent, "file", "image.png");

                using var req = new HttpRequestMessage(HttpMethod.Post, targetUri) { Content = form };
                AttachInternalApiKey(req, apiKey);

                // Note: Only log pass/fail + fileKey, do NOT log full URL or raw response
                using var response = await _httpClient.SendAsync(req, cts.Token);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cts.Token)
                    ?? throw new InvalidOperationException("SAM /embedding returned an empty response.");

                _logger.LogInformation("[SAM] Validation pass for fileKey: {FileKey}", fileKey);
                return result;
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning("[SAM] Attempt {Attempt} failed for fileKey: {FileKey}. Error: {Error}", attempt, fileKey, ex.Message);
                
                if (attempt < maxAttempts)
                {
                    // Delay before retry
                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }
        }

        _logger.LogWarning("[SAM] Validation fail for fileKey: {FileKey}. Final error: {Error}", fileKey, lastException?.Message);
        throw lastException ?? new InvalidOperationException("Failed to get embedding from SAM service.");
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
        var (samUrl, apiKey) = await GetSamConfigAsync(ct);
        var targetUri = new Uri(new Uri(samUrl.TrimEnd('/') + "/"), "predict");

        _logger.LogInformation("[SAM] Requesting mask prediction at ({X}, {Y})", request.X, request.Y);

        using var req = new HttpRequestMessage(HttpMethod.Post, targetUri)
        {
            Content = JsonContent.Create(request)
        };
        AttachInternalApiKey(req, apiKey);

        var response = await _httpClient.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MaskResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("SAM /predict returned an empty response.");

        _logger.LogInformation("[SAM] Mask received. Score: {Score}", result.Score);

        return result;
    }
}
