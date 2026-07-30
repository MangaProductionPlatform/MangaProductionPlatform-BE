using System.Security.Claims;
using System.Text.Json;
using MangaERP.Api.Controllers;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Api.Tests;

public class EditorialWorkflowIntegrationTests
{
    [Theory]
    [InlineData(EditorialDecision.Approved, EditorialDecision.Approved, SubmissionStatus.EB_Approved)]
    [InlineData(EditorialDecision.Rejected, EditorialDecision.Rejected, SubmissionStatus.EB_Rejected)]
    [InlineData(EditorialDecision.Approved, EditorialDecision.Rejected, SubmissionStatus.Conflict_Escalated)]
    public async System.Threading.Tasks.Task TwoReviewerDecisionMatrixProducesRequiredOutcome(
        EditorialDecision first, EditorialDecision second, SubmissionStatus expected)
    {
        await using var db = CreateDb();
        var setup = await SeedPendingRound(db);

        await Controller(db, setup.Reviewer1).Decide(setup.Assignment1, new(first, first == EditorialDecision.Rejected ? "First rejection" : null), default);
        await Controller(db, setup.Reviewer2).Decide(setup.Assignment2, new(second, second == EditorialDecision.Rejected ? "Second rejection" : null), default);

        var work = await db.SeriesSubmissions.SingleAsync(x => x.Id == setup.WorkId);
        Assert.Equal(expected, work.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task PeerReviewIdentityAndDecisionStayHiddenUntilBothComplete()
    {
        await using var db = CreateDb();
        var setup = await SeedPendingRound(db);

        await Controller(db, setup.Reviewer1).Decide(setup.Assignment1, new(EditorialDecision.Approved, null), default);
        var before = Assert.IsType<OkObjectResult>(await Controller(db, setup.Reviewer1).ReviewDetail(setup.Assignment1, default));
        var beforeJson = JsonSerializer.Serialize(before.Value);
        Assert.DoesNotContain(setup.Reviewer2.ToString(), beforeJson, StringComparison.OrdinalIgnoreCase);

        await Controller(db, setup.Reviewer2).Decide(setup.Assignment2, new(EditorialDecision.Rejected, "Needs revision"), default);
        var after = Assert.IsType<OkObjectResult>(await Controller(db, setup.Reviewer1).ReviewDetail(setup.Assignment1, default));
        var afterJson = JsonSerializer.Serialize(after.Value);
        Assert.Contains(setup.Reviewer2.ToString(), afterJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Needs revision", afterJson);
    }

    [Fact]
    public async System.Threading.Tasks.Task EligibleEditorInChiefResolvesSplitAndCannotBeAnInitialReviewer()
    {
        await using var db = CreateDb();
        var setup = await SeedPendingRound(db);
        var eic = Guid.NewGuid();
        db.Users.Add(ActiveUser(eic, UserRole.EditorInChief));
        await db.SaveChangesAsync();

        await Controller(db, setup.Reviewer1).Decide(setup.Assignment1, new(EditorialDecision.Approved, null), default);
        await Controller(db, setup.Reviewer2).Decide(setup.Assignment2, new(EditorialDecision.Rejected, "Reject"), default);
        await Controller(db, eic).Resolve("SeriesSubmission", setup.WorkId, new(EditorialDecision.Rejected, "Final rejection"), default);

        var work = await db.SeriesSubmissions.SingleAsync(x => x.Id == setup.WorkId);
        Assert.Equal(SubmissionStatus.EB_Rejected, work.Status);
        Assert.DoesNotContain(eic, await db.EditorialReviewAssignments.Select(x => x.ReviewerId).ToListAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task EditorInChiefResolvesSplitWithNullFeedback_UsesCombinedReviewerFeedbackFallback()
    {
        await using var db = CreateDb();
        var setup = await SeedPendingRound(db);
        var eic = Guid.NewGuid();
        db.Users.Add(ActiveUser(eic, UserRole.EditorInChief));
        await db.SaveChangesAsync();

        await Controller(db, setup.Reviewer1).Decide(setup.Assignment1, new(EditorialDecision.Approved, null), default);
        await Controller(db, setup.Reviewer2).Decide(setup.Assignment2, new(EditorialDecision.Rejected, "Reviewer 2 rejection reason"), default);
        await Controller(db, eic).Resolve("SeriesSubmission", setup.WorkId, new(EditorialDecision.Rejected, null), default);

        var work = await db.SeriesSubmissions.SingleAsync(x => x.Id == setup.WorkId);
        Assert.Equal(SubmissionStatus.EB_Rejected, work.Status);
        Assert.Equal("Reviewer 2 rejection reason", work.FeedbackMessage);
    }


    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static EditorialWorkflowController Controller(AppDbContext db, Guid userId)
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

    private static async Task<(Guid WorkId, Guid Reviewer1, Guid Reviewer2, Guid Assignment1, Guid Assignment2)> SeedPendingRound(AppDbContext db)
    {
        var mangaka = Guid.NewGuid();
        var tantou = Guid.NewGuid();
        var reviewer1 = Guid.NewGuid();
        var reviewer2 = Guid.NewGuid();
        db.Users.AddRange(
            ActiveUser(mangaka, UserRole.Mangaka),
            ActiveUser(tantou, UserRole.TantouEditor),
            ActiveUser(reviewer1, UserRole.EditorialBoard),
            ActiveUser(reviewer2, UserRole.EditorialBoard));
        var work = SeriesSubmission.CreateDraft(mangaka, "Proposal", null, null, null, "manuscript.pdf");
        work.SubmitDraft();
        var a1 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, work.Id, work.CurrentRound, reviewer1);
        var a2 = EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, work.Id, work.CurrentRound, reviewer2);
        db.SeriesSubmissions.Add(work);
        db.EditorialReviewAssignments.AddRange(a1, a2);
        await db.SaveChangesAsync();
        return (work.Id, reviewer1, reviewer2, a1.Id, a2.Id);
    }
}
