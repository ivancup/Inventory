using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes inventory movement history and stock-changing operations.</summary>
[ApiController]
[Route("api/v1/movements")]
public sealed class MovementsController(IInventoryService inventoryService) : ControllerBase
{
    /// <summary>Returns a page of movement history, optionally filtered by product.</summary>
    [HttpGet]
    [Authorize(Policy = "InventoryWrite")]
    public Task<IReadOnlyList<InventoryMovementResponse>> GetMovements(
        [FromQuery] int? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        inventoryService.GetMovementsAsync(productId, page, pageSize, cancellationToken);

    /// <summary>Adds the requested quantity to a product's stock.</summary>
    [HttpPost("add")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<InventoryMovementResponse> AddStock(
        InventoryMovementRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.AddStockAsync(request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);

    /// <summary>Removes the requested quantity from a product's stock.</summary>
    [HttpPost("remove")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<InventoryMovementResponse> RemoveStock(
        InventoryMovementRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.RemoveStockAsync(request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
