using Microsoft.EntityFrameworkCore;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).HasMaxLength(255);
            entity.Property(u => u.GoogleId).HasMaxLength(255);
            entity.HasIndex(u => u.GoogleId).IsUnique().HasFilter("google_id IS NOT NULL");
            entity.Property(u => u.DisplayName).HasMaxLength(100);
            entity.Property(u => u.Role).HasConversion<string>();
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.Property(rt => rt.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
