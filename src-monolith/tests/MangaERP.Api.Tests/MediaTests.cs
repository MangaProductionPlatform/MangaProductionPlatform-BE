using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using System.Net;
using System.Threading;
using STTask = System.Threading.Tasks.Task;
using Xunit;
using MangaERP.Api.Controllers;
using MangaERP.Api.Services;
using Microsoft.EntityFrameworkCore;
using MangaERP.Shared.Infrastructure.Persistence;

namespace MangaERP.Api.Tests;

public class MediaTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<IFormFile> CreateMockFormFile(string fileName, byte[] content)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return fileMock;
    }

    private static IServiceScopeFactory CreateMockScopeFactoryForDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options);
        
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(AppDbContext)))
            .Returns(dbContext);
            
        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(x => x.ServiceProvider).Returns(serviceProviderMock.Object);
        
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(serviceScopeMock.Object);
        
        return scopeFactoryMock.Object;
    }

    private static MediaController CreateController(
        ICloudinaryService? cloudinaryService = null,
        SamServiceClient? samServiceClient = null)
    {
        cloudinaryService ??= Mock.Of<ICloudinaryService>();
        if (samServiceClient == null)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://fake-sam-service.test")
            };
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");
            configMock.Setup(c => c["Cloudinary:CloudName"]).Returns("demo");
            configMock.Setup(c => c["Cloudinary__CloudName"]).Returns("demo");
            
            var scopeFactory = CreateMockScopeFactoryForDb();

            samServiceClient = new SamServiceClient(
                httpClient,
                NullLogger<SamServiceClient>.Instance,
                configMock.Object,
                scopeFactory);
        }
        return new MediaController(cloudinaryService, samServiceClient, NullLogger<MediaController>.Instance);
    }

    // ── Extension Whitelist Tests ─────────────────────────────────────────────

    [Theory]
    [InlineData("file.zip")]
    [InlineData("file.rar")]
    [InlineData("file.pdf")]
    [InlineData("file.txt")]
    [InlineData("file.exe")]
    public async STTask UploadFile_ShouldRejectForbiddenExtensions(string fileName)
    {
        // Arrange
        var controller = CreateController();
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG magic bytes
        var fileMock = CreateMockFormFile(fileName, content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Định dạng file không hỗ trợ", bad.Value!.ToString());
    }

    // ── Magic Bytes Validation Tests ──────────────────────────────────────────

    [Fact]
    public async STTask UploadFile_ShouldRejectExeRenamedToPng()
    {
        // Arrange: MZ header (EXE) disguised as .png
        var content = new byte[] { 0x4D, 0x5A, 0x90, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var controller = CreateController();
        var fileMock = CreateMockFormFile("dangerous.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Tệp tin hình ảnh không hợp lệ", bad.Value!.ToString());
    }

    [Theory]
    [InlineData("valid.png",  new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 })] // PNG
    [InlineData("valid.jpg",  new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 })] // JPEG
    [InlineData("valid.webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })] // WEBP
    public async STTask UploadFile_ShouldAcceptValidMagicBytes_AndCallCloudinary(string fileName, byte[] content)
    {
        // Arrange
        var cloudinaryMock = new Mock<ICloudinaryService>();
        cloudinaryMock
            .Setup(c => c.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudinaryUploadResult(
                SecureUrl: "https://res.cloudinary.com/demo/image/upload/manga-platform/abc123.png",
                PublicId:  "manga-platform/abc123"));

        var controller = CreateController(cloudinaryMock.Object);
        var fileMock   = CreateMockFormFile(fileName, content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert: 200 OK returned
        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        var doc  = System.Text.Json.JsonDocument.Parse(json);

        Assert.Contains("res.cloudinary.com", doc.RootElement.GetProperty("url").GetString());
        Assert.Contains("manga-platform/abc123", doc.RootElement.GetProperty("fileKey").GetString());

        // Assert: Cloudinary was actually called once
        cloudinaryMock.Verify(c =>
            c.UploadImageAsync(It.IsAny<Stream>(), fileName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Cloudinary Error Handling Tests ───────────────────────────────────────

    [Fact]
    public async STTask UploadFile_ShouldReturn500_WhenCloudinaryFails()
    {
        // Arrange
        var cloudinaryMock = new Mock<ICloudinaryService>();
        cloudinaryMock
            .Setup(c => c.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cloudinary upload thất bại: Invalid API key"));

        var controller = CreateController(cloudinaryMock.Object);
        var content    = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG
        var fileMock   = CreateMockFormFile("ok.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert: 500 returned with meaningful message
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
        Assert.Contains("Cloudinary", status.Value!.ToString());
    }

    // ── Empty/Null File Tests ─────────────────────────────────────────────────

    [Fact]
    public async STTask UploadFile_ShouldReturn400_WhenFileIsNull()
    {
        var controller = CreateController();
        var result = await controller.UploadFile(null!, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Không tìm thấy tệp", bad.Value!.ToString());
    }

    [Fact]
    public async STTask UploadFile_ShouldReturn400_WhenFileIsEmpty()
    {
        var controller = CreateController();
        var fileMock   = CreateMockFormFile("empty.png", Array.Empty<byte>());

        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Không tìm thấy tệp", bad.Value!.ToString());
    }

    // ── SAM Validation Tests ──────────────────────────────────────────────────

    [Fact]
    public async STTask UploadFile_ShouldCallSamValidationAndSucceed_WhenSamSucceeds()
    {
        // Arrange
        var cloudinaryMock = new Mock<ICloudinaryService>();
        cloudinaryMock
            .Setup(c => c.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudinaryUploadResult(
                SecureUrl: "https://res.cloudinary.com/demo/image/upload/manga-platform/abc123.png",
                PublicId:  "manga-platform/abc123"));

        // Mock HttpMessageHandler for SamServiceClient
        var samResponseJson = "{\"embedding\": \"data\", \"shape\": [1, 256, 64, 64], \"dtype\": \"float32\", \"image_size\": [1024, 1024]}";
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        
        // 1. Setup GET request for Cloudinary image download
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("cloudinary")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[100])
            });

        // 2. Setup POST request for SAM embedding
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("embedding")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(samResponseJson, System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://fake-sam-service.test")
        };
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");
        configMock.Setup(c => c["Cloudinary:CloudName"]).Returns("demo");
        configMock.Setup(c => c["Cloudinary__CloudName"]).Returns("demo");
        
        var scopeFactory = CreateMockScopeFactoryForDb();

        var samServiceClient = new SamServiceClient(
            httpClient,
            NullLogger<SamServiceClient>.Instance,
            configMock.Object,
            scopeFactory);

        var controller = CreateController(cloudinaryMock.Object, samServiceClient);
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG
        var fileMock = CreateMockFormFile("test.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async STTask UploadFile_ShouldNotBlockUpload_WhenSamThrowsException()
    {
        // Arrange
        var cloudinaryMock = new Mock<ICloudinaryService>();
        cloudinaryMock
            .Setup(c => c.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudinaryUploadResult(
                SecureUrl: "https://res.cloudinary.com/demo/image/upload/manga-platform/abc123.png",
                PublicId:  "manga-platform/abc123"));

        // Mock HttpMessageHandler to throw Exception on SAM call
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        
        // 1. Setup GET request for Cloudinary image download
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("cloudinary")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[100])
            });

        // 2. Setup POST request to fail
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Colab service is down"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://fake-sam-service.test")
        };
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["SamService:Url"]).Returns("https://fake-sam-service.test");
        configMock.Setup(c => c["Cloudinary:CloudName"]).Returns("demo");
        configMock.Setup(c => c["Cloudinary__CloudName"]).Returns("demo");
        
        var scopeFactory = CreateMockScopeFactoryForDb();

        var samServiceClient = new SamServiceClient(
            httpClient,
            NullLogger<SamServiceClient>.Instance,
            configMock.Object,
            scopeFactory);

        var controller = CreateController(cloudinaryMock.Object, samServiceClient);
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG
        var fileMock = CreateMockFormFile("test.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert: The upload succeeds even when the SAM service call fails
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async STTask UploadFile_ShouldTriggerCircuitBreakerAndNotBlockUpload_WhenColabFailsRepeatedly()
    {
        // Arrange
        var services = new ServiceCollection();
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        
        // 1. Setup GET request for Cloudinary image download
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("cloudinary")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[100])
            });

        // 2. Setup POST request to fail with InternalServerError (500)
        handlerMock
            .Protected()
            .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("embedding")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SamService:Url", "https://fake-sam-service.test" },
                { "Cloudinary:CloudName", "demo" },
                { "Cloudinary__CloudName", "demo" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        services.AddScoped<AppDbContext>(_ => new AppDbContext(options));

        services.AddHttpClient<SamServiceClient>(client =>
        {
            client.BaseAddress = new Uri("https://fake-sam-service.test");
        })
        .ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object)
        .AddPolicyHandler(Program.GetRetryPolicy())
        .AddPolicyHandler(Program.GetCircuitBreakerPolicy());

        var serviceProvider = services.BuildServiceProvider();
        var samServiceClient = serviceProvider.GetRequiredService<SamServiceClient>();
        
        var cloudinaryMock = new Mock<ICloudinaryService>();
        cloudinaryMock
            .Setup(c => c.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CloudinaryUploadResult(
                SecureUrl: "https://res.cloudinary.com/demo/image/upload/manga-platform/abc123.png",
                PublicId:  "manga-platform/abc123"));

        var controller = new MediaController(cloudinaryMock.Object, samServiceClient, NullLogger<MediaController>.Instance);
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG

        // Act & Assert: Call UploadFile 5 times.
        // The circuit breaker is set to trigger after 4 consecutive failures.
        // During these failures, MediaController catches the exception/broken circuit and returns OK 200.
        for (int i = 0; i < 5; i++)
        {
            var fileMock = CreateMockFormFile($"test_{i}.png", content);
            var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // Verify that a direct call to GetEmbeddingAsync now immediately throws BrokenCircuitException
        await Assert.ThrowsAnyAsync<Polly.CircuitBreaker.BrokenCircuitException>(async () =>
        {
            await samServiceClient.GetEmbeddingAsync("manga-platform/abc123", CancellationToken.None);
        });
    }
}
