using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Submission.Domain.Entities;

namespace MangaERP.Api.Tests;

public class EditorialWorkflowRulesTests
{
    [Fact]
    public void RejectedReviewRequiresFeedback()
    {
        var review = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, Guid.NewGuid(), 1, Guid.NewGuid());
        var error = Assert.Throws<InvalidOperationException>(() => review.Complete(EditorialDecision.Rejected, " "));
        Assert.Contains("Feedback is required", error.Message);
    }

    [Fact]
    public void ReviewCanOnlyBeCompletedOnce()
    {
        var review = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, Guid.NewGuid(), 1, Guid.NewGuid());
        review.Complete(EditorialDecision.Approved, null);
        Assert.Throws<InvalidOperationException>(() => review.Complete(EditorialDecision.Rejected, "Changed my mind"));
    }

    [Fact]
    public void SubmissionReturnsThroughTantouAfterEditorialRejection()
    {
        var mangaka = Guid.NewGuid();
        var tantou = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var work = SeriesSubmission.CreateDraft(mangaka, "Title", null, null, null, "manuscript.pdf");

        work.SubmitDraft(tantou);
        Assert.Equal(SubmissionStatus.Pending_Tantou_Review, work.Status);
        work.RecommendToEditorialBoard(tantou);
        work.RejectToTantou(reviewer, "The pacing needs work.");
        Assert.Equal(SubmissionStatus.Editorial_Rejected_To_Tantou, work.Status);
        work.ReturnConsolidatedGuidanceToMangaka(tantou, "Shorten the opening and clarify the conflict.");
        Assert.Equal(SubmissionStatus.Mangaka_Revision_Required, work.Status);
        work.ReSubmit();
        Assert.Equal(SubmissionStatus.Pending_Tantou_Review, work.Status);
    }

    [Fact]
    public void TantouCannotDirectlyApproveChapter()
    {
        var chapter = ChapterEntity.Create(Guid.NewGuid(), "Chapter 1", 1, 1, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => chapter.Approve());
    }
}
