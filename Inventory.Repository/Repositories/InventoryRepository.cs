using Dapper;
using Inventory.Domain.ApiModels;
using Inventory.Domain.Constants;
using Inventory.Domain.Exceptions;
using Inventory.Repository.Data;
using Inventory.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;

namespace Inventory.Repository.Repositories;

public sealed class InventoryRepository(
    InventoryDbContext inventoryDb,
    AuditDbContext auditDb,
    string inventoryConnectionString) : IInventoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = inventoryDb.Products.AsNoTracking().Include(product => product.ProductCategory).AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(product => !product.IsDeleted);
        }

        return await query
            .OrderBy(product => product.Id)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.ProductCategoryId,
                product.ProductCategory!.Name,
                product.Quantity,
                product.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductResponse?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        return await inventoryDb.Products
            .AsNoTracking()
            .Where(product => product.Id == id && !product.IsDeleted)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.ProductCategoryId,
                product.ProductCategory!.Name,
                product.Quantity,
                product.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        return await inventoryDb.ProductCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse(category.Id, category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovementResponse>> GetMovementsAsync(
        int? productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = inventoryDb.InventoryMovements.AsNoTracking();
        if (productId is not null)
        {
            query = query.Where(movement => movement.ProductId == productId);
        }

        return await query
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(movement => new InventoryMovementResponse(
                movement.Id,
                movement.ProductId,
                movement.Type,
                movement.Quantity,
                movement.UserId,
                movement.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<AuditLogResponse>> GetAuditLogsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = auditDb.AuditLogs.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.ChangeType,
                log.EntityName,
                log.EntityId,
                log.OldValueJson,
                log.NewValueJson,
                log.ActorUserId,
                log.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditLogResponse>(items, page, pageSize, totalCount);
    }

    public async Task<ProductResponse> CreateProductAsync(
        ProductWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var userId = await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var categoryExists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.ProductCategory WHERE Id = @CategoryId",
                new { CategoryId = request.ProductCategoryId },
                transaction,
                cancellationToken: cancellationToken));
        if (categoryExists == 0)
        {
            throw new InventoryException(404, "category_not_found", "The requested product category was not found.");
        }

        var productId = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO dbo.Product (Name, ProductCategoryId, Quantity, IsDeleted)
                OUTPUT INSERTED.Id
                VALUES (@Name, @ProductCategoryId, 0, 0);
                """,
                request,
                transaction,
                cancellationToken: cancellationToken));
        var created = await GetProductAsync(connection, transaction, productId, cancellationToken);
        await InsertAuditAsync(connection, transaction, "Created", "Product", productId.ToString(), null, created, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<ProductResponse> UpdateProductAsync(
        int id,
        ProductWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var current = await GetProductAsync(connection, transaction, id, cancellationToken);
        if (current is null)
        {
            throw new InventoryException(404, "product_not_found", "The product was not found.");
        }

        var categoryExists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.ProductCategory WHERE Id = @CategoryId",
                new { CategoryId = request.ProductCategoryId },
                transaction,
                cancellationToken: cancellationToken));
        if (categoryExists == 0)
        {
            throw new InventoryException(404, "category_not_found", "The requested product category was not found.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE dbo.Product SET Name = @Name, ProductCategoryId = @ProductCategoryId WHERE Id = @Id AND IsDeleted = 0",
                new { request.Name, request.ProductCategoryId, Id = id },
                transaction,
                cancellationToken: cancellationToken));
        var updated = await GetProductAsync(connection, transaction, id, cancellationToken)
            ?? throw new InventoryException(404, "product_not_found", "The product was not found.");
        await InsertAuditAsync(connection, transaction, "Updated", "Product", id.ToString(), current, updated, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(
        int id,
        CategoryWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var currentName = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT Name FROM dbo.ProductCategory WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (currentName is null)
        {
            throw new InventoryException(404, "category_not_found", "The product category was not found.");
        }

        var conflictingName = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.ProductCategory WHERE Name = @Name AND Id <> @Id",
                new { request.Name, Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (conflictingName > 0)
        {
            throw new InventoryException(409, "category_name_conflict", "A product category with that name already exists.");
        }

        var current = new CategoryResponse(id, currentName);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE dbo.ProductCategory SET Name = @Name WHERE Id = @Id",
                new { request.Name, Id = id },
                transaction,
                cancellationToken: cancellationToken));
        var updated = new CategoryResponse(id, request.Name);
        await InsertAuditAsync(connection, transaction, "Updated", "ProductCategory", id.ToString(), current, updated, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<CategoryResponse> CreateCategoryAsync(
        CategoryWriteRequest request,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var existingCategory = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.ProductCategory WITH (UPDLOCK, HOLDLOCK) WHERE Name = @Name",
                new { request.Name },
                transaction,
                cancellationToken: cancellationToken));
        if (existingCategory > 0)
        {
            throw new InventoryException(409, "category_name_conflict", "A product category with that name already exists.");
        }

        var id = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO dbo.ProductCategory (Name)
                OUTPUT INSERTED.Id
                VALUES (@Name);
                """,
                request,
                transaction,
                cancellationToken: cancellationToken));
        var created = new CategoryResponse(id, request.Name);
        await InsertAuditAsync(connection, transaction, "Created", "ProductCategory", id.ToString(), null, created, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<CategoryResponse> DeleteCategoryAsync(
        int id,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var name = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT Name FROM dbo.ProductCategory WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (name is null)
        {
            throw new InventoryException(404, "category_not_found", "The product category was not found.");
        }

        var productCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.Product WITH (UPDLOCK, HOLDLOCK) WHERE ProductCategoryId = @Id",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (productCount > 0)
        {
            throw new InventoryException(409, "category_in_use", "The product category cannot be deleted while products reference it.");
        }

        var deleted = new CategoryResponse(id, name);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM dbo.ProductCategory WHERE Id = @Id",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        await InsertAuditAsync(connection, transaction, "Deleted", "ProductCategory", id.ToString(), deleted, null, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async Task<InventoryMovementResponse> ApplyMovementAsync(
        int productId,
        string type,
        int quantity,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            throw new InventoryException(400, "invalid_quantity", "Movement quantity must be greater than zero.");
        }

        if (type is not (InventoryMovementType.Add or InventoryMovementType.Remove))
        {
            throw new InventoryException(400, "invalid_movement_type", "Only Add and Remove movements are accepted.");
        }

        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var userId = await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var current = await GetProductAsync(connection, transaction, productId, cancellationToken);
        if (current is null)
        {
            throw new InventoryException(404, "product_not_found", "The product was not found.");
        }

        if (type == InventoryMovementType.Remove && current.Quantity < quantity)
        {
            throw new InventoryException(409, "insufficient_stock", "The requested quantity exceeds available stock.");
        }

        if (type == InventoryMovementType.Add && current.Quantity > int.MaxValue - quantity)
        {
            throw new InventoryException(409, "quantity_overflow", "The resulting inventory quantity exceeds the supported limit.");
        }

        var nextQuantity = type == InventoryMovementType.Add
            ? current.Quantity + quantity
            : current.Quantity - quantity;
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE dbo.Product SET Quantity = @Quantity WHERE Id = @ProductId AND IsDeleted = 0",
                new { Quantity = nextQuantity, ProductId = productId },
                transaction,
                cancellationToken: cancellationToken));
        var updated = current with { Quantity = nextQuantity };
        await InsertAuditAsync(connection, transaction, "Updated", "Product", productId.ToString(), current, updated, actor.Subject, cancellationToken);
        var movement = await InsertMovementAsync(connection, transaction, productId, type, quantity, userId, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return movement;
    }

    public async Task<InventoryMovementResponse> SoftDeleteProductAsync(
        int productId,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(inventoryConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var userId = await EnsureActorAsync(connection, transaction, actor, cancellationToken);
        var current = await GetProductAsync(connection, transaction, productId, cancellationToken);
        if (current is null)
        {
            throw new InventoryException(404, "product_not_found", "The product was not found.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE dbo.Product SET IsDeleted = 1 WHERE Id = @Id AND IsDeleted = 0",
                new { Id = productId },
                transaction,
                cancellationToken: cancellationToken));
        var deleted = current with { IsDeleted = true };
        await InsertAuditAsync(connection, transaction, "Deleted", "Product", productId.ToString(), current, deleted, actor.Subject, cancellationToken);
        var movement = await InsertMovementAsync(connection, transaction, productId, InventoryMovementType.Delete, 0, userId, actor.Subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return movement;
    }

    private static async Task<ProductResponse> GetProductAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<ProductResponse>(
            new CommandDefinition(
                """
                SELECT p.Id, p.Name, p.ProductCategoryId, c.Name AS CategoryName, p.Quantity, p.IsDeleted
                FROM dbo.Product AS p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ProductCategory AS c ON c.Id = p.ProductCategoryId
                WHERE p.Id = @Id AND p.IsDeleted = 0
                """,
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken))
            ?? throw new InventoryException(404, "product_not_found", "The product was not found.");
    }

    private async Task<int> EnsureActorAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        var existingId = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                "SELECT Id FROM dbo.[User] WITH (UPDLOCK, HOLDLOCK) WHERE OAuthId = @OAuthId",
                new { OAuthId = actor.Subject },
                transaction,
                cancellationToken: cancellationToken));
        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var name = string.IsNullOrWhiteSpace(actor.Name) ? actor.Subject : actor.Name;
        var userId = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO dbo.[User] (Name, Email, OAuthId)
                OUTPUT INSERTED.Id
                VALUES (@Name, @Email, @OAuthId);
                """,
                new { Name = name, actor.Email, OAuthId = actor.Subject },
                transaction,
                cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            "Created",
            "User",
            userId.ToString(),
            null,
            new { Id = userId, Name = name, actor.Email, OAuthId = actor.Subject },
            actor.Subject,
            cancellationToken);
        return userId;
    }

    private static async Task<InventoryMovementResponse> InsertMovementAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        int productId,
        string type,
        int quantity,
        int userId,
        string actorSubject,
        CancellationToken cancellationToken)
    {
        var movementId = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO dbo.InventoryMovement (ProductId, Type, Quantity, UserId, CreatedAtUtc)
                OUTPUT INSERTED.Id
                VALUES (@ProductId, @Type, @Quantity, @UserId, @CreatedAtUtc);
                """,
                new { ProductId = productId, Type = type, Quantity = quantity, UserId = userId, CreatedAtUtc = DateTime.UtcNow },
                transaction,
                cancellationToken: cancellationToken));
        var movement = await connection.QuerySingleAsync<InventoryMovementResponse>(
            new CommandDefinition(
                "SELECT Id, ProductId, Type, Quantity, UserId, CreatedAtUtc FROM dbo.InventoryMovement WHERE Id = @Id",
                new { Id = movementId },
                transaction,
                cancellationToken: cancellationToken));
        await InsertAuditAsync(connection, transaction, "Created", "InventoryMovement", movementId.ToString(), null, movement, actorSubject, cancellationToken);
        return movement;
    }

    private static async Task InsertAuditAsync(
        SqlConnection connection,
        IDbTransaction transaction,
        string changeType,
        string entityName,
        string entityId,
        object? oldValue,
        object? newValue,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO AuditLogs.dbo.AuditLog
                    (ChangeType, EntityName, EntityId, OldValueJson, NewValueJson, ActorUserId, CreatedAtUtc)
                VALUES
                    (@ChangeType, @EntityName, @EntityId, @OldValueJson, @NewValueJson, @ActorUserId, @CreatedAtUtc)
                """,
                new
                {
                    ChangeType = changeType,
                    EntityName = entityName,
                    EntityId = entityId,
                    OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
                    NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
                    ActorUserId = actorUserId,
                    CreatedAtUtc = DateTime.UtcNow
                },
                transaction,
                cancellationToken: cancellationToken));
    }
}
