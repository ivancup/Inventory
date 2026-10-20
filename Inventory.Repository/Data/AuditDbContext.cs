using Inventory.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Repository.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.ChangeType).HasMaxLength(32).IsRequired();
            entity.Property(log => log.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(log => log.OldValueJson).HasColumnType("nvarchar(max)");
            entity.Property(log => log.NewValueJson).HasColumnType("nvarchar(max)");
            entity.Property(log => log.ActorUserId).HasMaxLength(200);
            entity.Property(log => log.CreatedAtUtc).HasColumnType("datetime2");
            entity.HasIndex(log => log.CreatedAtUtc);
            entity.HasIndex(log => new { log.EntityName, log.EntityId });
        });
    }
}
