using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Shared.Infrastructure.Persistence;

/// <summary>
/// Seeds the default Admin account for Development environment.
/// Idempotent — only inserts if no Admin user exists.
/// </summary>
public static class DbSeeder
{
    public static async System.Threading.Tasks.Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "CRITICAL SECURITY ERROR: 'Seed:AdminPassword' is not configured! " +
                "You must set the 'Seed__AdminPassword' environment variable (in your .env file or Railway dashboard) to seed the Admin account.");
        }

        var admin = new User
        {
            Id            = Guid.Parse("AAAAAAAA-0000-0000-0000-000000000001"),
            Username      = "sysadmin.adm@company.com",
            Email         = "sysadmin.adm@company.com",
            PersonalEmail = "admin@company.com",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role          = UserRole.Admin,
            FullName      = "System Administrator",
            AccountStatus = AccountStatus.Active,
            CreatedAt     = DateTime.UtcNow
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DbSeeder] Admin account seeded: {admin.Email}");
    }
}
