using Inventory.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Repository.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<ProductCategoryEntity> ProductCategories => Set<ProductCategoryEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<InventoryMovementEntity> InventoryMovements => Set<InventoryMovementEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductCategoryEntity>(entity =>
        {
            entity.ToTable("ProductCategory");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
            entity.HasData(
                new ProductCategoryEntity { Id = 1, Name = "General" },
                new ProductCategoryEntity { Id = 2, Name = "Electronics" },
                new ProductCategoryEntity { Id = 3, Name = "Office Supplies" });
        });

        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.ToTable("Product", table => table.HasCheckConstraint("CK_Product_Quantity_NonNegative", "[Quantity] >= 0"));
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.IsDeleted).HasDefaultValue(false);
            entity.HasOne(product => product.ProductCategory)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.ProductCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasData(
                new ProductEntity { Id = 1, Name = "Sample Product", ProductCategoryId = 1, Quantity = 25, IsDeleted = false },
                new ProductEntity { Id = 2, Name = "Sample Electronics", ProductCategoryId = 2, Quantity = 10, IsDeleted = false });
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320);
            entity.Property(user => user.OAuthId).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.OAuthId).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            entity.HasData(new UserEntity { Id = 1, Name = "Seed User", OAuthId = "seed-user" });
        });

        modelBuilder.Entity<InventoryMovementEntity>(entity =>
        {
            entity.ToTable("InventoryMovement", table =>
            {
                table.HasCheckConstraint("CK_InventoryMovement_Quantity_NonNegative", "[Quantity] >= 0");
                table.HasCheckConstraint("CK_InventoryMovement_Type", "[Type] IN ('Add', 'Remove', 'Delete')");
            });
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.Type).HasMaxLength(16).IsRequired();
            entity.Property(movement => movement.CreatedAtUtc).HasColumnType("datetime2");
            entity.HasOne(movement => movement.Product)
                .WithMany(product => product.Movements)
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movement => movement.User)
                .WithMany(user => user.Movements)
                .HasForeignKey(movement => movement.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(movement => movement.CreatedAtUtc);
        });
    }
}
