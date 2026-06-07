using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MangaERP.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Interface for entities that support soft delete.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Base DbContext with audit field automation and soft delete interceptor.
/// All per-service DbContexts should inherit from this.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
