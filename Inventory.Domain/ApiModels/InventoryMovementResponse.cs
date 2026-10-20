namespace Inventory.Domain.ApiModels;

public sealed record InventoryMovementResponse(
    int Id,
    int ProductId,
    string Type,
    int Quantity,
    int UserId,
    DateTime CreatedAtUtc);
