namespace Inventory.Repository.Entities;

public sealed class AuditLogEntity
{
    public long Id { get; set; }
    public required string ChangeType { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
