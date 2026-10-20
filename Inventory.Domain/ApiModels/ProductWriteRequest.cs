namespace Inventory.Domain.ApiModels;

public sealed record ProductWriteRequest(string Name, int ProductCategoryId);
