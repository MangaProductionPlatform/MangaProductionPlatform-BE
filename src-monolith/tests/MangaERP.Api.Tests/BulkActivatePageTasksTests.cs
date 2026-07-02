using MangaERP.Chapter.Application.Commands.BulkActivatePageTasks;
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

public class BulkActivatePageTasksTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IStudioInvitationRepository> _studioRepoMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private readonly BulkActivatePageTasksHandler _handler;

    public BulkActivatePageTasksTests()
    {
        _handler = new BulkActivatePageTasksHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object,
            _userRepoMock.Object,
            _studioRepoMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public void Validator_EmptyPageNumbers_FailsValidation()
    {
        // Arrange
        var command = new BulkActivatePageTasksCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<int>(), Guid.NewGuid());
        var validator = new BulkActivatePageTasksValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PageNumbers");
    }

    [Fact]
    public void Validator_NegativePageNumbers_FailsValidation()
    {
        // Arrange
        var command = new BulkActivatePageTasksCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<int> { 1, -2, 3 }, Guid.NewGuid());
        var validator = new BulkActivatePageTasksValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PageNumbers");
    }

    [Fact]
    public async STTask Handle_ChapterNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var chapterId = Guid.NewGuid();
        var command = new BulkActivatePageTasksCommand(
            Guid.NewGuid(), chapterId, new List<int> { 1 }, Guid.NewGuid());

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterEntity)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_SeriesNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var seriesId = Guid.NewGuid();
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var command = new BulkActivatePageTasksCommand(
            Guid.NewGuid(), chapterId, new List<int> { 1 }, Guid.NewGuid());

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MangaSeries)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_AssistantNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 1 }, assistantId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _userRepoMock.Setup(r => r.GetByIdAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_NotAssistantRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var readerUser = new User { Id = assistantId, Role = UserRole.Reader };
        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 1 }, assistantId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _userRepoMock.Setup(r => r.GetByIdAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readerUser);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_AssistantNotInStudio_ThrowsInvalidOperationException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var assistantUser = new User { Id = assistantId, Role = UserRole.Assistant };
        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 1 }, assistantId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _userRepoMock.Setup(r => r.GetByIdAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistantUser);
        _studioRepoMock.Setup(r => r.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioInvitation>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_PageTaskNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var assistantUser = new User { Id = assistantId, Role = UserRole.Assistant };
        var studioInvitation = new StudioInvitation
        {
            AssistantUserId = assistantId,
            SeriesId = seriesId,
            Status = StudioInvitationStatus.Accepted
        };

        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 5 }, assistantId); // Page 5 doesn't exist

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _userRepoMock.Setup(r => r.GetByIdAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistantUser);
        _studioRepoMock.Setup(r => r.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioInvitation> { studioInvitation });
        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumbersAsync(chapterId, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask>());

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_Success()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var assistantUser = new User { Id = assistantId, Role = UserRole.Assistant };
        
        var page1 = PageTask.CreatePending(chapterId, 1);
        var page2 = PageTask.CreatePending(chapterId, 2);

        var studioInvitation = new StudioInvitation
        {
            AssistantUserId = assistantId,
            SeriesId = seriesId,
            Status = StudioInvitationStatus.Accepted
        };

        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 1, 2 }, assistantId, "Please draw backgrounds.");

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _userRepoMock.Setup(r => r.GetByIdAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistantUser);
        _studioRepoMock.Setup(r => r.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioInvitation> { studioInvitation });

        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumbersAsync(chapterId, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask> { page1, page2 });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chapterId, result.ChapterId);
        Assert.Equal(assistantId, result.AssignedAssistantId);
        Assert.Equal(2, result.ActivatedPages.Count);

        // Verify status and assignment
        Assert.Equal(PageTaskStatus.Incomplete, page1.TaskStatus);
        Assert.Equal(assistantId, page1.AssignedAssistantId);
        Assert.Equal("Please draw backgrounds.", page1.Description);

        Assert.Equal(PageTaskStatus.Incomplete, page2.TaskStatus);
        Assert.Equal(assistantId, page2.AssignedAssistantId);
        Assert.Equal("Please draw backgrounds.", page2.Description);

        // Verify Repositories were updated and saved
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(page1, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(page2, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Verify NotificationService notified assistant for both pages
        _notificationServiceMock.Verify(n => n.NotifyTaskAssignedAsync(assistantId, page1.Id, 1, It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyTaskAssignedAsync(assistantId, page2.Id, 2, It.IsAny<CancellationToken>()), Times.Once);
    }
}
