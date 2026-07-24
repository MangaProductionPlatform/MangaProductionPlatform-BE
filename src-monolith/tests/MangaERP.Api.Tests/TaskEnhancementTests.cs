using MangaERP.Chapter.Application.Commands.UpdateTaskDetails;
using MangaERP.Chapter.Application.Commands.CancelAndRecreateTask;
using MangaERP.Chapter.Application.Commands.ReassignPageTask;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class TaskEnhancementTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IStudioInvitationRepository> _studioRepoMock = new();
    private readonly Mock<ICollaborationAuthorizationService> _collabAuthMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    [Fact]
    public async STTask UpdateTaskDetails_ValidRequest_Success()
    {
        var handler = new UpdateTaskDetailsHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);

        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series Title", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/old.png");

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        var command = new UpdateTaskDetailsCommand(
            authorId,
            pageTask.Id,
            "New Note Description",
            DateTime.UtcNow.AddDays(2),
            "Background",
            "https://example.com/new-base.png"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("New Note Description", result.Description);
        Assert.Equal("Background", result.TaskType);
        Assert.Equal("https://example.com/new-base.png", result.BaseImageUrl);

        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask UpdateTaskDetails_AlreadySubmitted_ThrowsInvalidOperationException()
    {
        var handler = new UpdateTaskDetailsHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);

        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series Title", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/old.png");
        pageTask.TaskStatus = PageTaskStatus.Reviewing; // Already submitted

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        var command = new UpdateTaskDetailsCommand(
            authorId,
            pageTask.Id,
            "New Note",
            null,
            null,
            null
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Cannot update task details after assistant has submitted artwork", ex.Message);
    }

    [Fact]
    public async STTask CancelAndRecreateTask_ValidRequest_Success()
    {
        var handler = new CancelAndRecreateTaskHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);

        var authorId = Guid.NewGuid();
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page.png");

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _pageTaskRepoMock.Setup(r => r.GetNextNegativePageNumberAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        var command = new CancelAndRecreateTaskCommand(authorId, pageTask.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(pageTask.Id, result.OldPageTaskId);
        Assert.NotEqual(Guid.Empty, result.NewPageTaskId);
        Assert.Equal(1, result.PageNumber);
        Assert.True(pageTask.IsDeleted);
        Assert.True(pageTask.PageNumber < 0); // Check negative page number shift

        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.AddAsync(It.IsAny<PageTask>(), It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask ReassignPageTask_HasSubmissions_WithoutConfirmation_ThrowsInvalidOperationException()
    {
        var handler = new ReassignPageTaskHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object,
            _userRepoMock.Object,
            _collabAuthMock.Object,
            _notificationServiceMock.Object);

        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1, 3);
        var pageTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/page.png");
        pageTask.Activate(assistantId, PageTaskType.General);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(series.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumberAsync(chapter.Id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _pageTaskRepoMock.Setup(r => r.HasSubmissionsAsync(pageTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Task has submissions!

        // ConfirmIfSubmitted = false (default)
        var command = new ReassignPageTaskCommand(authorId, chapter.Id, 1, newAssistantId, "Reassigned", false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("SUBMISSION_EXISTS_CONFIRMATION_REQUIRED", ex.Message);
    }
}
