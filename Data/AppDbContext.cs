using Microsoft.EntityFrameworkCore;

namespace kvwleidingmerch.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AccessLink> AccessLinks => Set<AccessLink>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<ProductUnavailableVariant> ProductUnavailableVariants => Set<ProductUnavailableVariant>();

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

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(p => p.Url)
                .HasMaxLength(500)
                .IsRequired();
            entity.HasMany(p => p.Sizes)
                .WithMany(s => s.Products);
            entity.HasMany(p => p.Colors)
                .WithMany(c => c.Products);
            entity.HasMany(p => p.UnavailableVariants)
                .WithOne(v => v.Product)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Size>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name)
                .HasMaxLength(50)
                .IsRequired();
            entity.HasIndex(s => s.Name)
                .IsUnique();
        });

        modelBuilder.Entity<Color>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(c => c.HexCode)
                .HasMaxLength(7)
                .IsRequired();
            entity.HasIndex(c => c.Name)
                .IsUnique();
        });

        modelBuilder.Entity<ProductUnavailableVariant>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => new { v.ProductId, v.SizeId, v.ColorId })
                .IsUnique();

            entity.HasOne(v => v.Size)
                .WithMany()
                .HasForeignKey(v => v.SizeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.Color)
                .WithMany()
                .HasForeignKey(v => v.ColorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public sealed class AccessLink
{
    public int Id { get; set; }

    public string UrlValue { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Product
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public ICollection<Size> Sizes { get; set; } = [];

    public ICollection<Color> Colors { get; set; } = [];

    public ICollection<ProductUnavailableVariant> UnavailableVariants { get; set; } = [];
}

public sealed class Size
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = [];
}

public sealed class Color
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string HexCode { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = [];
}

public sealed class ProductUnavailableVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = default!;

    public int SizeId { get; set; }

    public Size Size { get; set; } = default!;

    public int ColorId { get; set; }

    public Color Color { get; set; } = default!;
}
