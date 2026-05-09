using Microsoft.AspNetCore.Mvc;
using MeteringService.Models.DTOs.Requests;
using MeteringService.Models.DTOs.Responses;
using MeteringService.Services.Interfaces;

namespace MeteringService.Controllers;

[ApiController]
[Route("api/metering")]
public class MeteringController : ControllerBase
{
    private readonly IMeteringService _metering;
    public MeteringController(IMeteringService metering) => _metering = metering;

    /// <summary>Check quota and increment if allowed (atomic sliding window)</summary>
    [HttpPost("check")]
    public async Task<IActionResult> Check([FromBody] CheckQuotaRequest request)
    {
        var (allowed, used, remaining) = await _metering
            .CheckAndIncrementAsync(request.TenantId, request.MaxExecutionsPer24h);

        var response = new QuotaCheckResponse(
            allowed, request.TenantId, used,
            request.MaxExecutionsPer24h, remaining);

        return allowed ? Ok(response) : StatusCode(429, response);
    }

    /// <summary>Get current 24h usage for a tenant</summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetStatus(string tenantId,
        [FromQuery] int max = 100)
    {
        var used      = await _metering.GetCurrentUsageAsync(tenantId);
        var remaining = Math.Max(0, max - used);

        return Ok(new QuotaStatusResponse(tenantId, used, max, remaining));
    }

    /// <summary>Decrement quota (e.g. job cancelled before start)</summary>
    [HttpPost("{tenantId}/decrement")]
    public async Task<IActionResult> Decrement(string tenantId)
    {
        await _metering.DecrementAsync(tenantId);
        return Ok(new { message = "Quota decremented" });
    }

    /// <summary>Reset quota for a tenant (admin use)</summary>
    [HttpDelete("{tenantId}")]
    public async Task<IActionResult> Reset(string tenantId)
    {
        await _metering.ResetAsync(tenantId);
        return Ok(new { message = $"Quota reset for tenant '{tenantId}'" });
    }
}
