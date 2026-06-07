using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Identity.Infrastructure.Persistence;

public class IdentityDbContext : BaseDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete global filter
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        // Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200);          // [FIX-1]
            entity.Property(e => e.AvatarUrl).HasMaxLength(2048);        // [FIX-1]
            entity.Property(e => e.Role)
                .HasConversion(v => v.ToString(), v => Enum.Parse<UserRole>(v))
                .HasMaxLength(50);
        });

        // RefreshTokens [NEW]
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(512);
            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne(d => d.User)
                .WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
