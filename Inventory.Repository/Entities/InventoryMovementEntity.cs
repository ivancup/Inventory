namespace Inventory.Repository.Entities;

public sealed class InventoryMovementEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public required string Type { get; set; }
    public int Quantity { get; set; }
    public int UserId { get; set; }
    public UserEntity? User { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
