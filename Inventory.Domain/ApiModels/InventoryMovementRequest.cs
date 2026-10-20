namespace Inventory.Domain.ApiModels;

public sealed record InventoryMovementRequest(int ProductId, int Quantity);
