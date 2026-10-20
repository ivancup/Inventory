using System.Diagnostics.Metrics;

namespace Inventory.Service.Observability;

public sealed class InventoryMetrics
{
    private static readonly Meter Meter = new("Inventory.Management", "1.0.0");
    private readonly Counter<long> added = Meter.CreateCounter<long>("inventory.stock.added", unit: "{item}");
    private readonly Counter<long> removed = Meter.CreateCounter<long>("inventory.stock.removed", unit: "{item}");

    public void RecordAdded(int quantity, int productId, int categoryId) =>
        added.Add(quantity, new KeyValuePair<string, object?>("product.id", productId),
            new KeyValuePair<string, object?>("category.id", categoryId));

    public void RecordRemoved(int quantity, int productId, int categoryId) =>
        removed.Add(quantity, new KeyValuePair<string, object?>("product.id", productId),
            new KeyValuePair<string, object?>("category.id", categoryId));
}
