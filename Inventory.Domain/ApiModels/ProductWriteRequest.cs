using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record ProductWriteRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(1, int.MaxValue)] int ProductCategoryId);
