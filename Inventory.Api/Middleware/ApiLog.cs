namespace Inventory.Api.Middleware;

internal static partial class ApiLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Request failed with {ErrorCode}: {Message}")]
    public static partial void RequestRejected(ILogger logger, string errorCode, string message);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Unhandled request exception")]
    public static partial void UnhandledException(ILogger logger, Exception exception);
}
