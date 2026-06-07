namespace MangaERP.BuildingBlocks.Contracts.Dtos;

/// <summary>Shared user DTO used across service boundaries.</summary>
public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    string? FullName,
    string? AvatarUrl);
