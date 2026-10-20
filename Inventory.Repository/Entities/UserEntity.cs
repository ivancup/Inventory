namespace Inventory.Repository.Entities;

public sealed class UserEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public required string OAuthId { get; set; }
    public ICollection<InventoryMovementEntity> Movements { get; } = new List<InventoryMovementEntity>();
}
