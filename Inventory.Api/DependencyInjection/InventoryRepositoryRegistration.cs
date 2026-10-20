using Inventory.Repository.Data;
using Inventory.Repository.Interfaces;
using Inventory.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api.DependencyInjection;

public static class InventoryRepositoryRegistration
{
    public static IServiceCollection AddInventoryRepository(this IServiceCollection services, IConfiguration configuration)
    {
        var inventoryConnection = configuration.GetConnectionString("Inventory");
        var auditConnection = configuration.GetConnectionString("AuditLogs");
        if (string.IsNullOrWhiteSpace(inventoryConnection) || string.IsNullOrWhiteSpace(auditConnection))
        {
            throw new InvalidOperationException("Both Inventory and AuditLogs connection strings must be configured.");
        }

        services.AddDbContext<InventoryDbContext>(options => options.UseSqlServer(inventoryConnection));
        services.AddDbContext<AuditDbContext>(options => options.UseSqlServer(auditConnection));
        services.AddScoped<IInventoryRepository>(provider => new InventoryRepository(
            provider.GetRequiredService<InventoryDbContext>(),
            provider.GetRequiredService<AuditDbContext>(),
            inventoryConnection));
        return services;
    }
}
