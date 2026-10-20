using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Repository.Data;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AuditLogs")
            ?? "Server=localhost;Database=AuditLogs;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AuditDbContext(options);
    }
}
