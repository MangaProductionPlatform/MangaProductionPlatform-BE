using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MangaERP.Shared.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(150);
        entity.HasIndex(e => e.Username).IsUnique();
        entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.PersonalEmail).HasMaxLength(256);
        entity.HasIndex(e => e.PersonalEmail);
        entity.Property(e => e.NormalizedPersonalEmail).HasMaxLength(256);
        entity.HasIndex(e => e.NormalizedPersonalEmail).IsUnique();
        entity.Property(e => e.PasswordHash).IsRequired();
        entity.Property(e => e.FullName).HasMaxLength(200);
        entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
        entity.Property(e => e.PhoneNumber).HasMaxLength(50);
        entity.Property(e => e.PenName).HasMaxLength(100);
        entity.Property(e => e.DrawingSoftwares).HasMaxLength(500);
        entity.Property(e => e.BankAccountNumber).HasMaxLength(100);
        entity.Property(e => e.Role)
            .HasConversion(v => v.ToString(), v => Enum.Parse<UserRole>(v)).HasMaxLength(50);
        entity.Property(e => e.AccountStatus)
            .HasConversion(v => v.ToString(), v => Enum.Parse<AccountStatus>(v))
            .HasMaxLength(50);

        entity.HasOne(e => e.ManagingTantou)
            .WithMany()
            .HasForeignKey(e => e.ManagingTantouId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

// ── RBAC: Roles table ─────────────────────────────────────────────────────────

public class RoleEntityConfiguration : IEntityTypeConfiguration<Role_Entity>
{
    public void Configure(EntityTypeBuilder<Role_Entity> entity)
    {
        entity.ToTable("Roles");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.Name).IsUnique();
        entity.Property(e => e.Description).HasMaxLength(500);
    }
}

// ── RBAC: UserRoles join table ────────────────────────────────────────────────

public class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRole_Entity>
{
    public void Configure(EntityTypeBuilder<UserRole_Entity> entity)
    {
        entity.ToTable("UserRoles");
        entity.HasKey(e => new { e.UserId, e.RoleId });

        entity.HasOne(e => e.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Token).IsRequired().HasMaxLength(512);
        entity.HasIndex(e => e.Token).IsUnique();
        entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
            .HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.UserId, e.IsRevoked });
    }
}

public class InvitationTokenConfiguration : IEntityTypeConfiguration<InvitationToken>
{
    public void Configure(EntityTypeBuilder<InvitationToken> entity)
    {
        entity.ToTable("InvitationTokens");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Token).IsRequired();
        entity.Property(e => e.PersonalEmail).IsRequired().HasMaxLength(256);
        entity.HasOne(d => d.User).WithMany()
            .HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => e.UserId);
    }
}
