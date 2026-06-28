namespace MangaERP.Identity.Domain.Enums;

public enum UserRole
{
    // ⚠️ Admin = 0 is reserved — do NOT pass this in the provision API.
    // Admin is seeded automatically by the system at startup.
    Admin          = 0,

    // ── Provisionable roles (use these in POST /admin/accounts/provision) ──
    EditorialBoard  = 1,  // eb  (maps to RBAC role EDITORIAL_BOARD)
    TantouEditor    = 2,  // tt  (maps to RBAC role TANTOU_EDITOR)
    Mangaka         = 3,  // mgk (maps to RBAC role MANGAKA)
    Assistant       = 4,  // ast
    EditorInChief   = 5,  // eic (maps to RBAC role EDITOR_IN_CHIEF)

    // ── System-only (not provisionable) ──
    Reader         = 99
}

public enum AccountStatus
{
    PendingActivation,
    Active,
    Suspended,
    Deactivated
}

/// <summary>
/// Well-known RBAC role name constants — mirror the seeded Roles table rows.
/// Use these when checking roles from the UserRoles join table.
/// </summary>
public static class RoleNames
{
    public const string Admin           = "ADMIN";
    public const string Mangaka         = "MANGAKA";
    public const string EditorialBoard  = "EDITORIAL_BOARD";
    public const string EditorInChief   = "EDITOR_IN_CHIEF";
    public const string TantouEditor    = "TANTOU_EDITOR";
}
