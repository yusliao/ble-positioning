using BlePositioning.Application.Common.Models;
using BlePositioning.Domain;

namespace BlePositioning.Application.Floors;

public sealed class FloorService(IFloorRepository repository, IFloorMapStorage mapStorage) : IFloorService
{
    public async Task<Result<FloorDto>> CreateAsync(CreateFloorRequest request, CancellationToken ct = default)
    {
        var floor = Floor.Create(request.Name, request.BuildingCode, request.WidthMeters, request.HeightMeters);
        await repository.AddAsync(floor, ct);
        await repository.SaveChangesAsync(ct);
        return Result<FloorDto>.Ok(ToDto(floor));
    }

    public async Task<IReadOnlyList<FloorDto>> ListAsync(CancellationToken ct = default)
    {
        var floors = await repository.ListAsync(ct);
        return floors.Select(ToDto).ToList();
    }

    public async Task<Result<FloorDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var floor = await repository.GetByIdAsync(id, ct);
        return floor is null ? Result<FloorDto>.Fail("Floor not found.") : Result<FloorDto>.Ok(ToDto(floor));
    }

    public async Task<Result<IReadOnlyList<BeaconListItemDto>>> ListBeaconsByFloorAsync(
        Guid floorId,
        CancellationToken ct = default)
    {
        if (!await repository.ExistsActiveAsync(floorId, ct))
            return Result<IReadOnlyList<BeaconListItemDto>>.Fail("Floor not found.");
        var rows = await repository.ListActiveBeaconsByFloorAsync(floorId, ct);
        IReadOnlyList<BeaconListItemDto> list = rows.Select(b => new BeaconListItemDto(
            b.Id, b.Uuid, b.Major, b.Minor, b.X, b.Y, b.TxPower, (int)b.Status)).ToList();
        return Result<IReadOnlyList<BeaconListItemDto>>.Ok(list);
    }

    public async Task<Result<FloorDto>> UpdateAsync(Guid id, UpdateFloorRequest request, CancellationToken ct = default)
    {
        var floor = await repository.GetByIdAsync(id, ct);
        if (floor is null)
            return Result<FloorDto>.Fail("Floor not found.");

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BuildingCode))
            return Result<FloorDto>.Fail("Name and building code are required.");

        floor.Update(request.Name.Trim(), request.BuildingCode.Trim(), request.WidthMeters, request.HeightMeters);
        floor.SetMapImageUrl(string.IsNullOrWhiteSpace(request.MapImageUrl) ? null : request.MapImageUrl.Trim());
        await repository.SaveChangesAsync(ct);
        return Result<FloorDto>.Ok(ToDto(floor));
    }

    public async Task<Result<FloorDto>> UploadMapImageAsync(
        Guid id,
        Stream content,
        string contentType,
        string? originalFileName,
        CancellationToken ct = default)
    {
        var floor = await repository.GetByIdAsync(id, ct);
        if (floor is null)
            return Result<FloorDto>.Fail("Floor not found.");

        var saved = await mapStorage.SaveAsync(id, content, contentType, originalFileName, ct);
        if (!saved.IsSuccess)
            return Result<FloorDto>.Fail(saved.Error ?? "Map upload failed.");

        floor.SetMapImageUrl(saved.Value!);
        await repository.SaveChangesAsync(ct);
        return Result<FloorDto>.Ok(ToDto(floor));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var floor = await repository.GetByIdAsync(id, ct);
        if (floor is null)
            return Result<bool>.Fail("Floor not found.");

        foreach (var beacon in floor.Beacons.ToList())
            beacon.SoftDelete();

        floor.SoftDelete();
        await repository.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<BeaconListItemDto>> CreateBeaconAsync(
        Guid floorId,
        CreateBeaconRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Uuid))
            return Result<BeaconListItemDto>.Fail("Beacon UUID is required.");

        var uuid = request.Uuid.Trim();
        if (uuid.Length > 36)
            return Result<BeaconListItemDto>.Fail("Beacon UUID exceeds maximum length.");

        if (!await repository.ExistsActiveAsync(floorId, ct))
            return Result<BeaconListItemDto>.Fail("Floor not found.");

        if (await repository.BeaconIdentityInUseAsync(uuid, request.Major, request.Minor, ct))
            return Result<BeaconListItemDto>.Fail(FloorServiceErrors.DuplicateBeaconIdentity);

        var beacon = Beacon.Create(floorId, uuid, request.Major, request.Minor, request.X, request.Y, request.TxPower);
        await repository.AddBeaconAsync(beacon, ct);
        await repository.SaveChangesAsync(ct);
        return Result<BeaconListItemDto>.Ok(ToBeaconDto(beacon));
    }

    public async Task<Result<BeaconListItemDto>> UpdateBeaconAsync(
        Guid floorId,
        Guid beaconId,
        UpdateBeaconRequest request,
        CancellationToken ct = default)
    {
        var beacon = await repository.GetTrackedBeaconAsync(floorId, beaconId, ct);
        if (beacon is null)
            return Result<BeaconListItemDto>.Fail("Beacon not found.");

        beacon.Move(request.X, request.Y, request.TxPower);
        await repository.SaveChangesAsync(ct);
        return Result<BeaconListItemDto>.Ok(ToBeaconDto(beacon));
    }

    public async Task<Result<bool>> DeleteBeaconAsync(Guid floorId, Guid beaconId, CancellationToken ct = default)
    {
        var beacon = await repository.GetTrackedBeaconAsync(floorId, beaconId, ct);
        if (beacon is null)
            return Result<bool>.Fail("Beacon not found.");

        beacon.SoftDelete();
        await repository.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<IReadOnlyList<AlertRuleListItemDto>>> ListAlertRulesByFloorAsync(
        Guid floorId,
        CancellationToken ct = default)
    {
        if (!await repository.ExistsActiveAsync(floorId, ct))
            return Result<IReadOnlyList<AlertRuleListItemDto>>.Fail("Floor not found.");
        var rows = await repository.ListAlertRulesByFloorIdAsync(floorId, ct);
        IReadOnlyList<AlertRuleListItemDto> list = rows
            .Select(r => new AlertRuleListItemDto(
                r.Id, r.FloorId, r.Name, r.ZonePolygon, r.TriggerOn, r.IsEnabled))
            .ToList();
        return Result<IReadOnlyList<AlertRuleListItemDto>>.Ok(list);
    }

    public async Task<Result<AlertRuleListItemDto>> CreateAlertRuleAsync(
        Guid floorId,
        CreateAlertRuleRequest request,
        CancellationToken ct = default)
    {
        if (!await repository.ExistsActiveAsync(floorId, ct))
            return Result<AlertRuleListItemDto>.Fail("Floor not found.");

        var vName = ValidateRuleName(request.Name);
        if (vName is not null)
            return Result<AlertRuleListItemDto>.Fail(vName);
        var vTr = ValidateTriggerOn(request.TriggerOn);
        if (vTr is not null)
            return Result<AlertRuleListItemDto>.Fail(vTr);
        var vZone = ZonePolygonValidator.Validate(request.ZonePolygon);
        if (vZone is not null)
            return Result<AlertRuleListItemDto>.Fail(vZone);

        var name = request.Name.Trim();
        var zone = request.ZonePolygon.Trim();
        var rule = AlertRule.Create(floorId, name, zone, request.TriggerOn, request.IsEnabled);
        await repository.AddAlertRuleAsync(rule, ct);
        await repository.SaveChangesAsync(ct);
        return Result<AlertRuleListItemDto>.Ok(ToAlertRuleDto(rule));
    }

    public async Task<Result<AlertRuleListItemDto>> UpdateAlertRuleAsync(
        Guid floorId,
        Guid ruleId,
        UpdateAlertRuleRequest request,
        CancellationToken ct = default)
    {
        var rule = await repository.GetAlertRuleAsync(floorId, ruleId, ct);
        if (rule is null)
            return Result<AlertRuleListItemDto>.Fail("Alert rule not found.");

        var vName = ValidateRuleName(request.Name);
        if (vName is not null)
            return Result<AlertRuleListItemDto>.Fail(vName);
        var vTr = ValidateTriggerOn(request.TriggerOn);
        if (vTr is not null)
            return Result<AlertRuleListItemDto>.Fail(vTr);
        var vZone = ZonePolygonValidator.Validate(request.ZonePolygon);
        if (vZone is not null)
            return Result<AlertRuleListItemDto>.Fail(vZone);

        var name = request.Name.Trim();
        var zone = request.ZonePolygon.Trim();
        rule.Update(name, zone, request.TriggerOn, request.IsEnabled);
        await repository.SaveChangesAsync(ct);
        return Result<AlertRuleListItemDto>.Ok(ToAlertRuleDto(rule));
    }

    public async Task<Result<bool>> DeleteAlertRuleAsync(Guid floorId, Guid ruleId, CancellationToken ct = default)
    {
        var rule = await repository.GetAlertRuleAsync(floorId, ruleId, ct);
        if (rule is null)
            return Result<bool>.Fail("Alert rule not found.");

        repository.RemoveAlertRule(rule);
        await repository.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private static string? ValidateRuleName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";
        return name.Trim().Length > ZonePolygonValidator.NameMaxLength ? "Name exceeds maximum length." : null;
    }

    private static string? ValidateTriggerOn(short value)
    {
        if (!Enum.IsDefined(typeof(AlertTriggerKind), (AlertTriggerKind)value))
        {
            return "TriggerOn is invalid. Expected values: 0=Enter, 1=Exit, 2=EnterOrExit.";
        }

        return null;
    }

    private static AlertRuleListItemDto ToAlertRuleDto(AlertRule r) =>
        new(r.Id, r.FloorId, r.Name, r.ZonePolygon, r.TriggerOn, r.IsEnabled);

    private static BeaconListItemDto ToBeaconDto(Beacon b) =>
        new(b.Id, b.Uuid, b.Major, b.Minor, b.X, b.Y, b.TxPower, (int)b.Status);

    private static FloorDto ToDto(Floor f) =>
        new(f.Id, f.Name, f.BuildingCode, f.WidthMeters, f.HeightMeters, f.MapImageUrl);
}
