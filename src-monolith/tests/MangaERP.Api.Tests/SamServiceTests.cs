using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
// Use fully-qualified Task<> everywhere to avoid clash with MangaERP.Task namespace
using STTask = System.Threading.Tasks.Task;
using MangaERP.Api.Models.Sam;
using MangaERP.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace MangaERP.Api.Tests;

/// <summary>
/// Unit tests for <see cref="SamServiceClient"/>.
/// HttpClient is mocked via HttpMessageHandler so no real HTTP calls are made.
/// </summary>
public class SamServiceClientTests
{
    // ── helpers ──────────────────────────────────────────────────────────────────

    private static HttpClient CreateMockedHttpClient(object responsePayload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content    = new StringContent(json, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://fake-sam-service.test")
        };
    }

    private static Mock<IFormFile> CreateFileMock()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.png");
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.ContentType).Returns("image/png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
        return fileMock;
    }

    // ── GetEmbeddingAsync ─────────────────────────────────────────────────────

    [Fact]
    public async STTask GetEmbeddingAsync_ValidFile_ReturnsEmbeddingResponse()
    {
        // Arrange
        var expected = new EmbeddingResponse
        {
            Embedding = "base64encodeddata==",
            Shape     = [1, 256, 64, 64],
            Dtype     = "float32",
            ImageSize = [1024, 1024]
        };
        var client = new SamServiceClient(CreateMockedHttpClient(expected), NullLogger<SamServiceClient>.Instance);

        // Act
        var result = await client.GetEmbeddingAsync(CreateFileMock().Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("base64encodeddata==", result.Embedding);
        Assert.Equal([1, 256, 64, 64], result.Shape);
        Assert.Equal("float32", result.Dtype);
        Assert.Equal([1024, 1024], result.ImageSize);
    }

    [Fact]
    public async STTask GetEmbeddingAsync_ServiceReturns500_ThrowsHttpRequestException()
    {
        // Arrange
        var client = new SamServiceClient(
            CreateMockedHttpClient(new { error = "Internal Server Error" }, HttpStatusCode.InternalServerError),
            NullLogger<SamServiceClient>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetEmbeddingAsync(CreateFileMock().Object));
    }

    // ── PredictMaskAsync ──────────────────────────────────────────────────────

    [Fact]
    public async STTask PredictMaskAsync_ValidRequest_ReturnsMaskResponse()
    {
        // Arrange
        var expected = new MaskResponse
        {
            MaskRle = new { counts = "abc123", size = new[] { 1024, 1024 } },
            Score   = 0.95f,
            Bbox    = [100, 200, 300, 400]
        };
        var client = new SamServiceClient(CreateMockedHttpClient(expected), NullLogger<SamServiceClient>.Instance);

        var request = new PredictRequest
        {
            Embedding = "base64encodeddata==",
            Shape     = [1, 256, 64, 64],
            Dtype     = "float32",
            ImageSize = [1024, 1024],
            X = 512f, Y = 512f
        };

        // Act
        var result = await client.PredictMaskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.95f, result.Score);
        Assert.Equal([100, 200, 300, 400], result.Bbox);
    }

    [Fact]
    public async STTask PredictMaskAsync_ServiceUnavailable_ThrowsHttpRequestException()
    {
        // Arrange
        var client = new SamServiceClient(
            CreateMockedHttpClient(new { }, HttpStatusCode.ServiceUnavailable),
            NullLogger<SamServiceClient>.Instance);

        var request = new PredictRequest
        {
            Embedding = "data==",
            Shape     = [1, 256, 64, 64],
            Dtype     = "float32",
            ImageSize = [1024, 1024],
            X = 100f, Y = 100f
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PredictMaskAsync(request));
    }
}
