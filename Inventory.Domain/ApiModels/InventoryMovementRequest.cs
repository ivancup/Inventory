using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.ApiModels;

public sealed record InventoryMovementRequest(
    [param: Range(1, int.MaxValue)] int ProductId,
    [param: Range(1, int.MaxValue)] int Quantity);
