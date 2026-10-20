using Inventory.Domain.ApiModels;
using Inventory.Domain.Constants;
using Inventory.Domain.Exceptions;
using Inventory.Repository.Interfaces;
using Inventory.Service.Interfaces;
using Inventory.Service.Observability;
using Microsoft.Extensions.Logging;

namespace Inventory.Service;

public sealed class InventoryService(
    IInventoryRepository repository,
    InventoryMetrics metrics,
    ILogger<InventoryService> logger) : IInventoryService
{
    public Task<IReadOnlyList<ProductResponse>> GetProductsAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        repository.GetProductsAsync(includeDeleted, cancellationToken);

    public async Task<ProductResponse> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new InventoryException(400, "invalid_product_id", "Product id must be greater than zero.");
        }

        return await repository.GetProductAsync(id, cancellationToken)
            ?? throw new InventoryException(404, "product_not_found", "The product was not found.");
    }

    public Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        repository.GetCategoriesAsync(cancellationToken);

    public Task<IReadOnlyList<InventoryMovementResponse>> GetMovementsAsync(
        int? productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        if (productId is <= 0)
        {
            throw new InventoryException(400, "invalid_product_id", "Product id must be greater than zero.");
        }

        return repository.GetMovementsAsync(productId, page, pageSize, cancellationToken);
    }

    public Task<PagedResponse<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        return repository.GetAuditLogsAsync(page, pageSize, cancellationToken);
    }

    public Task<ProductResponse> CreateProductAsync(
        ProductWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        ValidateProduct(request);
        ValidateActor(actor);
        return repository.CreateProductAsync(request with { Name = request.Name.Trim() }, actor, cancellationToken);
    }

    public Task<ProductResponse> UpdateProductAsync(
        int id,
        ProductWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new InventoryException(400, "invalid_product_id", "Product id must be greater than zero.");
        }

        ValidateProduct(request);
        ValidateActor(actor);
        return repository.UpdateProductAsync(id, request with { Name = request.Name.Trim() }, actor, cancellationToken);
    }

    public Task<CategoryResponse> UpdateCategoryAsync(
        int id,
        CategoryWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new InventoryException(400, "invalid_category_id", "Category id must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            throw new InventoryException(400, "invalid_category_name", "Category name must contain between 1 and 120 characters.");
        }

        ValidateActor(actor);
        return repository.UpdateCategoryAsync(id, request with { Name = request.Name.Trim() }, actor, cancellationToken);
    }

    public async Task<InventoryMovementResponse> AddStockAsync(
        InventoryMovementRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        ValidateMovement(request, actor);
        var product = await GetProductAsync(request.ProductId, cancellationToken);
        if (product.Quantity > int.MaxValue - request.Quantity)
        {
            throw new InventoryException(409, "quantity_overflow", "The resulting inventory quantity exceeds the supported limit.");
        }

        var result = await repository.ApplyMovementAsync(request.ProductId, InventoryMovementType.Add, request.Quantity, actor, cancellationToken);
        metrics.RecordAdded(request.Quantity, product.Id, product.ProductCategoryId);
        InventoryLog.InventoryMoved(logger, InventoryMovementType.Add, request.Quantity, product.Id, product.ProductCategoryId);
        return result;
    }

    public async Task<InventoryMovementResponse> RemoveStockAsync(
        InventoryMovementRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        ValidateMovement(request, actor);
        var product = await GetProductAsync(request.ProductId, cancellationToken);
        var result = await repository.ApplyMovementAsync(request.ProductId, InventoryMovementType.Remove, request.Quantity, actor, cancellationToken);
        metrics.RecordRemoved(request.Quantity, product.Id, product.ProductCategoryId);
        InventoryLog.InventoryMoved(logger, InventoryMovementType.Remove, request.Quantity, product.Id, product.ProductCategoryId);
        return result;
    }

    public async Task<InventoryMovementResponse> DeleteProductAsync(
        int productId,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        if (productId <= 0)
        {
            throw new InventoryException(400, "invalid_product_id", "Product id must be greater than zero.");
        }

        ValidateActor(actor);
        var result = await repository.SoftDeleteProductAsync(productId, actor, cancellationToken);
        InventoryLog.ProductDeleted(logger, productId, actor.Subject);
        return result;
    }

    private static void ValidateProduct(ProductWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            throw new InventoryException(400, "invalid_product_name", "Product name must contain between 1 and 200 characters.");
        }

        if (request.ProductCategoryId <= 0)
        {
            throw new InventoryException(400, "invalid_category_id", "Category id must be greater than zero.");
        }
    }

    private static void ValidateMovement(InventoryMovementRequest request, ActorIdentity actor)
    {
        if (request.ProductId <= 0)
        {
            throw new InventoryException(400, "invalid_product_id", "Product id must be greater than zero.");
        }

        if (request.Quantity <= 0)
        {
            throw new InventoryException(400, "invalid_quantity", "Movement quantity must be greater than zero.");
        }

        ValidateActor(actor);
    }

    private static void ValidateActor(ActorIdentity actor)
    {
        if (string.IsNullOrWhiteSpace(actor.Subject))
        {
            throw new InventoryException(401, "invalid_actor", "The authenticated identity does not include a subject.");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            throw new InventoryException(400, "invalid_pagination", "Page must be positive and page size must be between 1 and 100.");
        }
    }
}
