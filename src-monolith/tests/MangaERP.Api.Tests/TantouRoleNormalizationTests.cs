using System.Security.Claims;
using MangaERP.Api.Controllers;
using MangaERP.Chapter.Domain.Entities;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MangaERP.Api.Tests;

public class TantouRoleNormalizationTests
{
    [Fact]
    public async System.Threading.Tasks.Task Tantou_ApproveOrRejectProposal_Returns403Forbidden()
    {
        await using var db = CreateDb();
        var tantouId = Guid.NewGuid();
        db.Users.Add(ActiveUser(tantouId, UserRole.TantouEditor));
        var workId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var controller = CreateController(db, tantouId);
        var returnResult = await controller.ReturnFromTantou("SeriesSubmission", workId, new("Guidance"), default);
        var recommendResult = await controller.Recommend("SeriesSubmission", workId, default);

        Assert.IsType<ForbidResult>(returnResult);
        Assert.IsType<ForbidResult>(recommendResult);
    }

    [Fact]
    public async System.Threading.Tasks.Task Tantou_ApproveOrRejectChapter_Returns403Forbidden()
    {
        await using var db = CreateDb();
        var tantouId = Guid.NewGuid();
        db.Users.Add(ActiveUser(tantouId, UserRole.TantouEditor));
        var chapterId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var controller = CreateController(db, tantouId);
        var returnResult = await controller.ReturnFromTantou("Chapter", chapterId, new("Guidance"), default);
        var recommendResult = await controller.Recommend("Chapter", chapterId, default);

        Assert.IsType<ForbidResult>(returnResult);
        Assert.IsType<ForbidResult>(recommendResult);
    }

    [Fact]
    public void Mangaka_SubmitProposal_WithoutTantou_SucceedsAndGoesDirectlyToEB()
    {
        var mangakaId = Guid.NewGuid();
        var submission = SeriesSubmission.CreateDraft(mangakaId, "Test Title", "Desc", "Genre", null, "https://example.com/manuscript.pdf");
        submission.SubmitDraft(); // Directly to EB, no Tantou Editor in MF1

        Assert.Equal(SubmissionStatus.Pending_EB_Review, submission.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Proposal_ApprovedByBoard_AutomaticallyAssignsTantouEditor()
    {
        await using var db = CreateDb();
        var mangakaId = Guid.NewGuid();
        var tantouId = Guid.NewGuid();
        var reviewer1 = Guid.NewGuid();
        var reviewer2 = Guid.NewGuid();

        var mangaka = ActiveUser(mangakaId, UserRole.Mangaka);
        mangaka.ManagingTantouId = tantouId;
        var tantou = ActiveUser(tantouId, UserRole.TantouEditor);
        var eb1 = ActiveUser(reviewer1, UserRole.EditorialBoard);
        var eb2 = ActiveUser(reviewer2, UserRole.EditorialBoard);

        db.Users.AddRange(mangaka, tantou, eb1, eb2);

        var submission = SeriesSubmission.CreateDraft(mangakaId, "Approved Series", "Desc", "Genre", null, "manuscript.pdf");
        submission.SubmitDraft();
        db.SeriesSubmissions.Add(submission);

        var a1 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, 1, reviewer1);
        var a2 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, 1, reviewer2);
        db.EditorialReviewAssignments.AddRange(a1, a2);
        await db.SaveChangesAsync();

        await CreateController(db, reviewer1).Decide(a1.Id, new(EditorialDecision.Approved, null), default);
        await CreateController(db, reviewer2).Decide(a2.Id, new(EditorialDecision.Approved, null), default);

        var updatedSubmission = await db.SeriesSubmissions.SingleAsync(s => s.Id == submission.Id);
        var updatedMangaka = await db.Users.SingleAsync(u => u.Id == mangakaId);

        Assert.Equal(SubmissionStatus.EB_Approved, updatedSubmission.Status);
        Assert.Equal(tantouId, updatedMangaka.ManagingTantouId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Proposal_RejectedByBoard_ReturnsFeedbackDirectlyToMangaka()
    {
        await using var db = CreateDb();
        var mangakaId = Guid.NewGuid();
        var reviewer1 = Guid.NewGuid();
        var reviewer2 = Guid.NewGuid();

        db.Users.AddRange(
            ActiveUser(mangakaId, UserRole.Mangaka),
            ActiveUser(reviewer1, UserRole.EditorialBoard),
            ActiveUser(reviewer2, UserRole.EditorialBoard));

        var submission = SeriesSubmission.CreateDraft(mangakaId, "Rejected Series", "Desc", "Genre", null, "manuscript.pdf");
        submission.SubmitDraft();
        db.SeriesSubmissions.Add(submission);

        var a1 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, 1, reviewer1);
        var a2 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, 1, reviewer2);
        db.EditorialReviewAssignments.AddRange(a1, a2);
        await db.SaveChangesAsync();

        await CreateController(db, reviewer1).Decide(a1.Id, new(EditorialDecision.Rejected, "Needs work"), default);
        await CreateController(db, reviewer2).Decide(a2.Id, new(EditorialDecision.Rejected, "Needs major revision"), default);

        var updatedSubmission = await db.SeriesSubmissions.SingleAsync(s => s.Id == submission.Id);

        Assert.Equal(SubmissionStatus.EB_Rejected, updatedSubmission.Status);
        Assert.NotNull(updatedSubmission.FeedbackMessage);
    }

    [Fact]
    public async System.Threading.Tasks.Task Tantou_AddAdvice_SucceedsWithoutChangingReviewStatus()
    {
        await using var db = CreateDb();
        var tantouId = Guid.NewGuid();
        var mangakaId = Guid.NewGuid();

        db.Users.AddRange(ActiveUser(tantouId, UserRole.TantouEditor), ActiveUser(mangakaId, UserRole.Mangaka));
        var submission = SeriesSubmission.CreateDraft(mangakaId, "Series Title", "Desc", "Genre", null, "manuscript.pdf");
        submission.SubmitDraft();
        submission.AssignTantou(tantouId);
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var controller = CreateController(db, tantouId);
        var result = await controller.AddAdvice("SeriesSubmission", submission.Id, new("Focus on character development"), default);

        Assert.IsType<NoContentResult>(result);

        var updated = await db.SeriesSubmissions.SingleAsync(s => s.Id == submission.Id);
        Assert.Equal(SubmissionStatus.Pending_EB_Review, updated.Status); // Review status unchanged!
        Assert.Equal("Focus on character development", updated.TantouGuidance);
    }

    [Fact]
    public void Mangaka_CanResubmit_WithoutTantouApproval()
    {
        var mangakaId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var submission = SeriesSubmission.CreateDraft(mangakaId, "Series Title", "Desc", "Genre", null, "manuscript.pdf");
        submission.SubmitDraft();

        submission.RejectToTantou(reviewerId, "Fix chapter 1 pacing");
        Assert.Equal(SubmissionStatus.Requires_Revision, submission.Status);

        submission.ReSubmit();
        Assert.Equal(SubmissionStatus.Pending_EB_Review, submission.Status);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static EditorialWorkflowController CreateController(AppDbContext db, Guid userId)
    {
        var controller = new EditorialWorkflowController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "test"))
            }
        };
        return controller;
    }

    private static User ActiveUser(Guid id, UserRole role) => new()
    {
        Id = id,
        Username = $"{id:N}@company.test",
        Email = $"{id:N}@company.test",
        PasswordHash = "hash",
        Role = role,
        AccountStatus = AccountStatus.Active
    };
}
