using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record ProductWriteRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Name,
    [param: Range(1, int.MaxValue)] int ProductCategoryId);
