using BlePositioning.Application.Floors;
using BlePositioning.Application.Geofence;
using BlePositioning.Domain;
using BlePositioning.Infrastructure.Geofence;
using BlePositioning.Infrastructure.Persistence;
using BlePositioning.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlePositioning.Tests;

public sealed class GeofenceEvaluationServiceTests
{
    private const string Square10 =
        """{"type":"Polygon","coordinates":[[[0,0],[10,0],[10,10],[0,10],[0,0]]]}""";

    [Fact]
    public async Task Three_positions_crossing_boundary_produce_enter_and_exit_events()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"gf-eval-{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options);
        var floor = Floor.Create("F1", "B1", 30, 30);
        await db.Floors.AddAsync(floor);
        var device = TrackedDevice.Create("dev-gf-1", "D", DeviceType.Person, new string('a', 64));
        await db.TrackedDevices.AddAsync(device);
        var rule = AlertRule.Create(floor.Id, "z1", Square10, (short)AlertTriggerKind.EnterOrExit, true);
        await db.AlertRules.AddAsync(rule);
        await db.SaveChangesAsync();

        var floorRepo = new FloorRepository(db);
        var state = new MemoryGeofenceStateStore();
        var pub = new NoOpGeofenceEventPublisher();
        var svc = new GeofenceEvaluationService(
            floorRepo,
            db,
            state,
            pub,
            NullLogger<GeofenceEvaluationService>.Instance);
        var t0 = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc);

        await svc.EvaluateAsync(device.Id, floor.Id, 20, 20, 1, t0, default);
        await svc.EvaluateAsync(device.Id, floor.Id, 5, 5, 1, t0.AddSeconds(1), default);
        await svc.EvaluateAsync(device.Id, floor.Id, 20, 20, 1, t0.AddSeconds(2), default);

        Assert.Equal(2, await db.GeofenceEvents.CountAsync());
        var kinds = await db.GeofenceEvents.Select(e => e.EventKind).OrderBy(k => k).ToListAsync();
        Assert.Equal(GeofenceEvent.KindEnter, kinds[0]);
        Assert.Equal(GeofenceEvent.KindExit, kinds[1]);
    }

    private sealed class MemoryGeofenceStateStore : IGeofenceStateStore
    {
        private readonly Dictionary<string, bool> _map = new();

        private static string K(Guid a, Guid b) => $"{a:N}:{b:N}";

        public Task<bool> GetWasInsideAsync(Guid deviceId, Guid ruleId, CancellationToken ct = default) =>
            Task.FromResult(_map.TryGetValue(K(deviceId, ruleId), out var v) && v);

        public Task SetWasInsideAsync(Guid deviceId, Guid ruleId, bool wasInside, CancellationToken ct = default)
        {
            _map[K(deviceId, ruleId)] = wasInside;
            return Task.CompletedTask;
        }
    }
}
