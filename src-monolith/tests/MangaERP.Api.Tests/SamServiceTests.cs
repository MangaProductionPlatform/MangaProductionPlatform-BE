using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using STTask = System.Threading.Tasks.Task;
using MangaERP.Api.Models.Sam;
using MangaERP.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

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

    private static IServiceScopeFactory CreateMockScopeFactory(Action<AppDbContext>? seedAction = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);
        
        seedAction?.Invoke(dbContext);
        dbContext.SaveChanges();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(AppDbContext)))
            .Returns(dbContext);

        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock
            .Setup(x => x.ServiceProvider)
            .Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(x => x.CreateScope())
            .Returns(serviceScopeMock.Object);

        return scopeFactoryMock.Object;
    }

    private static IServiceProvider CreateServiceProviderWithPolly(HttpMessageHandler httpMessageHandler, AppDbContext dbContext)
    {
        var services = new ServiceCollection();

        services.AddSingleton(dbContext);
        
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(AppDbContext)))
            .Returns(dbContext);
        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(x => x.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(serviceScopeMock.Object);
        
        services.AddSingleton(scopeFactoryMock.Object);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");
        configMock.Setup(c => c["SamService:InternalApiKey"]).Returns("fake-key");
        services.AddSingleton<IConfiguration>(configMock.Object);
        services.AddLogging();

        services.AddHttpClient<SamServiceClient>(client =>
        {
            client.BaseAddress = new Uri("https://fake-sam-service.test");
            client.Timeout = TimeSpan.FromSeconds(180);
        })
        .ConfigurePrimaryHttpMessageHandler(() => httpMessageHandler)
        .AddPolicyHandler(Program.GetRetryPolicy())
        .AddPolicyHandler(Program.GetCircuitBreakerPolicy());

        return services.BuildServiceProvider();
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
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:InternalApiKey"]).Returns((string)null);
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");

        var scopeFactory = CreateMockScopeFactory();
        var client = new SamServiceClient(CreateMockedHttpClient(expected), NullLogger<SamServiceClient>.Instance, configMock.Object, scopeFactory);

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
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:InternalApiKey"]).Returns((string)null);
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");

        var scopeFactory = CreateMockScopeFactory();
        var client = new SamServiceClient(
            CreateMockedHttpClient(new { error = "Internal Server Error" }, HttpStatusCode.InternalServerError),
            NullLogger<SamServiceClient>.Instance,
            configMock.Object,
            scopeFactory);

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
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:InternalApiKey"]).Returns((string)null);
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");

        var scopeFactory = CreateMockScopeFactory();
        var client = new SamServiceClient(CreateMockedHttpClient(expected), NullLogger<SamServiceClient>.Instance, configMock.Object, scopeFactory);

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
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:InternalApiKey"]).Returns((string)null);
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");

        var scopeFactory = CreateMockScopeFactory();
        var client = new SamServiceClient(
            CreateMockedHttpClient(new { }, HttpStatusCode.ServiceUnavailable),
            NullLogger<SamServiceClient>.Instance,
            configMock.Object,
            scopeFactory);

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

    // ── Polly Policy Tests ────────────────────────────────────────────────────

    [Fact]
    public async STTask CheckHealthAsync_TransientError_RetriesHttpRequest()
    {
        // Arrange
        int requestCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                requestCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);

        var serviceProvider = CreateServiceProviderWithPolly(handlerMock.Object, dbContext);
        var client = serviceProvider.GetRequiredService<SamServiceClient>();

        // Act & Assert
        // retry 2 lần + 1 lần đầu = 3 requests
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CheckHealthAsync(CancellationToken.None));

        Assert.Equal(3, requestCount);
    }

    [Fact]
    public async STTask CheckHealthAsync_ConsecutiveFailures_BreaksCircuit()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);

        var serviceProvider = CreateServiceProviderWithPolly(handlerMock.Object, dbContext);
        var client = serviceProvider.GetRequiredService<SamServiceClient>();

        // Act & Assert
        // Lần gọi thứ 1: Gửi 1 request gốc + 2 retries = 3 failures liên tiếp. Ném HttpRequestException.
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CheckHealthAsync(CancellationToken.None));

        // Lần gọi thứ 2: Gửi request tiếp theo -> gặp failure thứ 4 -> Circuit OPEN và ném BrokenCircuitException lập tức.
        await Assert.ThrowsAnyAsync<Polly.CircuitBreaker.BrokenCircuitException>(() =>
            client.CheckHealthAsync(CancellationToken.None));
    }
}
