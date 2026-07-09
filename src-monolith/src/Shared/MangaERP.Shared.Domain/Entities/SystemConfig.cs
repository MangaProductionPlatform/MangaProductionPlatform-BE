using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Shared.Domain.Entities;

/// <summary>
/// Chứa các cấu hình động của hệ thống (Key-Value), ví dụ: SamService:Url
/// </summary>
public class SystemConfig : Entity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
