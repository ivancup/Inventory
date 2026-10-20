using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/v1/movements")]
public sealed class MovementsController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Task<IReadOnlyList<InventoryMovementResponse>> GetMovements(
        [FromQuery] int? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        inventoryService.GetMovementsAsync(productId, page, pageSize, cancellationToken);

    [HttpPost("add")]
    [Authorize]
    public Task<InventoryMovementResponse> AddStock(
        InventoryMovementRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.AddStockAsync(request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);

    [HttpPost("remove")]
    [Authorize]
    public Task<InventoryMovementResponse> RemoveStock(
        InventoryMovementRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.RemoveStockAsync(request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
