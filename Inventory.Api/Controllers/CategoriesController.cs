using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes product category queries and mutations.</summary>
[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(IInventoryService inventoryService) : ControllerBase
{
    /// <summary>Returns all product categories.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<CategoryResponse>> GetCategories(CancellationToken cancellationToken) =>
        inventoryService.GetCategoriesAsync(cancellationToken);

    /// <summary>Creates a product category.</summary>
    [HttpPost]
    [Authorize(Policy = "InventoryWrite")]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var created = await inventoryService.CreateCategoryAsync(
            request,
            ActorIdentityFactory.FromPrincipal(User),
            cancellationToken);
        return CreatedAtAction(nameof(GetCategories), new { }, created);
    }

    /// <summary>Updates the name of a product category.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<CategoryResponse> UpdateCategory(
        int id,
        CategoryWriteRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.UpdateCategoryAsync(id, request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);

    /// <summary>Deletes a product category that is not assigned to any products.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<CategoryResponse> DeleteCategory(int id, CancellationToken cancellationToken) =>
        inventoryService.DeleteCategoryAsync(id, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
