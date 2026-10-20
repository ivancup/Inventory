using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record CategoryWriteRequest(
    [property: Required, StringLength(120, MinimumLength = 1)] string Name);
