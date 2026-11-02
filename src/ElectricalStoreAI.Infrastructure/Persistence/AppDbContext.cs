using ElectricalStoreAI.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ElectricalStoreAI.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductReferenceImage> ProductReferenceImages => Set<ProductReferenceImage>();

    public DbSet<ProductImageEmbedding> ProductImageEmbeddings => Set<ProductImageEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");

            entity.HasKey(product => product.Id);

            entity.Property(product => product.Sku)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(product => product.Brand)
                .HasMaxLength(128);

            entity.Property(product => product.Category)
                .HasMaxLength(128);

            entity.Property(product => product.Description)
                .HasMaxLength(1024);

            entity.Property(product => product.SalePrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(product => product.SaleUnit)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(product => product.IsActive)
                .HasDefaultValue(true);

            entity.Property(product => product.CreatedAtUtc)
                .HasColumnType("datetime");

            entity.Property(product => product.UpdatedAtUtc)
                .HasColumnType("datetime");

            entity.HasIndex(product => product.Sku)
                .IsUnique();
        });

        modelBuilder.Entity<ProductReferenceImage>(entity =>
        {
            entity.ToTable("ProductReferenceImages");

            entity.HasKey(image => image.Id);

            entity.Property(image => image.ProductId)
                .IsRequired();

            entity.Property(image => image.StorageKey)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(image => image.ContentType)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(image => image.CreatedAtUtc)
                .HasColumnType("datetime");

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImageEmbedding>(entity =>
        {
            entity.ToTable("ProductImageEmbeddings");

            entity.HasKey(embedding => embedding.Id);

            entity.Property(embedding => embedding.ProductReferenceImageId)
                .IsRequired();

            entity.Property(embedding => embedding.ModelKey)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(embedding => embedding.CreatedAtUtc)
                .HasColumnType("datetime");

            entity.HasOne<ProductReferenceImage>()
                .WithMany()
                .HasForeignKey(embedding => embedding.ProductReferenceImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
