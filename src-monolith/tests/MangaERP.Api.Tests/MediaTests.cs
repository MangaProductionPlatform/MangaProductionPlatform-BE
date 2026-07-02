using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using STTask = System.Threading.Tasks.Task;
using Xunit;
using MangaERP.Api.Controllers;

namespace MangaERP.Api.Tests;

public class MediaTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly Mock<IWebHostEnvironment> _envMock;

    public MediaTests()
    {
        // Use a unique temp folder for testing uploads to avoid polluting real files
        _tempTestDir = Path.Combine(Directory.GetCurrentDirectory(), $"TempTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempTestDir);

        _envMock = new Mock<IWebHostEnvironment>();
        _envMock.Setup(e => e.WebRootPath).Returns(Path.Combine(_tempTestDir, "wwwroot"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempTestDir))
        {
            Directory.Delete(_tempTestDir, true);
        }
    }

    private static Mock<IFormFile> CreateMockFormFile(string fileName, byte[] content)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(content));
        return fileMock;
    }

    private MediaController CreateController()
    {
        var controller = new MediaController(_envMock.Object, NullLogger<MediaController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost", 8080);
        return controller;
    }

    [Theory]
    [InlineData("file.zip")]
    [InlineData("file.rar")]
    [InlineData("file.pdf")]
    [InlineData("file.txt")]
    public async STTask UploadFile_ShouldRejectForbiddenExtensions(string fileName)
    {
        // Arrange
        var controller = CreateController();
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }; // PNG magic bytes
        var fileMock = CreateMockFormFile(fileName, content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Định dạng file không hỗ trợ", badRequestResult.Value.ToString());
    }

    [Fact]
    public async STTask UploadFile_ShouldRejectFakeHeaderRenamedFiles()
    {
        // Arrange
        var controller = CreateController();
        // Rename .exe (MZ magic bytes) to .png
        var content = new byte[] { 0x4D, 0x5A, 0x90, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // Exe header
        var fileMock = CreateMockFormFile("dangerous.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Tệp tin hình ảnh không hợp lệ", badRequestResult.Value.ToString());
    }

    [Fact]
    public async STTask UploadFile_ShouldSaveToPrivateFolderOutsideWwwroot_ByDefault()
    {
        // Arrange
        var controller = CreateController();
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }; // Valid JPEG
        var fileMock = CreateMockFormFile("manuscript.png", content);

        // Act
        var result = await controller.UploadFile(fileMock.Object, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        string url = doc.RootElement.GetProperty("url").GetString();
        string fileKey = doc.RootElement.GetProperty("fileKey").GetString();

        Assert.Contains("http://localhost:8080/api/v1/media/", url);
        Assert.Contains("/view", url);

        // Verify the file physically exists in the private directory (App_Data/uploads/private)
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "private", fileKey);
        Assert.True(File.Exists(expectedPath));

        // Cleanup the created private file
        if (File.Exists(expectedPath))
        {
            File.Delete(expectedPath);
        }
    }

    [Fact]
    public void ViewPrivateFile_ShouldReturnStream_ForValidPrivateFile()
    {
        // Arrange
        var controller = CreateController();
        var privateFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "private");
        if (!Directory.Exists(privateFolder))
        {
            Directory.CreateDirectory(privateFolder);
        }

        var testFileKey = $"{Guid.NewGuid()}.png";
        var filePath = Path.Combine(privateFolder, testFileKey);
        var testContent = "fake image data";
        File.WriteAllText(filePath, testContent);

        try
        {
            // Act
            var result = controller.ViewPrivateFile(testFileKey);

            // Assert
            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("image/png", fileResult.ContentType);
            fileResult.FileStream.Dispose(); // Release lock!
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Theory]
    [InlineData("../passwd")]
    [InlineData("..\\secret.txt")]
    [InlineData("folder/../../secret")]
    public void ViewPrivateFile_ShouldRejectDirectoryTraversal(string dangerousFileKey)
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.ViewPrivateFile(dangerousFileKey);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không hợp lệ", badRequestResult.Value.ToString());
    }
}
