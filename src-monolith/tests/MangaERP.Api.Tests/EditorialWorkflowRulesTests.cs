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
    public void SubmissionGoesDirectlyToEBAndCanResubmitDirectly()
    {
        var mangaka = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var work = SeriesSubmission.CreateDraft(mangaka, "Title", null, null, null, "manuscript.pdf");

        work.SubmitDraft();
        Assert.Equal(SubmissionStatus.Pending_EB_Review, work.Status);
        work.RejectToTantou(reviewer, "The pacing needs work.");
        Assert.Equal(SubmissionStatus.Requires_Revision, work.Status);
        Assert.Equal("The pacing needs work.", work.FeedbackMessage);

        work.ReSubmit();
        Assert.Equal(SubmissionStatus.Pending_EB_Review, work.Status);
        Assert.Null(work.FeedbackMessage);
    }

    [Fact]
    public void TantouCannotDirectlyApproveChapter()
    {
        var chapter = ChapterEntity.Create(Guid.NewGuid(), "Chapter 1", 1, 1, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => chapter.Approve());
    }
}
