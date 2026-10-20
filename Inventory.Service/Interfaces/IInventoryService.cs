using Inventory.Domain.ApiModels;

namespace Inventory.Service.Interfaces;

/// <summary>Defines inventory operations and their business-facing results.</summary>
public interface IInventoryService
{
    /// <summary>Gets products, optionally including soft-deleted records.</summary>
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(bool includeDeleted, CancellationToken cancellationToken);
    /// <summary>Gets a product by identifier.</summary>
    Task<ProductResponse> GetProductAsync(int id, CancellationToken cancellationToken);
    /// <summary>Gets all product categories.</summary>
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    /// <summary>Gets paginated inventory movements, optionally filtered by product.</summary>
    Task<IReadOnlyList<InventoryMovementResponse>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Gets a page of audit-log records.</summary>
    Task<PagedResponse<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Creates a product and records the acting user and audit data.</summary>
    Task<ProductResponse> CreateProductAsync(ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Updates a product and records the acting user and audit data.</summary>
    Task<ProductResponse> UpdateProductAsync(int id, ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Creates a category and records the acting user and audit data.</summary>
    Task<CategoryResponse> CreateCategoryAsync(CategoryWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Updates a category and records the acting user and audit data.</summary>
    Task<CategoryResponse> UpdateCategoryAsync(int id, CategoryWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Deletes a category and records the acting user and audit data.</summary>
    Task<CategoryResponse> DeleteCategoryAsync(int id, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Adds stock and records the resulting movement.</summary>
    Task<InventoryMovementResponse> AddStockAsync(InventoryMovementRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Removes stock and records the resulting movement.</summary>
    Task<InventoryMovementResponse> RemoveStockAsync(InventoryMovementRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Soft-deletes a product and records a zero-quantity delete movement.</summary>
    Task<InventoryMovementResponse> DeleteProductAsync(int productId, ActorIdentity actor, CancellationToken cancellationToken);
}
