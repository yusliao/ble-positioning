using BlePositioning.API.Extensions;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;
using BlePositioning.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlePositioning.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/floors")]
public sealed class FloorsController(IFloorService floorService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FloorDto>>>> List(CancellationToken ct)
    {
        var list = await floorService.ListAsync(ct);
        return Ok(new ApiResponse<IReadOnlyList<FloorDto>>(true, list, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FloorDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await floorService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return Ok(new ApiResponse<FloorDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{floorId:guid}/beacons")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BeaconListItemDto>>>> ListBeacons(Guid floorId, CancellationToken ct)
    {
        var result = await floorService.ListBeaconsByFloorAsync(floorId, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return Ok(new ApiResponse<IReadOnlyList<BeaconListItemDto>>(
            true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpGet("{floorId:guid}/alert-rules")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AlertRuleListItemDto>>>> ListAlertRules(
        Guid floorId,
        CancellationToken ct)
    {
        var result = await floorService.ListAlertRulesByFloorAsync(floorId, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return Ok(new ApiResponse<IReadOnlyList<AlertRuleListItemDto>>(
            true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpPost("{floorId:guid}/alert-rules")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<AlertRuleListItemDto>>> CreateAlertRule(
        Guid floorId,
        [FromBody] CreateAlertRuleRequest request,
        CancellationToken ct)
    {
        var result = await floorService.CreateAlertRuleAsync(floorId, request, ct);
        if (!result.IsSuccess)
        {
            var status = result.Error == "Floor not found."
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return CreatedAtAction(
            nameof(ListAlertRules),
            new { floorId },
            new ApiResponse<AlertRuleListItemDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpPut("{floorId:guid}/alert-rules/{ruleId:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<AlertRuleListItemDto>>> UpdateAlertRule(
        Guid floorId,
        Guid ruleId,
        [FromBody] UpdateAlertRuleRequest request,
        CancellationToken ct)
    {
        var result = await floorService.UpdateAlertRuleAsync(floorId, ruleId, request, ct);
        if (!result.IsSuccess)
        {
            var status = result.Error == "Alert rule not found."
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(new ApiResponse<AlertRuleListItemDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpDelete("{floorId:guid}/alert-rules/{ruleId:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<IActionResult> DeleteAlertRule(Guid floorId, Guid ruleId, CancellationToken ct)
    {
        var result = await floorService.DeleteAlertRuleAsync(floorId, ruleId, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> Create([FromBody] CreateFloorRequest request, CancellationToken ct)
    {
        var result = await floorService.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: result.Error);
        return Ok(new ApiResponse<FloorDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpPost("{floorId:guid}/map-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> UploadMapImage(
        Guid floorId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Image file is required.");

        await using var stream = file.OpenReadStream();
        var result = await floorService.UploadMapImageAsync(floorId, stream, file.ContentType, file.FileName, ct);
        if (!result.IsSuccess)
        {
            var status = result.Error == "Floor not found."
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(new ApiResponse<FloorDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<FloorDto>>> Update(Guid id, [FromBody] UpdateFloorRequest request, CancellationToken ct)
    {
        var result = await floorService.UpdateAsync(id, request, ct);
        if (!result.IsSuccess)
        {
            var status = result.Error == "Floor not found."
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, detail: result.Error);
        }

        return Ok(new ApiResponse<FloorDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await floorService.DeleteAsync(id, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return NoContent();
    }

    [HttpPost("{floorId:guid}/beacons")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<BeaconListItemDto>>> CreateBeacon(
        Guid floorId,
        [FromBody] CreateBeaconRequest request,
        CancellationToken ct)
    {
        var result = await floorService.CreateBeaconAsync(floorId, request, ct);
        if (!result.IsSuccess)
        {
            var status = result.Error switch
            {
                "Floor not found." => StatusCodes.Status404NotFound,
                FloorServiceErrors.DuplicateBeaconIdentity => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, detail: result.Error);
        }

        return CreatedAtAction(
            nameof(ListBeacons),
            new { floorId },
            new ApiResponse<BeaconListItemDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpPut("{floorId:guid}/beacons/{beaconId:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<ActionResult<ApiResponse<BeaconListItemDto>>> UpdateBeacon(
        Guid floorId,
        Guid beaconId,
        [FromBody] UpdateBeaconRequest request,
        CancellationToken ct)
    {
        var result = await floorService.UpdateBeaconAsync(floorId, beaconId, request, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return Ok(new ApiResponse<BeaconListItemDto>(true, result.Value, null, HttpContext.GetTraceId()));
    }

    [HttpDelete("{floorId:guid}/beacons/{beaconId:guid}")]
    [Authorize(Roles = BlePositioningRoles.Admin)]
    public async Task<IActionResult> DeleteBeacon(Guid floorId, Guid beaconId, CancellationToken ct)
    {
        var result = await floorService.DeleteBeaconAsync(floorId, beaconId, ct);
        if (!result.IsSuccess)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error);
        return NoContent();
    }
}
