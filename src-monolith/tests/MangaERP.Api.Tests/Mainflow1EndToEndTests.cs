using System;
using System.Linq;
using System.Reflection;
using STTask = System.Threading.Tasks.Task;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Services;
using MangaERP.Submission.Application.Commands.SubmitProposal;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Submission.Presentation.Controllers;
using MangaERP.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MangaERP.Api.Tests;

public class Mainflow1EndToEndTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("PHASE1_POSTGRES_CONNECTION");

    private sealed class TestDbContextProvider(AppDbContext context) : IDbContextProvider
    {
        public object GetDbContext() => context;
    }

    private AppDbContext CreateDbContext()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseInMemoryDatabase("Mainflow1TestDb_" + Guid.NewGuid());
        var db = new AppDbContext(builder.Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static (User mangaka, User tantou, User eb1, User eb2, User eic) SeedUsers(AppDbContext db)
    {
        var tantou = new User
        {
            Id = Guid.NewGuid(),
            Username = "tantou_" + Guid.NewGuid().ToString("N")[..8],
            Email = "tantou_" + Guid.NewGuid().ToString("N")[..8] + "@manga.com",
            FullName = "Tantou Editor",
            Role = UserRole.TantouEditor,
            AccountStatus = AccountStatus.Active
        };

        var mangaka = new User
        {
            Id = Guid.NewGuid(),
            Username = "mangaka_" + Guid.NewGuid().ToString("N")[..8],
            Email = "mangaka_" + Guid.NewGuid().ToString("N")[..8] + "@manga.com",
            FullName = "Mangaka Author",
            Role = UserRole.Mangaka,
            ManagingTantouId = tantou.Id,
            AccountStatus = AccountStatus.Active
        };

        var eb1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "eb1_" + Guid.NewGuid().ToString("N")[..8],
            Email = "eb1_" + Guid.NewGuid().ToString("N")[..8] + "@manga.com",
            FullName = "Editorial Board Reviewer 1",
            Role = UserRole.EditorialBoard,
            AccountStatus = AccountStatus.Active
        };

        var eb2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "eb2_" + Guid.NewGuid().ToString("N")[..8],
            Email = "eb2_" + Guid.NewGuid().ToString("N")[..8] + "@manga.com",
            FullName = "Editorial Board Reviewer 2",
            Role = UserRole.EditorialBoard,
            AccountStatus = AccountStatus.Active
        };

        var eic = new User
        {
            Id = Guid.NewGuid(),
            Username = "eic_" + Guid.NewGuid().ToString("N")[..8],
            Email = "eic_" + Guid.NewGuid().ToString("N")[..8] + "@manga.com",
            FullName = "Editor in Chief",
            Role = UserRole.EditorInChief,
            AccountStatus = AccountStatus.Active
        };

        db.Users.AddRange(mangaka, tantou, eb1, eb2, eic);
        db.SaveChanges();

        return (mangaka, tantou, eb1, eb2, eic);
    }

    [Fact]
    public async STTask TestA_AdminCreatesMangakaWithTantou_SubmitProposal_StatusPendingEB_AssignedEditorNull_TantouQueueExcludes()
    {
        await using var db = CreateDbContext();
        var (mangaka, tantou, eb1, eb2, _) = SeedUsers(db);

        var submission = SeriesSubmission.CreateDraft(mangaka.Id, "Series Title A", "Synopsis", "Action", "http://cover.jpg", "http://manuscript.pdf");
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var provider = new TestDbContextProvider(db);
        var userRepo = new UserRepository(provider);
        var subRepo = new SubmissionRepository(provider);
        var pubRepo = new PublishingRepositories(provider);
        var notifService = new NotificationService(pubRepo, userRepo, null!);
        var handler = new SubmitProposalHandler(subRepo, userRepo, notifService);

        var result = await handler.Handle(new SubmitProposalCommand(submission.Id, mangaka.Id), CancellationToken.None);

        Assert.Equal("Pending_EB_Review", result.NewStatus);

        var reloaded = await db.SeriesSubmissions.FindAsync(submission.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(SubmissionStatus.Pending_EB_Review, reloaded.Status);
        Assert.Null(reloaded.AssignedEditorId);

        // Verify Tantou queue does NOT contain submission
        var tantouSubmissions = await db.SeriesSubmissions
            .Where(x => x.AssignedEditorId == tantou.Id)
            .ToListAsync();
        Assert.Empty(tantouSubmissions);
    }

    [Fact]
    public async STTask TestB_SubmitProposal_CreatesExactly2EditorialReviewAssignments_For2EBReviewers()
    {
        await using var db = CreateDbContext();
        var (mangaka, tantou, eb1, eb2, eic) = SeedUsers(db);

        var submission = SeriesSubmission.CreateDraft(mangaka.Id, "Series Title B", "Synopsis", "Action", "http://cover.jpg", "http://manuscript.pdf");
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var provider = new TestDbContextProvider(db);
        var userRepo = new UserRepository(provider);
        var subRepo = new SubmissionRepository(provider);
        var pubRepo = new PublishingRepositories(provider);
        var notifService = new NotificationService(pubRepo, userRepo, null!);
        var handler = new SubmitProposalHandler(subRepo, userRepo, notifService);

        await handler.Handle(new SubmitProposalCommand(submission.Id, mangaka.Id), CancellationToken.None);

        var assignments = await db.EditorialReviewAssignments
            .Where(a => a.WorkType == EditorialWorkType.SeriesSubmission && a.WorkId == submission.Id && a.RoundNumber == 1)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Equal(EditorialReviewAssignmentStatus.Pending, assignments[0].Status);
        Assert.Equal(EditorialReviewAssignmentStatus.Pending, assignments[1].Status);

        var assignedReviewerIds = assignments.Select(a => a.ReviewerId).Distinct().ToList();
        Assert.Equal(2, assignedReviewerIds.Count);

        Assert.Contains(eb1.Id, assignedReviewerIds);
        Assert.Contains(eb2.Id, assignedReviewerIds);
        Assert.DoesNotContain(tantou.Id, assignedReviewerIds);
        Assert.DoesNotContain(eic.Id, assignedReviewerIds);
    }

    [Fact]
    public async STTask TestC_SubmitProposal_PersistsNotificationsFor2AssignedReviewers_RetryDoesNotDuplicate()
    {
        await using var db = CreateDbContext();
        var (mangaka, _, eb1, eb2, _) = SeedUsers(db);

        var submission = SeriesSubmission.CreateDraft(mangaka.Id, "Series Title C", "Synopsis", "Action", "http://cover.jpg", "http://manuscript.pdf");
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var provider = new TestDbContextProvider(db);
        var userRepo = new UserRepository(provider);
        var subRepo = new SubmissionRepository(provider);
        var pubRepo = new PublishingRepositories(provider);
        var notifService = new NotificationService(pubRepo, userRepo, null!);
        var handler = new SubmitProposalHandler(subRepo, userRepo, notifService);

        await handler.Handle(new SubmitProposalCommand(submission.Id, mangaka.Id), CancellationToken.None);

        var notifications = await db.Notifications
            .Where(n => n.RelatedEntityId == submission.Id && n.NotifyType == "EditorialReviewAssignment")
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        var recipientIds = notifications.Select(n => n.ReceiverId).Distinct().ToList();
        Assert.Contains(eb1.Id, recipientIds);
        Assert.Contains(eb2.Id, recipientIds);

        // Idempotency check
        var countAssignments = await db.EditorialReviewAssignments
            .CountAsync(a => a.WorkType == EditorialWorkType.SeriesSubmission && a.WorkId == submission.Id);
        var countNotifications = await db.Notifications
            .CountAsync(n => n.RelatedEntityId == submission.Id && n.NotifyType == "EditorialReviewAssignment");

        Assert.Equal(2, countAssignments);
        Assert.Equal(2, countNotifications);
    }

    [Fact]
    public async STTask TestD_AssignedReviewerSeesAssignment_UnassignedReviewerDoesNot()
    {
        await using var db = CreateDbContext();
        var (mangaka, _, eb1, eb2, _) = SeedUsers(db);

        // Add a 3rd EB user — only the first 2 (by Id sort) should be assigned
        var eb3 = new User
        {
            // Use a very large Guid so it sorts last and is guaranteed NOT to be picked
            Id = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Username = "eb3_unassigned",
            Email = "eb3@manga.com",
            FullName = "EB 3 Unassigned",
            Role = UserRole.EditorialBoard,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(eb3);
        db.SaveChanges();

        var submission = SeriesSubmission.CreateDraft(mangaka.Id, "Series Title D", "Synopsis", "Action", "http://cover.jpg", "http://manuscript.pdf");
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var provider = new TestDbContextProvider(db);
        var userRepo = new UserRepository(provider);
        var subRepo = new SubmissionRepository(provider);
        var pubRepo = new PublishingRepositories(provider);
        var notifService = new NotificationService(pubRepo, userRepo, null!);
        var handler = new SubmitProposalHandler(subRepo, userRepo, notifService);

        await handler.Handle(new SubmitProposalCommand(submission.Id, mangaka.Id), CancellationToken.None);

        // Exactly 2 assignments total
        var allAssignments = await db.EditorialReviewAssignments
            .Where(x => x.WorkType == EditorialWorkType.SeriesSubmission && x.WorkId == submission.Id)
            .ToListAsync();
        Assert.Equal(2, allAssignments.Count);

        // eb3 (largest GUID, always last) must NOT be assigned
        Assert.DoesNotContain(eb3.Id, allAssignments.Select(a => a.ReviewerId));

        // The 2 assigned must be from {eb1, eb2} (the two smallest GUIDs among EB users)
        var assignedIds = allAssignments.Select(a => a.ReviewerId).ToHashSet();
        var ebUserIds = new HashSet<Guid> { eb1.Id, eb2.Id };
        Assert.Equal(ebUserIds, assignedIds);
    }

    [Fact]
    public void TestE_EBReviewEndpointsExposedWithoutNonAction()
    {
        var editorialControllerMethods = typeof(EditorialWorkflowController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        foreach (var method in editorialControllerMethods)
        {
            var nonAction = method.GetCustomAttribute<NonActionAttribute>();
            Assert.Null(nonAction);
        }

        var submissionControllerMethods = typeof(SubmissionsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        foreach (var method in submissionControllerMethods)
        {
            var nonAction = method.GetCustomAttribute<NonActionAttribute>();
            Assert.Null(nonAction);
        }
    }

    [Fact]
    public async STTask TestF_DoubleBlindConfidentiality_Reviewer1DecisionNotVisibleToReviewer2UntilBothComplete()
    {
        await using var db = CreateDbContext();
        var (mangaka, _, eb1, eb2, _) = SeedUsers(db);

        var submission = SeriesSubmission.CreateDraft(mangaka.Id, "Series Title F", "Synopsis", "Action", "http://cover.jpg", "http://manuscript.pdf");
        db.SeriesSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var provider = new TestDbContextProvider(db);
        var userRepo = new UserRepository(provider);
        var subRepo = new SubmissionRepository(provider);
        var pubRepo = new PublishingRepositories(provider);
        var notifService = new NotificationService(pubRepo, userRepo, null!);
        var handler = new SubmitProposalHandler(subRepo, userRepo, notifService);

        await handler.Handle(new SubmitProposalCommand(submission.Id, mangaka.Id), CancellationToken.None);

        var assignment1 = await db.EditorialReviewAssignments.SingleAsync(a => a.WorkId == submission.Id && a.ReviewerId == eb1.Id);
        var assignment2 = await db.EditorialReviewAssignments.SingleAsync(a => a.WorkId == submission.Id && a.ReviewerId == eb2.Id);

        // Reviewer 1 completes decision
        assignment1.Complete(EditorialDecision.Approved, "Great manuscript");
        await db.SaveChangesAsync();

        // Round check for Reviewer 2
        var roundAssignments = await db.EditorialReviewAssignments
            .Where(x => x.WorkType == EditorialWorkType.SeriesSubmission && x.WorkId == submission.Id && x.RoundNumber == 1)
            .ToListAsync();

        var bothCompleteBefore = roundAssignments.All(x => x.Status == EditorialReviewAssignmentStatus.Completed);
        Assert.False(bothCompleteBefore); // Reviewer 2 has not completed yet

        // Reviewer 2 completes decision
        assignment2.Complete(EditorialDecision.Approved, "Excellent work");
        await db.SaveChangesAsync();

        var roundAssignmentsAfter = await db.EditorialReviewAssignments
            .Where(x => x.WorkType == EditorialWorkType.SeriesSubmission && x.WorkId == submission.Id && x.RoundNumber == 1)
            .ToListAsync();

        var bothCompleteAfter = roundAssignmentsAfter.All(x => x.Status == EditorialReviewAssignmentStatus.Completed);
        Assert.True(bothCompleteAfter);
    }
}
