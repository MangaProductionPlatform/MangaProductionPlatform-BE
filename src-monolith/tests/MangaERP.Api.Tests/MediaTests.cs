using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Threading;
using STTask = System.Threading.Tasks.Task;
using Xunit;
using MangaERP.Api.Controllers;
using MangaERP.Api.Services;

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

    private static MediaController CreateController(ICloudinaryService? cloudinaryService = null)
    {
        cloudinaryService ??= Mock.Of<ICloudinaryService>();
        return new MediaController(cloudinaryService, NullLogger<MediaController>.Instance);
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
}
