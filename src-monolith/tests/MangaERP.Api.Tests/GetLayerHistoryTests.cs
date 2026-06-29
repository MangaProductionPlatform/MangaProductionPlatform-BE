using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Task.Application.Queries.GetLayerHistory;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class GetLayerHistoryTests
{
    private readonly Mock<IPageTaskRepository> _pageTaskRepoMock = new();
    private readonly Mock<IChapterRepository> _chapterRepoMock = new();
    private readonly Mock<ISeriesRepository> _seriesRepoMock = new();
    private readonly Mock<IArtworkLayerRepository> _layerRepoMock = new();

    private readonly GetLayerHistoryHandler _handler;

    public GetLayerHistoryTests()
    {
        _handler = new GetLayerHistoryHandler(
            _pageTaskRepoMock.Object,
            _chapterRepoMock.Object,
            _seriesRepoMock.Object,
            _layerRepoMock.Object);
    }

    [Fact]
    public void Validator_InvalidStatus_FailsValidation()
    {
        // Arrange
        var query = new GetLayerHistoryQuery(Guid.NewGuid(), Status: "InvalidStatus");
        var validator = new GetLayerHistoryValidator();

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Status");
    }

    [Fact]
    public void Validator_ValidStatuses_PassesValidation()
    {
        // Arrange
        var query = new GetLayerHistoryQuery(Guid.NewGuid(), Status: "Accepted");
        var validator = new GetLayerHistoryValidator();

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async STTask Handle_PageTaskNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var pageTaskId = Guid.NewGuid();
        var query = new GetLayerHistoryQuery(Guid.NewGuid(), PageTaskId: pageTaskId);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PageTaskEntity)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedPageTaskId_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var authorId = Guid.NewGuid(); // different owner
        
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1);
        var pageTaskId = pageTask.Id;

        var query = new GetLayerHistoryQuery(mangakaId, PageTaskId: pageTaskId);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedChapterId_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var authorId = Guid.NewGuid(); // different owner
        
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;

        var query = new GetLayerHistoryQuery(mangakaId, ChapterId: chapterId);

        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedSeriesId_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var authorId = Guid.NewGuid(); // different owner
        
        var series = MangaSeries.Create(authorId, null, "Series", null, null, null);
        var seriesId = series.Id;

        var query = new GetLayerHistoryQuery(mangakaId, SeriesId: seriesId);

        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidQuery_ReturnsOnlyReviewedLayersSortedNewestFirst()
    {
        // Arrange
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", null, null, null);
        var seriesId = series.Id;
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1, 3);
        var chapterId = chapter.Id;
        var pageTask = PageTaskEntity.CreatePending(chapterId, 1);
        var pageTaskId = pageTask.Id;

        var layer1 = new ArtworkLayer 
        { 
            PageTaskId = pageTaskId, 
            AssistantId = assistantId,
            FileUrlOriginal = "orig1.png",
            Version = 1,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10),
            ReviewedAt = DateTime.UtcNow.AddMinutes(-5),
            RejectionNote = "Redraw backgrounds"
        };
        var layer2 = new ArtworkLayer 
        { 
            PageTaskId = pageTaskId, 
            AssistantId = assistantId,
            FileUrlOriginal = "orig2.png",
            Version = 2,
            SubmittedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow.AddMinutes(2),
            RejectionNote = null // Accepted
        };
        var layer3 = new ArtworkLayer 
        { 
            PageTaskId = pageTaskId, 
            AssistantId = assistantId,
            FileUrlOriginal = "orig3.png",
            Version = 3,
            SubmittedAt = DateTime.UtcNow.AddMinutes(10),
            ReviewedAt = null // Pending review (should be skipped)
        };

        var query = new GetLayerHistoryQuery(mangakaId, PageTaskId: pageTaskId);

        _pageTaskRepoMock.Setup(r => r.GetByIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageTask);
        _chapterRepoMock.Setup(r => r.GetByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chapter);
        _seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _layerRepoMock.Setup(r => r.GetByPageTaskIdAsync(pageTaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArtworkLayer> { layer1, layer2, layer3 });

        // Act
        var result = (await _handler.Handle(query, CancellationToken.None)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // layer3 is skipped because ReviewedAt is null

        // Order descending by SubmittedAt (layer2 submitted at DateTime.UtcNow, layer1 submitted at DateTime.UtcNow - 10 min)
        Assert.Equal(layer2.Id, result[0].LayerId);
        Assert.Equal("Accepted", result[0].Status);

        Assert.Equal(layer1.Id, result[1].LayerId);
        Assert.Equal("Rejected", result[1].Status);
    }
}
