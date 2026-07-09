using MangaERP.Segmentation.Application.Commands.CreateSegmentationTask;
using MangaERP.Segmentation.Application.Commands.UpdateSegmentationTaskStatus;
using MangaERP.Segmentation.Application.Queries.GetMySegmentationTasks;
using MangaERP.Segmentation.Domain.Entities;
using MangaERP.Segmentation.Infrastructure;
using MangaERP.Segmentation.Infrastructure.Repositories;
using MangaERP.Shared.Application.Contracts.Events;
using MangaERP.Shared.Application.Contracts.Queries;
using MangaERP.Segmentation.Application.Ports;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http;
using Xunit;

namespace MangaERP.Segmentation.Tests;

public class TestableCreateSegmentationTaskHandler : CreateSegmentationTaskHandler
{
    public (int Width, int Height)? MockedDimensions { get; set; }

    public TestableCreateSegmentationTaskHandler(
        ISegmentationTaskRepository repo,
        IMediator mediator,
        ILogger<CreateSegmentationTaskHandler> logger,
        IHttpClientFactory httpClientFactory)
        : base(repo, mediator, logger, httpClientFactory)
    {
    }

    protected override Task<(int Width, int Height)?> GetImageDimensionsAsync(string imageUrl, CancellationToken ct)
    {
        return Task.FromResult(MockedDimensions);
    }
}

public class SegmentationTests
{
    private readonly DbContextOptions<SegmentationDbContext> _dbOptions;

    public SegmentationTests()
    {
        _dbOptions = new DbContextOptionsBuilder<SegmentationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private SegmentationDbContext CreateDbContext() => new SegmentationDbContext(_dbOptions);

    [Fact]
    public void Validator_ShouldReject_WhenPageIdOrMaskRleIsEmpty()
    {
        // Arrange
        var validator = new CreateSegmentationTaskValidator();

        var command1 = new CreateSegmentationTaskCommand(
            PageId: Guid.Empty, // Empty
            MaskRle: "some-rle",
            Bbox: new[] { 0, 0, 100, 100 },
            TaskType: SegmentationTaskType.LineArt,
            Note: "Test",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        var command2 = new CreateSegmentationTaskCommand(
            PageId: Guid.NewGuid(),
            MaskRle: "", // Empty
            Bbox: new[] { 0, 0, 100, 100 },
            TaskType: SegmentationTaskType.LineArt,
            Note: "Test",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act
        var result1 = validator.Validate(command1);
        var result2 = validator.Validate(command2);

        // Assert
        Assert.False(result1.IsValid);
        Assert.Contains(result1.Errors, e => e.PropertyName == "PageId");

        Assert.False(result2.IsValid);
        Assert.Contains(result2.Errors, e => e.PropertyName == "MaskRle");
    }

    [Fact]
    public async Task CreateTaskHandler_ShouldSaveTaskAndPublishEvent()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<CreateSegmentationTaskHandler>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var pageId = Guid.NewGuid();

        mediatorMock.Setup(m => m.Send(It.Is<GetPageTaskPreviewUrlQuery>(q => q.PageId == pageId), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://example.com/image.png");

        var handler = new TestableCreateSegmentationTaskHandler(
            repo, 
            mediatorMock.Object, 
            loggerMock.Object,
            httpClientFactoryMock.Object)
        {
            MockedDimensions = (1920, 1080)
        };

        var command = new CreateSegmentationTaskCommand(
            PageId: pageId,
            MaskRle: "rle-data",
            Bbox: new[] { 10, 20, 30, 40 },
            TaskType: SegmentationTaskType.Shading,
            Note: "Please color this",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.TaskId);
        Assert.Equal("Pending", result.Status);

        var savedTask = await db.SegmentationTasks.FindAsync(result.TaskId);
        Assert.NotNull(savedTask);
        Assert.Equal("rle-data", savedTask.MaskRle);
        Assert.Equal("Assistant", savedTask.AssignedToUserRole);
        Assert.Equal(1920, savedTask.OriginalWidth);
        Assert.Equal(1080, savedTask.OriginalHeight);

        mediatorMock.Verify(m => m.Publish(
            It.Is<SegmentationTaskAssignedEvent>(e => 
                e.TaskId == result.TaskId &&
                e.AssignedToUserId == command.AssignedToUserId &&
                e.PageId == command.PageId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTaskHandler_ShouldSaveTask_WhenImageDimensionsAreNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<CreateSegmentationTaskHandler>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var pageId = Guid.NewGuid();

        mediatorMock.Setup(m => m.Send(It.Is<GetPageTaskPreviewUrlQuery>(q => q.PageId == pageId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new TestableCreateSegmentationTaskHandler(
            repo, 
            mediatorMock.Object, 
            loggerMock.Object,
            httpClientFactoryMock.Object)
        {
            MockedDimensions = null
        };

        var command = new CreateSegmentationTaskCommand(
            PageId: pageId,
            MaskRle: "rle-data",
            Bbox: new[] { 10, 20, 30, 40 },
            TaskType: SegmentationTaskType.Shading,
            Note: "Null dimensions",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var savedTask = await db.SegmentationTasks.FindAsync(result.TaskId);
        Assert.NotNull(savedTask);
        Assert.Null(savedTask.OriginalWidth);
        Assert.Null(savedTask.OriginalHeight);
    }

    [Fact]
    public async Task CreateTaskHandler_ShouldThrowValidationException_WhenDimensionsAreNegative()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<CreateSegmentationTaskHandler>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var pageId = Guid.NewGuid();

        mediatorMock.Setup(m => m.Send(It.Is<GetPageTaskPreviewUrlQuery>(q => q.PageId == pageId), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://example.com/image.png");

        var handler = new TestableCreateSegmentationTaskHandler(
            repo, 
            mediatorMock.Object, 
            loggerMock.Object,
            httpClientFactoryMock.Object)
        {
            MockedDimensions = (-100, 1080)
        };

        var command = new CreateSegmentationTaskCommand(
            PageId: pageId,
            MaskRle: "rle-data",
            Bbox: new[] { 10, 20, 30, 40 },
            TaskType: SegmentationTaskType.Shading,
            Note: "Negative Width",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => 
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateTaskHandler_ShouldThrowValidationException_WhenDimensionsAreTooLarge()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<CreateSegmentationTaskHandler>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var pageId = Guid.NewGuid();

        mediatorMock.Setup(m => m.Send(It.Is<GetPageTaskPreviewUrlQuery>(q => q.PageId == pageId), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://example.com/image.png");

        var handler = new TestableCreateSegmentationTaskHandler(
            repo, 
            mediatorMock.Object, 
            loggerMock.Object,
            httpClientFactoryMock.Object)
        {
            MockedDimensions = (1920, 12000) // Height too large
        };

        var command = new CreateSegmentationTaskCommand(
            PageId: pageId,
            MaskRle: "rle-data",
            Bbox: new[] { 10, 20, 30, 40 },
            TaskType: SegmentationTaskType.Shading,
            Note: "Excessive Height",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act & Assert
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => 
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateTaskHandler_ShouldNotRollback_WhenEventPublishFails()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RabbitMQ/SignalR is down"));

        var loggerMock = new Mock<ILogger<CreateSegmentationTaskHandler>>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var pageId = Guid.NewGuid();
        mediatorMock.Setup(m => m.Send(It.Is<GetPageTaskPreviewUrlQuery>(q => q.PageId == pageId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new TestableCreateSegmentationTaskHandler(
            repo, 
            mediatorMock.Object, 
            loggerMock.Object,
            httpClientFactoryMock.Object)
        {
            MockedDimensions = null
        };

        var command = new CreateSegmentationTaskCommand(
            PageId: pageId,
            MaskRle: "rle-data",
            Bbox: new[] { 10, 20, 30, 40 },
            TaskType: SegmentationTaskType.Shading,
            Note: "Ignore event error",
            AssignedToUserId: Guid.NewGuid(),
            AssignedToUserRole: "Assistant",
            CreatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var savedTask = await db.SegmentationTasks.FindAsync(result.TaskId);
        Assert.NotNull(savedTask); // Saved successfully despite event publish failure
    }

    [Fact]
    public async Task GetMyTasksQuery_ShouldClampPageSizeTo100()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var handler = new GetMySegmentationTasksHandler(repo);
        var userId = Guid.NewGuid();

        for (int i = 0; i < 110; i++)
        {
            await db.SegmentationTasks.AddAsync(new SegmentationTask
            {
                PageId = Guid.NewGuid(),
                MaskRle = "rle",
                AssignedToUserId = userId,
                CreatedByUserId = Guid.NewGuid(),
                Status = SegmentationTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var query = new GetMySegmentationTasksQuery(userId, null, 1, 999);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, result.Items.Count());
        Assert.Equal(110, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetMyTasksQuery_ShouldNotShowTasksOfOtherUsers()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var handler = new GetMySegmentationTasksHandler(repo);
        
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await db.SegmentationTasks.AddAsync(new SegmentationTask
        {
            PageId = Guid.NewGuid(),
            MaskRle = "rle",
            AssignedToUserId = userA,
            CreatedByUserId = Guid.NewGuid(),
            Status = SegmentationTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await db.SegmentationTasks.AddAsync(new SegmentationTask
        {
            PageId = Guid.NewGuid(),
            MaskRle = "rle",
            AssignedToUserId = userB,
            CreatedByUserId = Guid.NewGuid(),
            Status = SegmentationTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new GetMySegmentationTasksQuery(userA, null, 1, 20);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(userA, result.Items.First().AssignedToUserId);
    }

    [Fact]
    public async Task UpdateStatus_ShouldThrowUnauthorized_WhenUserIsNotOwner()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var handler = new UpdateSegmentationTaskStatusHandler(repo);

        var task = new SegmentationTask
        {
            PageId = Guid.NewGuid(),
            MaskRle = "rle",
            AssignedToUserId = Guid.NewGuid(), // Owner A
            CreatedByUserId = Guid.NewGuid(),
            Status = SegmentationTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await db.SegmentationTasks.AddAsync(task);
        await db.SaveChangesAsync();

        var command = new UpdateSegmentationTaskStatusCommand(
            TaskId: task.Id,
            CallerUserId: Guid.NewGuid(), // Non-owner B
            NewStatus: SegmentationTaskStatus.InProgress
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStatus_ShouldThrowException_WhenTransitionIsInvalid()
    {
        // Arrange
        using var db = CreateDbContext();
        var repo = new SegmentationTaskRepository(db);
        var handler = new UpdateSegmentationTaskStatusHandler(repo);

        var ownerId = Guid.NewGuid();
        var task = new SegmentationTask
        {
            PageId = Guid.NewGuid(),
            MaskRle = "rle",
            AssignedToUserId = ownerId,
            CreatedByUserId = Guid.NewGuid(),
            Status = SegmentationTaskStatus.Pending, // Status Pending
            CreatedAt = DateTime.UtcNow
        };
        await db.SegmentationTasks.AddAsync(task);
        await db.SaveChangesAsync();

        // Valid transition is InProgress. Invalid is Submitted directly.
        var command = new UpdateSegmentationTaskStatusCommand(
            TaskId: task.Id,
            CallerUserId: ownerId,
            NewStatus: SegmentationTaskStatus.Submitted // Skip InProgress (Invalid)
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid status transition", ex.Message);
    }
}
