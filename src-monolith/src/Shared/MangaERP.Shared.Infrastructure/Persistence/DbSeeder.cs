using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Shared.Infrastructure.Persistence;

/// <summary>
/// Seeds the default Admin account and RBAC roles for Development environment.
/// Idempotent — checks existence before inserting.
/// Also migrates existing User.Role (single-role) data to UserRoles join table.
/// </summary>
public static class DbSeeder
{
    // Well-known fixed GUIDs for the seeded roles — stable across environments.
    private static readonly Guid RoleIdAdmin          = Guid.Parse("BBBBBBBB-0000-0000-0000-000000000001");
    private static readonly Guid RoleIdMangaka        = Guid.Parse("BBBBBBBB-0000-0000-0000-000000000002");
    private static readonly Guid RoleIdEditorialBoard = Guid.Parse("BBBBBBBB-0000-0000-0000-000000000003");
    private static readonly Guid RoleIdEditorInChief  = Guid.Parse("BBBBBBBB-0000-0000-0000-000000000004");
    private static readonly Guid RoleIdTantouEditor   = Guid.Parse("BBBBBBBB-0000-0000-0000-000000000005");

    private static readonly (Guid Id, string Name, string Description)[] SeedRoles =
    [
        (RoleIdAdmin,          RoleNames.Admin,          "System Administrator — full access"),
        (RoleIdMangaka,        RoleNames.Mangaka,        "Manga artist / creator"),
        (RoleIdEditorialBoard, RoleNames.EditorialBoard, "Editorial Board member — votes on submissions"),
        (RoleIdEditorInChief,  RoleNames.EditorInChief,  "Editor-in-Chief — arbitrates conflicts"),
        (RoleIdTantouEditor,   RoleNames.TantouEditor,   "Tantou Editor — manages assigned series"),
    ];

    public static async System.Threading.Tasks.Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        // ── 1. Seed RBAC Roles (idempotent) ─────────────────────────────────
        await SeedRolesAsync(db);

        // ── 2. Seed Admin user (idempotent) ──────────────────────────────────
        await SeedAdminAsync(db, config);

        // ── 3. Migrate existing User.Role → UserRoles join table ─────────────
        await MigrateUserRolesToRbacAsync(db);
    }

    private static async System.Threading.Tasks.Task SeedRolesAsync(AppDbContext db)
    {
        foreach (var (id, name, description) in SeedRoles)
        {
            var exists = await db.Roles.AnyAsync(r => r.Id == id);
            if (!exists)
            {
                db.Roles.Add(new Role_Entity
                {
                    Id = id,
                    Name = name,
                    Description = description
                });
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DbSeeder] RBAC Roles seeded/verified.");
    }

    private static async System.Threading.Tasks.Task SeedAdminAsync(AppDbContext db, IConfiguration config)
    {
        // Check if admin already exists in Users table
        var adminId = Guid.Parse("AAAAAAAA-0000-0000-0000-000000000001");
        if (await db.Users.AnyAsync(u => u.Id == adminId))
        {
            Console.WriteLine("[DbSeeder] Admin account already exists — skipping.");
            return;
        }

        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "CRITICAL SECURITY ERROR: 'Seed:AdminPassword' is not configured! " +
                "You must set the 'Seed__AdminPassword' environment variable (in your .env file or Railway dashboard) to seed the Admin account.");
        }

        var admin = new User
        {
            Id            = adminId,
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

        // Assign admin role in RBAC table
        await AssignRbacRoleAsync(db, adminId, RoleIdAdmin);
    }

    /// <summary>
    /// Migrates existing User.Role enum values to the UserRoles join table.
    /// Runs idempotently — skips users already in the join table.
    /// </summary>
    private static async System.Threading.Tasks.Task MigrateUserRolesToRbacAsync(AppDbContext db)
    {
        // Get all non-deleted users
        var users = await db.Users.IgnoreQueryFilters()
            .Where(u => !u.IsDeleted)
            .ToListAsync();

        int migrated = 0;
        foreach (var user in users)
        {
            // Check if user already has any entry in UserRoles
            var alreadyMigrated = await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id);
            if (alreadyMigrated) continue;

            var roleId = MapUserRoleToRoleId(user.Role);
            if (roleId.HasValue)
            {
                await AssignRbacRoleAsync(db, user.Id, roleId.Value);
                migrated++;
            }
        }

        if (migrated > 0)
            Console.WriteLine($"[DbSeeder] Migrated {migrated} users to RBAC UserRoles table.");
        else
            Console.WriteLine("[DbSeeder] No users needed RBAC migration.");
    }

    private static async System.Threading.Tasks.Task AssignRbacRoleAsync(AppDbContext db, Guid userId, Guid roleId)
    {
        var exists = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (exists) return;

        db.UserRoles.Add(new UserRole_Entity
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static Guid? MapUserRoleToRoleId(UserRole role) => role switch
    {
        UserRole.Admin          => RoleIdAdmin,
        UserRole.Mangaka        => RoleIdMangaka,
        UserRole.EditorialBoard => RoleIdEditorialBoard,
        UserRole.EditorInChief  => RoleIdEditorInChief,
        UserRole.TantouEditor   => RoleIdTantouEditor,
        _                       => null
    };
}
