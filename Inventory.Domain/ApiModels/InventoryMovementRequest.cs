using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record InventoryMovementRequest(
    [property: Range(1, int.MaxValue)] int ProductId,
    [property: Range(1, int.MaxValue)] int Quantity);
