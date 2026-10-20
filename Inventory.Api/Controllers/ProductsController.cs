using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<ProductResponse>> GetProducts(
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        inventoryService.GetProductsAsync(includeDeleted, cancellationToken);

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public Task<ProductResponse> GetProduct(int id, CancellationToken cancellationToken) =>
        inventoryService.GetProductAsync(id, cancellationToken);

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        ProductWriteRequest request,
        CancellationToken cancellationToken)
    {
        var created = await inventoryService.CreateProductAsync(
            request,
            ActorIdentityFactory.FromPrincipal(User),
            cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public Task<ProductResponse> UpdateProduct(
        int id,
        ProductWriteRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.UpdateProductAsync(id, request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize]
    public Task<InventoryMovementResponse> DeleteProduct(int id, CancellationToken cancellationToken) =>
        inventoryService.DeleteProductAsync(id, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
