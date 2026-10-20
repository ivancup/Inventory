using Inventory.Api.Middleware;
using Inventory.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Inventory.UnitTest.Api;

[TestClass]
public sealed class ErrorHandlingMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_MapsKnownErrorsToProblemDetails()
    {
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        var middleware = new ErrorHandlingMiddleware(_ =>
            Task.FromException(new InventoryException(409, "insufficient_stock", "Insufficient stock.")));

        await middleware.InvokeAsync(context, NullLogger<ErrorHandlingMiddleware>.Instance);

        Assert.AreEqual(409, context.Response.StatusCode);
        responseBody.Position = 0;
        using var body = await JsonDocument.ParseAsync(responseBody);
        Assert.AreEqual("insufficient_stock", body.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task InvokeAsync_HidesUnexpectedExceptionDetails()
    {
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        var middleware = new ErrorHandlingMiddleware(_ =>
            Task.FromException(new InvalidOperationException("internal detail")));

        await middleware.InvokeAsync(context, NullLogger<ErrorHandlingMiddleware>.Instance);

        Assert.AreEqual(500, context.Response.StatusCode);
        responseBody.Position = 0;
        using var body = await JsonDocument.ParseAsync(responseBody);
        Assert.AreEqual("An unexpected error occurred.", body.RootElement.GetProperty("detail").GetString());
        Assert.IsFalse(body.RootElement.GetRawText().Contains("internal detail", StringComparison.Ordinal));
    }
}
