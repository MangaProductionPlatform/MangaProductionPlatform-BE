using System.Globalization;
using System.Text;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Infrastructure.Services;

/// <summary>
/// Generates corporate usernames from Vietnamese full names.
/// Algorithm: {firstName}{lastInitials}.{roleCode}@company.com
/// Example: "Nguyễn Văn Anh" + Mangaka → "anhnv.mgk@company.com"
/// Handles collisions by appending incremental integers.
/// </summary>
public class UsernameGeneratorService : IUsernameGenerator
{
    private static readonly Dictionary<UserRole, string> RoleCodes = new()
    {
        { UserRole.Admin,          "adm" },
        { UserRole.EditorialBoard, "eb"  },
        { UserRole.TantouEditor,   "tt"  },
        { UserRole.Mangaka,        "mgk" },
        { UserRole.Assistant,      "ast" },
        { UserRole.EditorInChief,  "eic" },
        { UserRole.Reader,         "rdr" },
    };

    private readonly IUserRepository _userRepo;

    public UsernameGeneratorService(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<string> GenerateAsync(string fullName, UserRole role, CancellationToken ct = default)
    {
        var base_ = BuildBase(fullName);
        var roleCode = RoleCodes.TryGetValue(role, out var code) ? code : role.ToString().ToLower();
        var candidate = $"{base_}.{roleCode}@company.com";

        if (!await _userRepo.UsernameExistsAsync(candidate, ct))
            return candidate;

        // Collision handling: append 1, 2, 3, ...
        for (var i = 1; i <= 999; i++)
        {
            var numbered = $"{base_}.{roleCode}{i}@company.com";
            if (!await _userRepo.UsernameExistsAsync(numbered, ct))
                return numbered;
        }

        throw new InvalidOperationException($"Cannot generate unique username for '{fullName}' with role '{role}'.");
    }

    private static string BuildBase(string fullName)
    {
        var normalized = RemoveDiacritics(fullName.Trim());
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) return "user";

        // first_name = last token (in Vietnamese: first name comes last)
        var firstName = parts[^1].ToLower();

        // last_initials = first letter of each token except the last
        var initials = string.Concat(parts[..^1].Select(p => char.ToLower(p[0])));

        // Filter to only alphanumeric
        firstName = new string(firstName.Where(char.IsLetterOrDigit).ToArray());
        initials  = new string(initials.Where(char.IsLetterOrDigit).ToArray());

        return firstName + initials;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
