using System.Reflection;
using MangaERP.Api.Controllers;
using MangaERP.QA.Presentation.Controllers;
using MangaERP.Submission.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace MangaERP.Api.Tests;

public class LegacyRouteAndConcurrencyTests
{
    [Fact]
    public void LegacyEditorialEndpointsAreNotMapped()
    {
        var legacyMethods = new[]
        {
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.RequestRevision)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.CastVote)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.ResolveConflict)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.GetVotes)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.GetReviewResults)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.Approve)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.Reject)),
            typeof(SubmissionsController).GetMethod(nameof(SubmissionsController.GetQueue)),
            typeof(QasController).GetMethod(nameof(QasController.ApproveChapter))
        };

        Assert.All(legacyMethods, method =>
        {
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<NonActionAttribute>());
        });
    }

}
