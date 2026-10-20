namespace Inventory.Repository.Entities;

public sealed class ProductCategoryEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<ProductEntity> Products { get; } = new List<ProductEntity>();
}
