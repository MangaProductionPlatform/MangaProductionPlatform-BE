using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using PageTaskType = MangaERP.Chapter.Domain.Entities.PageTaskType;
using PreviewPageEntity = MangaERP.Chapter.Domain.Entities.PreviewPage;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Task.Application.Commands.BulkReviewLayers;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class BulkReviewLayersTests
{
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();
    private readonly Mock<IArtworkLayerRepository> _layerRepoMock = new();
    private readonly Mock<IPreviewPageRepository> _previewRepoMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private readonly BulkReviewLayersHandler _handler;

    public BulkReviewLayersTests()
    {
        _handler = new BulkReviewLayersHandler(
            _pageTaskRepoMock.Object,
            _chapterRepoMock.Object,
            _seriesRepoMock.Object,
            _layerRepoMock.Object,
            _previewRepoMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public void Validator_EmptyReviews_FailsValidation()
    {
        // Arrange
        var command = new BulkReviewLayersCommand(Guid.NewGuid(), new List<BulkReviewItem>());
        var validator = new BulkReviewLayersValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reviews");
    }

    [Fact]
    public void Validator_RejectedWithoutNote_FailsValidation()
    {
        // Arrange
        var command = new BulkReviewLayersCommand(Guid.NewGuid(), new List<BulkReviewItem>
        {
            new(Guid.NewGuid(), false, null) // Rejected but note is null
        });
        var validator = new BulkReviewLayersValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("Reviews[0]"));
    }

    [Fact]
    public async STTask Handle_PageTaskNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var pageTaskId = Guid.NewGuid();
        var command = new BulkReviewLayersCommand(Guid.NewGuid(), new List<BulkReviewItem>
        {
            new(pageTaskId, true, null)
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageTaskEntity)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ChapterNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var chapterId = Guid.NewGuid();
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var pageTaskId = pageTask.Id;
        var command = new BulkReviewLayersCommand(Guid.NewGuid(), new List<BulkReviewItem>
        {
            new(pageTaskId, true, null)
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(pageTask.ChapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterEntity)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_SeriesNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var chapterId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var pageTaskId = pageTask.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        
        var command = new BulkReviewLayersCommand(Guid.NewGuid(), new List<BulkReviewItem>
        {
            new(pageTaskId, true, null)
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MangaSeries)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedMangaka_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var authorId = Guid.NewGuid(); // different owner
        
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var pageTaskId = pageTask.Id;

        var command = new BulkReviewLayersCommand(mangakaId, new List<BulkReviewItem>
        {
            new(pageTaskId, true, null)
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_NoPendingArtworkLayer_ThrowsInvalidOperationException()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        
        var series = MangaSeries.Create(mangakaId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var pageTaskId = pageTask.Id;

        var command = new BulkReviewLayersCommand(mangakaId, new List<BulkReviewItem>
        {
            new(pageTaskId, true, null)
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _layerRepoMock.Setup(r => r.GetCurrentByPageTaskIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArtworkLayer)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_AcceptsAndRejectsCorrectly()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;

        // Page tasks
        var pageTask1 = PageTaskEntity.CreatePending(chapterId, 1, "https://example.com/page-1.png");
        var pageTask2 = PageTaskEntity.CreatePending(chapterId, 2, "https://example.com/page-2.png");

        pageTask1.Activate(assistantId, PageTaskType.General, "Task 1");
        pageTask1.MarkReviewing();

        pageTask2.Activate(assistantId, PageTaskType.General, "Task 2");
        pageTask2.MarkReviewing();

        var pageTaskId1 = pageTask1.Id;
        var pageTaskId2 = pageTask2.Id;

        // Artwork layers
        var layer1 = new ArtworkLayer 
        { 
            PageTaskId = pageTaskId1, 
            AssistantId = assistantId,
            FileUrlOriginal = "orig1.png"
        };
        var layer2 = new ArtworkLayer 
        { 
            PageTaskId = pageTaskId2, 
            AssistantId = assistantId,
            FileUrlOriginal = "orig2.png"
        };

        var command = new BulkReviewLayersCommand(mangakaId, new List<BulkReviewItem>
        {
            new(pageTaskId1, true, null), // Accept
            new(pageTaskId2, false, "Redraw backgrounds please") // Reject
        });

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask1);
        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask2);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        _layerRepoMock.Setup(r => r.GetCurrentByPageTaskIdAsync(pageTaskId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(layer1);
        _layerRepoMock.Setup(r => r.GetCurrentByPageTaskIdAsync(pageTaskId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(layer2);

        _previewRepoMock.Setup(r => r.GetByPageTaskIdAsync(pageTaskId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PreviewPageEntity)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);

        // Verify pageTask1 (Approved)
        Assert.Equal(MangaERP.Chapter.Domain.Entities.PageTaskStatus.Approved, pageTask1.TaskStatus);
        Assert.Null(layer1.RejectionNote);
        _previewRepoMock.Verify(r => r.AddAsync(It.IsAny<PreviewPageEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.NotifyTaskApprovedAsync(assistantId, pageTaskId1, It.IsAny<CancellationToken>()), Times.Once);

        // Verify pageTask2 (RevisionAlert)
        Assert.Equal(MangaERP.Chapter.Domain.Entities.PageTaskStatus.RevisionAlert, pageTask2.TaskStatus);
        Assert.Equal("Redraw backgrounds please", layer2.RejectionNote);
        _notificationServiceMock.Verify(n => n.NotifyRevisionRequiredAsync(assistantId, pageTaskId2, "Redraw backgrounds please", It.IsAny<CancellationToken>()), Times.Once);

        // Verify updates and save changes
        _layerRepoMock.Verify(r => r.UpdateAsync(layer1, It.IsAny<CancellationToken>()), Times.Once);
        _layerRepoMock.Verify(r => r.UpdateAsync(layer2, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask1, It.IsAny<CancellationToken>()), Times.Once);
        _pageTaskRepoMock.Verify(r => r.UpdateAsync(pageTask2, It.IsAny<CancellationToken>()), Times.Once);
        _layerRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
