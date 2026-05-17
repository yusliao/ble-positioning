using BlePositioning.API.Extensions;
using BlePositioning.API.Security;
using BlePositioning.Application.Positioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlePositioning.API.Controllers;

[ApiController]
[Route("api/v1/rssi")]
[EnableRateLimiting(RateLimitingServiceExtensions.RssiPolicyName)]
public sealed class RssiController(IRssiReportQueue queue) : ControllerBase
{
    [HttpPost("report")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public IActionResult Report([FromBody] RssiReportDto body)
    {
        var deviceIdClaim = User.FindFirst(ApiKeyAuthenticationHandler.DeviceIdClaimType)?.Value;
        if (string.IsNullOrEmpty(deviceIdClaim) || !Guid.TryParse(deviceIdClaim, out var deviceFromKey))
            return Unauthorized();

        if (body.DeviceId != deviceFromKey)
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "deviceId does not match API key.");

        if (body.Signals.Count == 0)
        {
            ModelState.AddModelError(nameof(body.Signals), "At least one signal is required.");
            return ValidationProblem(ModelState);
        }

        var ts = body.Timestamp.Kind == DateTimeKind.Utc
            ? body.Timestamp
            : DateTime.SpecifyKind(body.Timestamp, DateTimeKind.Utc);

        var report = body with { Timestamp = ts };
        queue.TryEnqueue(report);

        Response.Headers.Append("X-Trace-Id", HttpContext.GetTraceId());
        return Accepted();
    }
}
