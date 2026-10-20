using Inventory.Domain.ApiModels;

namespace Inventory.Service.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(bool includeDeleted, CancellationToken cancellationToken);
    Task<ProductResponse> GetProductAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryMovementResponse>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResponse<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductResponse> CreateProductAsync(ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateProductAsync(int id, ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    Task<CategoryResponse> UpdateCategoryAsync(int id, CategoryWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    Task<InventoryMovementResponse> AddStockAsync(InventoryMovementRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    Task<InventoryMovementResponse> RemoveStockAsync(InventoryMovementRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    Task<InventoryMovementResponse> DeleteProductAsync(int productId, ActorIdentity actor, CancellationToken cancellationToken);
}
