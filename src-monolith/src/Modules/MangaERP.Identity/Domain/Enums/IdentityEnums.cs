namespace MangaERP.Identity.Domain.Enums;

public enum UserRole
{
    // ⚠️ Admin = 0 is reserved — do NOT pass this in the provision API.
    // Admin is seeded automatically by the system at startup.
    Admin          = 0,

    // ── Provisionable roles (use these in POST /admin/accounts/provision) ──
    EditorialBoard = 1,  // eb
    TantouEditor   = 2,  // tt
    Mangaka        = 3,  // mgk
    Assistant      = 4,  // ast

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
