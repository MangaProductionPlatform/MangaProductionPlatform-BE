using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;

namespace MangaERP.Shared.Infrastructure.Persistence;

/// <summary>Concrete DbContext provider — registered by Shared.Infrastructure DI.</summary>
public class AppDbContextProvider : IDbContextProvider
{
    private readonly AppDbContext _db;
    public AppDbContextProvider(AppDbContext db) => _db = db;
    public object GetDbContext() => _db;
}
