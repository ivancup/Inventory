namespace Inventory.Domain.ApiModels;

public sealed record ProductResponse(
    int Id,
    string Name,
    int ProductCategoryId,
    string CategoryName,
    int Quantity,
    bool IsDeleted);
