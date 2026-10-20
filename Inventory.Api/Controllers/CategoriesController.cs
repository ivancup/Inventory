using Inventory.Api.Security;
using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<CategoryResponse>> GetCategories(CancellationToken cancellationToken) =>
        inventoryService.GetCategoriesAsync(cancellationToken);

    [HttpPut("{id:int}")]
    [Authorize]
    public Task<CategoryResponse> UpdateCategory(
        int id,
        CategoryWriteRequest request,
        CancellationToken cancellationToken) =>
        inventoryService.UpdateCategoryAsync(id, request, ActorIdentityFactory.FromPrincipal(User), cancellationToken);
}
