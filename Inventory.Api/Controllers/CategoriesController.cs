using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes product category queries and updates.</summary>
[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(IInventoryService inventoryService) : ControllerBase
{
    /// <summary>Returns all product categories.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<CategoryResponse>> GetCategories(CancellationToken cancellationToken) =>
        inventoryService.GetCategoriesAsync(cancellationToken);

    /// <summary>Updates the name of a product category.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "InventoryWrite")]
    public Task<CategoryResponse> UpdateCategory(
        int id,
        CategoryWriteRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.UpdateCategoryAsync(id, request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
