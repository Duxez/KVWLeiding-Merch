using Microsoft.EntityFrameworkCore;

namespace kvwleidingmerch.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AccessLink> AccessLinks => Set<AccessLink>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<ProductUnavailableVariant> ProductUnavailableVariants => Set<ProductUnavailableVariant>();
    public DbSet<OrderRecipientEmail> OrderRecipientEmails => Set<OrderRecipientEmail>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ScheduledEmail> ScheduledEmails => Set<ScheduledEmail>();

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
            entity.Property(p => p.Price)
                .HasColumnType("decimal(10,2)")
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

        modelBuilder.Entity<OrderRecipientEmail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmailAddress)
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();
            entity.HasIndex(e => e.EmailAddress)
                .IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.CustomerEmail)
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(o => o.TotalAmount)
                .HasColumnType("decimal(10,2)")
                .IsRequired();
            entity.Property(o => o.CreatedAtUtc)
                .IsRequired();
            entity.Property(o => o.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(o => o.IsSentInScheduledEmail);
            entity.HasIndex(o => o.CreatedAtUtc);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ProductTitle)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(i => i.SizeName)
                .HasMaxLength(50);
            entity.Property(i => i.ColorName)
                .HasMaxLength(50);
            entity.Property(i => i.ColorHex)
                .HasMaxLength(7);
            entity.Property(i => i.UnitPrice)
                .HasColumnType("decimal(10,2)")
                .IsRequired();
            entity.Property(i => i.SizeId);
            entity.Property(i => i.ColorId);
            entity.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduledEmail>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Subject)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(s => s.ScheduledTimeUtc)
                .IsRequired();
            entity.Property(s => s.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(s => s.Timezone)
                .HasMaxLength(50)
                .HasDefaultValue("Europe/Amsterdam");
            entity.Property(s => s.CreatedAtUtc)
                .IsRequired();
            entity.HasIndex(s => s.ScheduledTimeUtc);
            entity.HasIndex(s => s.Status);
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

    public decimal Price { get; set; }

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

public sealed class OrderRecipientEmail
{
    public int Id { get; set; }

    public string EmailAddress { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Order
{
    public int Id { get; set; }

    public string CustomerEmail { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Pending";

    public bool IsSentInScheduledEmail { get; set; } = false;

    public ICollection<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = default!;

    public int ProductId { get; set; }

    public string ProductTitle { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int? SizeId { get; set; }

    public string SizeName { get; set; } = string.Empty;

    public int? ColorId { get; set; }

    public string ColorName { get; set; } = string.Empty;

    public string ColorHex { get; set; } = string.Empty;
}

public sealed class ScheduledEmail
{
    public int Id { get; set; }

    public string Subject { get; set; } = string.Empty;

    public DateTime ScheduledTimeUtc { get; set; }

    public string Timezone { get; set; } = "Europe/Amsterdam";

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SentAtUtc { get; set; }

    public string? ErrorMessage { get; set; }
}
