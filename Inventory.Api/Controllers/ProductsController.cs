using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes product queries and authenticated product mutations.</summary>
[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(IInventoryService inventoryService) : ControllerBase
{
    /// <summary>Returns products, optionally including soft-deleted records.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<ProductResponse>> GetProducts(
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        inventoryService.GetProductsAsync(includeDeleted, cancellationToken);

    /// <summary>Returns a product by its identifier.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public Task<ProductResponse> GetProduct(int id, CancellationToken cancellationToken) =>
        inventoryService.GetProductAsync(id, cancellationToken);

    /// <summary>Creates a product and returns its location and representation.</summary>
    [HttpPost]
    [Authorize(Policy = "InventoryWrite")]
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

    /// <summary>Updates a product's name and category.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<ProductResponse> UpdateProduct(
        int id,
        ProductWriteRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.UpdateProductAsync(id, request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);

    /// <summary>Soft-deletes a product and returns its delete movement.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<InventoryMovementResponse> DeleteProduct(int id, CancellationToken cancellationToken) =>
        inventoryService.DeleteProductAsync(id, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
