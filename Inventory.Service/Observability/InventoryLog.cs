using Microsoft.Extensions.Logging;

namespace Inventory.Service.Observability;

internal static partial class InventoryLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Inventory {MovementType} of {Quantity} units for product {ProductId} in category {CategoryId}")]
    public static partial void InventoryMoved(ILogger logger, string movementType, int quantity, int productId, int categoryId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Product {ProductId} was soft-deleted by {ActorSubject}")]
    public static partial void ProductDeleted(ILogger logger, int productId, string actorSubject);
}
