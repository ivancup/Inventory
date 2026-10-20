namespace Inventory.Domain.ApiModels;

public sealed record AuditLogResponse(
    long Id,
    string ChangeType,
    string EntityName,
    string EntityId,
    string? OldValueJson,
    string? NewValueJson,
    string? ActorUserId,
    DateTime CreatedAtUtc);
