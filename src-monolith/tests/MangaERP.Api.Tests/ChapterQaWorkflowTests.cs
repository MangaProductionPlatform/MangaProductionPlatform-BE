using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Api.Tests;

public class ChapterQaWorkflowTests
{
    [Fact]
    public void Chapter_Create_StartsInDraftStatus()
    {
        var seriesId = Guid.NewGuid();
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1: The Beginning", 1.0m, 15);

        Assert.Equal(ChapterStatus.Draft, chapter.Status);
        Assert.Equal(seriesId, chapter.SeriesId);
        Assert.Equal("Chapter 1: The Beginning", chapter.Title);
        Assert.Equal(15, chapter.TotalPages);
    }

    [Fact]
    public void Chapter_SubmitForQA_FailsIfPagesNotApproved()
    {
        var seriesId = Guid.NewGuid();
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1.0m, 10);

        var ex = Assert.Throws<InvalidOperationException>(() => chapter.SubmitForQA());
        Assert.Contains("must be approved before submitting for QA", ex.Message);
    }

    [Fact]
    public void Chapter_RejectToTantou_RequiresFeedback()
    {
        var seriesId = Guid.NewGuid();
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1.0m, 10);

        var ex = Assert.Throws<InvalidOperationException>(() => chapter.RejectToTantou(""));
        Assert.Equal("This chapter is not awaiting an editorial decision.", ex.Message);
    }

    [Fact]
    public void Chapter_EnsureOwnedBy_ThrowsUnauthorized_WhenUserNotAuthor()
    {
        var mangakaId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var chapter = ChapterEntity.Create(seriesId, "Chapter 1", 1.0m, 10);

        Assert.Throws<UnauthorizedAccessException>(() => chapter.EnsureOwnedBy(otherUserId, mangakaId));
    }
}
