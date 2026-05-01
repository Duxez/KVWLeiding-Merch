using Microsoft.EntityFrameworkCore;

namespace kvwleidingmerch.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AccessLink> AccessLinks => Set<AccessLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessLink>(entity =>
        {
            entity.HasKey(link => link.Id);
            entity.Property(link => link.UrlValue)
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(link => link.CreatedAtUtc)
                .IsRequired();
            entity.HasIndex(link => link.UrlValue)
                .IsUnique();
        });
    }
}

public sealed class AccessLink
{
    public int Id { get; set; }

    public string UrlValue { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
