using Inventory.Domain.ApiModels;

namespace Inventory.Repository.Interfaces;

/// <summary>Defines inventory data access for EF Core queries and transactional writes.</summary>
public interface IInventoryRepository
{
    /// <summary>Queries products, optionally including soft-deleted records.</summary>
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(bool includeDeleted, CancellationToken cancellationToken);
    /// <summary>Queries a product by identifier, returning null when it does not exist.</summary>
    Task<ProductResponse?> GetProductAsync(int id, CancellationToken cancellationToken);
    /// <summary>Queries all product categories.</summary>
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    /// <summary>Queries movement history with optional product filtering and paging.</summary>
    Task<IReadOnlyList<InventoryMovementResponse>> GetMovementsAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Queries a page of audit-log records.</summary>
    Task<PagedResponse<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Creates a product and its audit record transactionally.</summary>
    Task<ProductResponse> CreateProductAsync(ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Updates a product and its audit record transactionally.</summary>
    Task<ProductResponse> UpdateProductAsync(int id, ProductWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Updates a category and its audit record transactionally.</summary>
    Task<CategoryResponse> UpdateCategoryAsync(int id, CategoryWriteRequest request, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Applies a stock movement and records its audit data transactionally.</summary>
    Task<InventoryMovementResponse> ApplyMovementAsync(int productId, string type, int quantity, ActorIdentity actor, CancellationToken cancellationToken);
    /// <summary>Soft-deletes a product and records its movement and audit data transactionally.</summary>
    Task<InventoryMovementResponse> SoftDeleteProductAsync(int productId, ActorIdentity actor, CancellationToken cancellationToken);
}
