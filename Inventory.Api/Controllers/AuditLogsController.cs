using Inventory.Domain.ApiModels;
using Inventory.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>Exposes authenticated, paginated audit-log queries.</summary>
[ApiController]
[Authorize(Policy = "InventoryWrite")]
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController(IInventoryService inventoryService) : ControllerBase
{
    /// <summary>Returns a page of audit records.</summary>
    [HttpGet]
    public Task<PagedResponse<AuditLogResponse>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        inventoryService.GetAuditLogsAsync(page, pageSize, cancellationToken);
}
