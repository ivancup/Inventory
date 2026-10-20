using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record CategoryWriteRequest(
    [param: Required, StringLength(120, MinimumLength = 1)] string Name);
