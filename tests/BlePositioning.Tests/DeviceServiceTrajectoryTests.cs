using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Positioning;
using BlePositioning.Domain;
using Moq;

namespace BlePositioning.Tests;

public sealed class DeviceServiceTrajectoryTests
{
    private readonly Mock<ITrackedDeviceRepository> _repo = new();
    private readonly Mock<IApiKeyHasher> _hasher = new();
    private readonly Mock<IPositionCache> _cache = new();
    private readonly Mock<ITrajectoryQuery> _trajectory = new();

    [Fact]
    public async Task GetTrajectoryAsync_invalid_interval_fails()
    {
        var sut = CreateSut();
        var r = await sut.GetTrajectoryAsync(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, 0);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task GetTrajectoryAsync_start_not_before_end_fails()
    {
        var sut = CreateSut();
        var t = DateTime.UtcNow;
        var r = await sut.GetTrajectoryAsync(Guid.NewGuid(), t, t, null, 1);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task GetTrajectoryAsync_bucket_cap_fails()
    {
        var sut = CreateSut();
        var start = DateTime.UtcNow;
        var end = start.AddSeconds(10_001);
        var r = await sut.GetTrajectoryAsync(Guid.NewGuid(), start, end, null, 1);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task GetTrajectoryAsync_device_missing_fails()
    {
        var sut = CreateSut();
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedDevice?)null);
        var r = await sut.GetTrajectoryAsync(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1), null, 1);
        Assert.False(r.IsSuccess);
        Assert.Equal("Device not found.", r.Error);
    }

    [Fact]
    public async Task GetTrajectoryAsync_too_many_points_from_db_fails()
    {
        var device = TrackedDevice.Create("a", "b", DeviceType.Person, "deadbeef");
        _repo.Setup(x => x.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var many = Enumerable.Range(0, DeviceService.TrajectoryMaxPoints + 1)
            .Select(i => new TrajectoryPointDto(i, i, Guid.NewGuid(), DateTime.UtcNow))
            .ToList();
        _trajectory.Setup(x => x.GetPointsAsync(
                device.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                1,
                DeviceService.TrajectoryMaxPoints + 1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(many);

        var sut = CreateSut();
        var r = await sut.GetTrajectoryAsync(device.Id, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1), null, 1);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task GetTrajectoryAsync_success()
    {
        var device = TrackedDevice.Create("a", "b", DeviceType.Person, "deadbeef");
        _repo.Setup(x => x.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var pts = new TrajectoryPointDto[] { new(1, 2, Guid.NewGuid(), DateTime.UtcNow) };
        _trajectory.Setup(x => x.GetPointsAsync(
                device.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                5,
                DeviceService.TrajectoryMaxPoints + 1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pts);

        var sut = CreateSut();
        var start = DateTime.UtcNow;
        var r = await sut.GetTrajectoryAsync(device.Id, start, start.AddMinutes(10), null, 5);
        Assert.True(r.IsSuccess);
        Assert.Single(r.Value!.Points);
        Assert.Equal(1, r.Value.TotalPoints);
    }

    private DeviceService CreateSut() =>
        new(_repo.Object, _hasher.Object, _cache.Object, _trajectory.Object);
}
