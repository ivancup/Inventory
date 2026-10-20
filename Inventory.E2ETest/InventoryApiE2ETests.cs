using Inventory.Domain.ApiModels;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Inventory.E2ETest;

[TestClass]
public sealed class InventoryApiE2ETests
{
    private static Uri BaseAddress =>
        new(Environment.GetEnvironmentVariable("INVENTORY_API_BASE_URL") ?? "http://127.0.0.1:8081");

    [TestMethod]
    public async Task HealthAndReadEndpoints_AreAvailable()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };

        var health = await client.GetAsync("/health");
        var products = await client.GetAsync("/api/v1/products");
        var categories = await client.GetAsync("/api/v1/categories");

        Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, products.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, categories.StatusCode);
        Assert.IsNotEmpty(await products.Content.ReadAsStringAsync());
        Assert.IsNotEmpty(await categories.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task MutatingEndpoints_RequireAuthentication()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };

        var response = await client.PostAsJsonAsync(
            "/api/v1/movements/add",
            new InventoryMovementRequest(1, 1));
        var createCategory = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new CategoryWriteRequest($"E2E category {Guid.NewGuid():N}"));
        var deleteCategory = await client.DeleteAsync("/api/v1/categories/1");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, createCategory.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, deleteCategory.StatusCode);
    }

    [TestMethod]
    public async Task AuthenticatedInventoryFlow_UpdatesStockAndAudit()
    {
        var token = Environment.GetEnvironmentVariable("INVENTORY_ACCESS_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Assert.Inconclusive("Set INVENTORY_ACCESS_TOKEN to a local Keycloak access token to run the mutation flow.");
        }

        using var client = new HttpClient { BaseAddress = BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoryName = $"E2E category {Guid.NewGuid():N}";
        var categoryResponse = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new CategoryWriteRequest(categoryName));
        Assert.AreEqual(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.IsNotNull(category);
        Assert.AreEqual(categoryName, category.Name);

        var createdResponse = await client.PostAsJsonAsync(
            "/api/v1/products",
            new ProductWriteRequest($"E2E product {Guid.NewGuid():N}", 1));
        Assert.AreEqual(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.IsNotNull(created);

        var categoryUpdate = await client.PutAsJsonAsync("/api/v1/categories/2", new CategoryWriteRequest("Electronics"));
        Assert.AreEqual(HttpStatusCode.OK, categoryUpdate.StatusCode);
        var productUpdate = await client.PutAsJsonAsync(
            $"/api/v1/products/{created.Id}",
            new ProductWriteRequest(created.Name, category.Id));
        Assert.AreEqual(HttpStatusCode.OK, productUpdate.StatusCode);
        var categoryInUse = await client.DeleteAsync($"/api/v1/categories/{category.Id}");
        Assert.AreEqual(HttpStatusCode.Conflict, categoryInUse.StatusCode);
        productUpdate = await client.PutAsJsonAsync(
            $"/api/v1/products/{created.Id}",
            new ProductWriteRequest(created.Name, 2));
        Assert.AreEqual(HttpStatusCode.OK, productUpdate.StatusCode);

        var add = await client.PostAsJsonAsync("/api/v1/movements/add", new InventoryMovementRequest(created.Id, 5));
        Assert.AreEqual(HttpStatusCode.OK, add.StatusCode);
        var zeroAdd = await client.PostAsJsonAsync("/api/v1/movements/add", new InventoryMovementRequest(created.Id, 0));
        Assert.AreEqual(HttpStatusCode.BadRequest, zeroAdd.StatusCode);
        var excessRemove = await client.PostAsJsonAsync("/api/v1/movements/remove", new InventoryMovementRequest(created.Id, 6));
        Assert.AreEqual(HttpStatusCode.Conflict, excessRemove.StatusCode);
        var remove = await client.PostAsJsonAsync("/api/v1/movements/remove", new InventoryMovementRequest(created.Id, 2));
        Assert.AreEqual(HttpStatusCode.OK, remove.StatusCode);

        var productResponse = await client.GetAsync($"/api/v1/products/{created.Id}");
        Assert.AreEqual(HttpStatusCode.OK, productResponse.StatusCode);
        var updated = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.AreEqual(3, updated?.Quantity);

        var connectionString = Environment.GetEnvironmentVariable("INVENTORY_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            await VerifyAuditFailureRollsBackAsync(client, created.Id, connectionString);
        }

        var audit = await client.GetFromJsonAsync<PagedResponse<AuditLogResponse>>("/api/v1/audit-logs?page=1&pageSize=100");
        Assert.IsNotNull(audit);
        Assert.IsTrue(audit.Items.Any(item => item.EntityName == "Product" && item.EntityId == created.Id.ToString()));

        var deleted = await client.DeleteAsync($"/api/v1/products/{created.Id}");
        Assert.AreEqual(HttpStatusCode.OK, deleted.StatusCode);
        var deleteMovement = await deleted.Content.ReadFromJsonAsync<InventoryMovementResponse>();
        Assert.AreEqual("Delete", deleteMovement?.Type);
        Assert.AreEqual(0, deleteMovement?.Quantity);
        var missing = await client.GetAsync($"/api/v1/products/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);

        var deletedCategoryResponse = await client.DeleteAsync($"/api/v1/categories/{category.Id}");
        Assert.AreEqual(HttpStatusCode.OK, deletedCategoryResponse.StatusCode);
        var deletedCategory = await deletedCategoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.AreEqual(category, deletedCategory);
    }

    [TestMethod]
    public async Task DatabaseRejectsNegativeProductQuantity()
    {
        var connectionString = Environment.GetEnvironmentVariable("INVENTORY_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive("Set INVENTORY_DB_CONNECTION to test the SQL Server quantity constraint.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.Product SET Quantity = -1 WHERE Id = 1";

        var exception = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

        Assert.AreEqual(547, exception.Number);
    }

    private static async Task VerifyAuditFailureRollsBackAsync(HttpClient client, int productId, string connectionString)
    {
        var current = await client.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{productId}");
        Assert.IsNotNull(current);
        var movementsBefore = await client.GetFromJsonAsync<IReadOnlyList<InventoryMovementResponse>>(
            $"/api/v1/movements?productId={productId}&page=1&pageSize=100");
        Assert.IsNotNull(movementsBefore);

        var triggerName = $"E2E_RejectAudit_{Guid.NewGuid():N}";
        var auditConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "AuditLogs"
        }.ConnectionString;

        await using var auditConnection = new SqlConnection(auditConnectionString);
        await auditConnection.OpenAsync();
        await using var auditCountBeforeCommand = auditConnection.CreateCommand();
        auditCountBeforeCommand.CommandText = "SELECT COUNT(1) FROM dbo.AuditLog WHERE EntityName = 'Product' AND EntityId = @EntityId";
        auditCountBeforeCommand.Parameters.AddWithValue("@EntityId", productId.ToString());
        var auditCountBefore = (int)(await auditCountBeforeCommand.ExecuteScalarAsync())!;
        await using (var createTrigger = auditConnection.CreateCommand())
        {
            createTrigger.CommandText =
                $"CREATE TRIGGER [dbo].[{triggerName}] ON [dbo].[AuditLog] AFTER INSERT AS BEGIN THROW 51000, 'E2E audit failure', 1; END;";
            await createTrigger.ExecuteNonQueryAsync();
        }

        try
        {
            var failedMovement = await client.PostAsJsonAsync(
                "/api/v1/movements/add",
                new InventoryMovementRequest(productId, 1));
            Assert.AreEqual(HttpStatusCode.InternalServerError, failedMovement.StatusCode);

            var after = await client.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{productId}");
            var movementsAfter = await client.GetFromJsonAsync<IReadOnlyList<InventoryMovementResponse>>(
                $"/api/v1/movements?productId={productId}&page=1&pageSize=100");
            Assert.AreEqual(current.Quantity, after?.Quantity);
            Assert.AreEqual(movementsBefore.Count, movementsAfter?.Count);
            await using var auditCountAfterCommand = auditConnection.CreateCommand();
            auditCountAfterCommand.CommandText = "SELECT COUNT(1) FROM dbo.AuditLog WHERE EntityName = 'Product' AND EntityId = @EntityId";
            auditCountAfterCommand.Parameters.AddWithValue("@EntityId", productId.ToString());
            Assert.AreEqual(auditCountBefore, (int)(await auditCountAfterCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var dropTrigger = auditConnection.CreateCommand();
            dropTrigger.CommandText = $"DROP TRIGGER [dbo].[{triggerName}]";
            await dropTrigger.ExecuteNonQueryAsync();
        }
    }
}
