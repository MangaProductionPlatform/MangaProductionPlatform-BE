using MangaERP.Chapter.Application.Commands.BulkActivatePageTasks;
using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class BulkActivatePageTasksTests
{
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();

    private readonly BulkActivatePageTasksHandler _handler;

    public BulkActivatePageTasksTests()
    {
        _handler = new BulkActivatePageTasksHandler(
            _chapterRepoMock.Object,
            _pageTaskRepoMock.Object,
            _seriesRepoMock.Object);
    }

    [Fact]
    public void Validator_EmptyPageNumbers_FailsValidation()
    {
        // Arrange
        var command = new BulkActivatePageTasksCommand(
            Guid.NewGuid(), Guid.NewGuid(), new List<int>(), Guid.NewGuid(), "General");
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
            Guid.NewGuid(), Guid.NewGuid(), new List<int> { 1, -2, 3 }, Guid.NewGuid(), "General");
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
            Guid.NewGuid(), chapterId, new List<int> { 1 }, Guid.NewGuid(), "General");

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
            Guid.NewGuid(), chapterId, new List<int> { 1 }, Guid.NewGuid(), "General");

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MangaSeries)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
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

        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 5 }, assistantId, "General"); // Page 5 doesn't exist

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumbersAsync(chapterId, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask>());

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_Success_ActivatesTasksWithoutDirectAssignmentBypass()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(authorId, null, "Test Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        
        var page1 = PageTask.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var page2 = PageTask.CreatePending(chapterId, 2, "https://example.com/page-2.png");

        var command = new BulkActivatePageTasksCommand(
            authorId, chapterId, new List<int> { 1, 2 }, assistantId, "General", "Please draw backgrounds.");

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        _pageTaskRepoMock.Setup(r => r.GetByChapterAndPageNumbersAsync(chapterId, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PageTask> { page1, page2 });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chapterId, result.ChapterId);
        Assert.Equal(2, result.ActivatedPages.Count);

        // Verify status and unassigned Pending state ready for candidate selection
        Assert.Equal(PageTaskStatus.Pending, page1.TaskStatus);
        Assert.Null(page1.AssignedAssistantId);
        Assert.Null(page1.WorkStartedAt);
        Assert.Equal("Please draw backgrounds.", page1.Description);

        Assert.Equal(PageTaskStatus.Pending, page2.TaskStatus);
        Assert.Null(page2.AssignedAssistantId);
        Assert.Null(page2.WorkStartedAt);
        Assert.Equal("Please draw backgrounds.", page2.Description);

        // Verify Repositories were updated and saved
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(page1, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(page2, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
