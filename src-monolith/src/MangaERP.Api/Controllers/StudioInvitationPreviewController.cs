using System.Net.Mail;
using System.Security.Claims;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/studios")]
[Authorize(Roles = "Mangaka")]
public class StudioInvitationPreviewController : ControllerBase
{
    private readonly AppDbContext _db;
    public StudioInvitationPreviewController(AppDbContext db) => _db = db;
    public record PreviewRequest(string PersonalEmail);

    [HttpPost("{seriesId:guid}/invitations/preview")]
    public async Task<IActionResult> Preview(Guid seriesId, PreviewRequest request, CancellationToken ct)
    {
        var mangakaId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        if (!await _db.MangaSeries.AnyAsync(x => x.Id == seriesId && x.AuthorId == mangakaId, ct)) return Forbid();
        if (!IsCompleteEmail(request.PersonalEmail)) return BadRequest(new { message = "Enter a complete, valid personal email." });
        var normalized = request.PersonalEmail.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email.ToLower() == normalized || x.Username.ToLower() == normalized, ct))
            return BadRequest(new { message = "Internal email addresses are not accepted." });

        var matches = await _db.Users.Where(x => x.NormalizedPersonalEmail == normalized && !x.IsDeleted).ToListAsync(ct);
        if (matches.Count > 1) return Conflict(new { message = "Duplicate personal-email accounts require administrator resolution." });
        var user = matches.SingleOrDefault();
        if (user is null) return Ok(new { found = false, personalEmail = normalized });
        if (user.Role != UserRole.Assistant) return BadRequest(new { message = "This personal email belongs to a non-Assistant account." });
        return Ok(new { found = true, personalEmail = normalized, name = user.FullName, maskedInternalEmail = Mask(user.Email) });
    }

    private static bool IsCompleteEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return new MailAddress(value).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static string Mask(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return "***";
        var local = parts[0];
        return $"{(local.Length == 0 ? "*" : local[..1])}***@{parts[1]}";
    }
}
