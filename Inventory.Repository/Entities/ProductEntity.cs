namespace Inventory.Repository.Entities;

public sealed class ProductEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int ProductCategoryId { get; set; }
    public ProductCategoryEntity? ProductCategory { get; set; }
    public int Quantity { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<InventoryMovementEntity> Movements { get; } = new List<InventoryMovementEntity>();
}
