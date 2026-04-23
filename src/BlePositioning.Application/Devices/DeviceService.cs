using System.Security.Cryptography;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Positioning;
using BlePositioning.Domain;

namespace BlePositioning.Application.Devices;

public sealed class DeviceService(
    ITrackedDeviceRepository repository,
    IApiKeyHasher apiKeyHasher,
    IPositionCache positionCache,
    ITrajectoryQuery trajectoryQuery) : IDeviceService
{
    public const int TrajectoryMaxPoints = 10_000;

    public async Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await repository.ListAsync(ct);
        if (rows.Count == 0)
            return Array.Empty<DeviceSummaryDto>();

        var online = await positionCache.GetPositionKeyExistsAsync(
            rows.Select(d => d.Id).ToList(),
            ct);
        return rows
            .Select(d => new DeviceSummaryDto(
                d.Id,
                d.DeviceCode,
                d.DisplayName,
                d.Type,
                online.GetValueOrDefault(d.Id, false)))
            .ToList();
    }

    public async Task<Result<CreateTrackedDeviceResult>> CreateWithApiKeyAsync(
        CreateTrackedDeviceRequest request,
        CancellationToken ct = default)
    {
        var plaintext = GenerateApiKey();
        var hash = apiKeyHasher.Hash(plaintext);
        var device = TrackedDevice.Create(request.DeviceCode, request.DisplayName, request.Type, hash);
        await repository.AddAsync(device, ct);
        await repository.SaveChangesAsync(ct);
        return Result<CreateTrackedDeviceResult>.Ok(new CreateTrackedDeviceResult(device, plaintext));
    }

    public async Task<Result<DevicePositionDto>> GetLatestPositionAsync(Guid deviceId, CancellationToken ct = default)
    {
        var device = await repository.GetByIdAsync(deviceId, ct);
        if (device is null || device.IsDeleted)
            return Result<DevicePositionDto>.Fail("Device not found.");

        var cached = await positionCache.GetAsync(deviceId, ct);
        if (cached is null)
            return Result<DevicePositionDto>.Fail("Device has no known position.");

        var isOnline = await positionCache.HasPositionKeyAsync(deviceId, ct);
        return Result<DevicePositionDto>.Ok(new DevicePositionDto(
            deviceId,
            cached.FloorId,
            cached.X,
            cached.Y,
            cached.Accuracy,
            cached.Timestamp,
            IsOnline: isOnline));
    }

    public async Task<Result<DeviceTrajectoryDto>> GetTrajectoryAsync(
        Guid deviceId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Guid? floorId,
        int intervalSeconds,
        CancellationToken ct = default)
    {
        if (intervalSeconds is < 1 or > 60)
            return Result<DeviceTrajectoryDto>.Fail("intervalSeconds must be between 1 and 60.");

        var start = NormalizeToUtc(startTimeUtc);
        var end = NormalizeToUtc(endTimeUtc);
        if (start >= end)
            return Result<DeviceTrajectoryDto>.Fail("startTime must be before endTime.");

        var spanSeconds = (end - start).TotalSeconds;
        var bucketCount = (long)Math.Ceiling(spanSeconds / intervalSeconds);
        if (bucketCount > TrajectoryMaxPoints)
            return Result<DeviceTrajectoryDto>.Fail(
                "Time range and interval would exceed the maximum of 10,000 points. Narrow the range or increase intervalSeconds.");

        var device = await repository.GetByIdAsync(deviceId, ct);
        if (device is null || device.IsDeleted)
            return Result<DeviceTrajectoryDto>.Fail("Device not found.");

        var points = await trajectoryQuery.GetPointsAsync(
            deviceId,
            start,
            end,
            floorId,
            intervalSeconds,
            TrajectoryMaxPoints + 1,
            ct);

        if (points.Count > TrajectoryMaxPoints)
            return Result<DeviceTrajectoryDto>.Fail(
                "Too many trajectory points for this query. Narrow the time range or increase intervalSeconds.");

        return Result<DeviceTrajectoryDto>.Ok(new DeviceTrajectoryDto(deviceId, points.Count, points));
    }

    private static DateTime NormalizeToUtc(DateTime t) =>
        t.Kind switch
        {
            DateTimeKind.Utc => t,
            DateTimeKind.Local => t.ToUniversalTime(),
            _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
        };

    private static string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
