namespace Inventory.Domain.Exceptions;

public sealed class InventoryException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}
