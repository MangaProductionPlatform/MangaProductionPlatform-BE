namespace MangaERP.Shared.Application.Ports;

/// <summary>
/// Provides access to the underlying database context for repository implementations in modules.
/// Declared as returning <c>object</c> to avoid any EF Core dependency in the Application layer.
/// Concrete implementation casts to AppDbContext internally.
/// </summary>
public interface IDbContextProvider
{
    object GetDbContext();
}
