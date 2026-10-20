using Inventory.Domain.ApiModels;
using Inventory.Domain.Constants;
using Inventory.Domain.Exceptions;
using Inventory.Repository.Interfaces;
using Inventory.Service;
using Inventory.Service.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Inventory.UnitTest.Services;

[TestClass]
public sealed class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> repository = new();
    private readonly InventoryService service;
    private readonly ActorIdentity actor = new("subject-1", "Test User", "test@example.local");

    public InventoryServiceTests()
    {
        service = new InventoryService(repository.Object, new InventoryMetrics(), NullLogger<InventoryService>.Instance);
    }

    [TestMethod]
    public async Task AddStock_RecordsAddMovement()
    {
        var product = new ProductResponse(1, "Item", 2, "Electronics", 3, false);
        repository.Setup(item => item.GetProductAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        repository.Setup(item => item.ApplyMovementAsync(1, InventoryMovementType.Add, 4, actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryMovementResponse(7, 1, InventoryMovementType.Add, 4, 2, DateTime.UtcNow));

        var result = await service.AddStockAsync(new InventoryMovementRequest(1, 4), actor, CancellationToken.None);

        Assert.AreEqual(InventoryMovementType.Add, result.Type);
        Assert.AreEqual(4, result.Quantity);
        repository.VerifyAll();
    }

    [TestMethod]
    public async Task RemoveStock_RecordsRemoveMovement()
    {
        var product = new ProductResponse(3, "Item", 1, "General", 9, false);
        repository.Setup(item => item.GetProductAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        repository.Setup(item => item.ApplyMovementAsync(3, InventoryMovementType.Remove, 2, actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryMovementResponse(8, 3, InventoryMovementType.Remove, 2, 2, DateTime.UtcNow));

        var result = await service.RemoveStockAsync(new InventoryMovementRequest(3, 2), actor, CancellationToken.None);

        Assert.AreEqual(InventoryMovementType.Remove, result.Type);
        repository.VerifyAll();
    }

    [TestMethod]
    public async Task AddStock_RejectsZeroQuantityBeforeRepositoryCall()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.AddStockAsync(new InventoryMovementRequest(1, 0), actor, CancellationToken.None));

        Assert.AreEqual("invalid_quantity", exception.Code);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task AddStock_RejectsIntegerOverflow()
    {
        repository.Setup(item => item.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductResponse(1, "Item", 1, "General", int.MaxValue, false));
        repository.Setup(item => item.ApplyMovementAsync(1, InventoryMovementType.Add, 1, actor, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryException(409, "quantity_overflow", "The resulting inventory quantity exceeds the supported limit."));

        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.AddStockAsync(new InventoryMovementRequest(1, 1), actor, CancellationToken.None));

        Assert.AreEqual("quantity_overflow", exception.Code);
        repository.Verify(item => item.ApplyMovementAsync(
            1,
            InventoryMovementType.Add,
            1,
            actor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RemoveStock_RejectsNegativeQuantityBeforeRepositoryCall()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.RemoveStockAsync(new InventoryMovementRequest(1, -1), actor, CancellationToken.None));

        Assert.AreEqual(400, exception.StatusCode);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RemoveStock_PropagatesInsufficientStockConflict()
    {
        repository.Setup(item => item.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductResponse(1, "Item", 1, "General", 2, false));
        repository.Setup(item => item.ApplyMovementAsync(1, InventoryMovementType.Remove, 3, actor, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryException(409, "insufficient_stock", "The requested quantity exceeds available stock."));

        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.RemoveStockAsync(new InventoryMovementRequest(1, 3), actor, CancellationToken.None));

        Assert.AreEqual(409, exception.StatusCode);
    }

    [TestMethod]
    public async Task CreateProduct_RejectsBlankName()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.CreateProductAsync(new ProductWriteRequest(" ", 1), actor, CancellationToken.None));

        Assert.AreEqual("invalid_product_name", exception.Code);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task UpdateCategory_RejectsOverlongName()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.UpdateCategoryAsync(1, new CategoryWriteRequest(new string('x', 121)), actor, CancellationToken.None));

        Assert.AreEqual(400, exception.StatusCode);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task AuditQuery_RejectsPageSizeOverMaximum()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.GetAuditLogsAsync(1, 101, CancellationToken.None));

        Assert.AreEqual("invalid_pagination", exception.Code);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task AuditQuery_RejectsPageThatOverflowsOffset()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.GetAuditLogsAsync(21_474_838, 100, CancellationToken.None));

        Assert.AreEqual("invalid_pagination", exception.Code);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task MovementQuery_RejectsPageThatOverflowsOffset()
    {
        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.GetMovementsAsync(null, 21_474_838, 100, CancellationToken.None));

        Assert.AreEqual("invalid_pagination", exception.Code);
        repository.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetProduct_ReturnsNotFoundWhenMissing()
    {
        repository.Setup(item => item.GetProductAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse?)null);

        var exception = await Assert.ThrowsAsync<InventoryException>(
            () => service.GetProductAsync(99, CancellationToken.None));

        Assert.AreEqual(404, exception.StatusCode);
    }
}
