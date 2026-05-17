using BlePositioning.API.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Geofence;
using BlePositioning.Application.Security;
using BlePositioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlePositioning.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/devices")]
[EnableRateLimiting(RateLimitingServiceExtensions.GeneralPolicyName)]
public sealed class DevicesController(
    IDeviceService deviceService,
    IGeofenceEventQueryService geofenceEventQuery,
    IDevicePresenceEventQueryService devicePresenceEventQuery) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DeviceSummaryDto>>>> List(CancellationToken ct)
    {
        var list = await deviceService.ListAsync(ct);
        return Ok(new ApiResponse<IReadOnlyList<DeviceSummaryDto>>(true, list, null, HttpContext.GetTraceId()));
    }

    public sealed record CreateDeviceApiRequest(string DeviceCode, string DisplayName, DeviceType Type);

    public sealed record CreateDeviceApiResponse(Guid DeviceId, string DeviceCode, string ApiKey);

    [HttpPost]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<CreateDeviceApiResponse>>> Create(
        [FromBody] CreateDeviceApiRequest request,
        CancellationToken ct)
    {
        var result = await deviceService.CreateWithApiKeyAsync(
            new CreateTrackedDeviceRequest(request.DeviceCode, request.DisplayName, request.Type),
            ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: result.Error);

        var r = result.Value!;
        var data = new CreateDeviceApiResponse(r.Device.Id, r.Device.DeviceCode, r.PlaintextApiKey);
        return Ok(new ApiResponse<CreateDeviceApiResponse>(true, data, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{deviceId:guid}/position")]
    public async Task<ActionResult<ApiResponse<DevicePositionDto>>> GetPosition(Guid deviceId, CancellationToken ct)
    {
        var result = await deviceService.GetLatestPositionAsync(deviceId, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return Ok(new ApiResponse<DevicePositionDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{deviceId:guid}/trajectory")]
    public async Task<ActionResult<ApiResponse<DeviceTrajectoryDto>>> GetTrajectory(
        Guid deviceId,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] Guid? floorId,
        [FromQuery] int intervalSeconds = 1,
        CancellationToken ct = default)
    {
        var result = await deviceService.GetTrajectoryAsync(deviceId, startTime, endTime, floorId, intervalSeconds, ct);
        if (!result.IsSuccess)
        {
            var status = string.Equals(result.Error, "Device not found.", StringComparison.Ordinal)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(new ApiResponse<DeviceTrajectoryDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{deviceId:guid}/geofence-events")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<GeofenceEventDto>>>> GetGeofenceEvents(
        Guid deviceId,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        CancellationToken ct)
    {
        var result = await geofenceEventQuery.ListByDeviceAsync(deviceId, startTime, endTime, ct);
        if (!result.IsSuccess)
        {
            var status = string.Equals(result.Error, "Device not found.", StringComparison.Ordinal)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(
            new ApiResponse<IReadOnlyList<GeofenceEventDto>>(
                true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{deviceId:guid}/presence-events")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DevicePresenceEventDto>>>> GetPresenceEvents(
        Guid deviceId,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        CancellationToken ct)
    {
        var result = await devicePresenceEventQuery.ListByDeviceAsync(deviceId, startTime, endTime, ct);
        if (!result.IsSuccess)
        {
            var status = string.Equals(result.Error, "Device not found.", StringComparison.Ordinal)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(
            new ApiResponse<IReadOnlyList<DevicePresenceEventDto>>(
                true, result.Value, null, HttpContext.GetTraceId()));
    }
}
