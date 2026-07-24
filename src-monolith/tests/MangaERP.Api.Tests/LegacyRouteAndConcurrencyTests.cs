using MangaERP.Submission.Presentation.Controllers;

namespace MangaERP.Api.Tests;

public class LegacyRouteAndConcurrencyTests
{
    [Fact]
    public void LegacyRequestRevisionEndpointIsRemoved()
    {
        var method = typeof(SubmissionsController).GetMethod("RequestRevision");
        Assert.Null(method);
    }
}
