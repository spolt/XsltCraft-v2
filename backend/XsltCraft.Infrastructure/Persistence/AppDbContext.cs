using Microsoft.EntityFrameworkCore;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<UserXsltTemplate> UserXsltTemplates => Set<UserXsltTemplate>();

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

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(255).IsRequired();
            entity.Property(t => t.DocumentType).HasConversion<string>();
            entity.Property(t => t.BlockTree).HasColumnType("jsonb");
            entity.Property(t => t.XsltStoragePath).HasMaxLength(1000);
            entity.Property(t => t.ThumbnailUrl).HasMaxLength(500);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasOne(t => t.Owner)
                  .WithMany()
                  .HasForeignKey(t => t.OwnerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Type).HasConversion<string>();
            entity.Property(a => a.FilePath).HasMaxLength(1000).IsRequired();
            entity.Property(a => a.MimeType).HasMaxLength(100).IsRequired();
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasOne(a => a.Owner)
                  .WithMany()
                  .HasForeignKey(a => a.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserXsltTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(255).IsRequired();
            entity.Property(t => t.XsltContent).IsRequired();
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasOne(t => t.Owner)
                  .WithMany()
                  .HasForeignKey(t => t.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
