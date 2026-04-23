using BlePositioning.Application.Floors;
using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;

namespace BlePositioning.Infrastructure.Persistence.Repositories;

public sealed class FloorRepository(AppDbContext db) : IFloorRepository
{
    public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct = default) =>
        db.Floors.AsNoTracking().AnyAsync(f => f.Id == id && !f.IsDeleted, ct);

    public async Task<IReadOnlyList<Beacon>> ListActiveBeaconsByFloorAsync(Guid floorId, CancellationToken ct = default) =>
        await db.Beacons.AsNoTracking()
            .Where(b => b.FloorId == floorId && !b.IsDeleted)
            .OrderBy(b => b.Uuid).ThenBy(b => b.Major).ThenBy(b => b.Minor)
            .ToListAsync(ct);

    public Task<Floor?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Floors.Include(f => f.Beacons).FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<Floor>> ListAsync(CancellationToken ct = default) =>
        await db.Floors.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);

    public async Task AddAsync(Floor floor, CancellationToken ct = default) =>
        await db.Floors.AddAsync(floor, ct);

    public async Task AddBeaconAsync(Beacon beacon, CancellationToken ct = default) =>
        await db.Beacons.AddAsync(beacon, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<bool> BeaconIdentityInUseAsync(string uuid, int major, int minor, CancellationToken ct = default) =>
        db.Beacons.AnyAsync(b => b.Uuid == uuid && b.Major == major && b.Minor == minor, ct);

    public Task<Beacon?> GetTrackedBeaconAsync(Guid floorId, Guid beaconId, CancellationToken ct = default) =>
        db.Beacons.FirstOrDefaultAsync(b => b.FloorId == floorId && b.Id == beaconId, ct);

    public async Task<IReadOnlyList<AlertRule>> ListAlertRulesByFloorIdAsync(Guid floorId, CancellationToken ct = default) =>
        await db.AlertRules.AsNoTracking()
            .Where(r => r.FloorId == floorId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public Task<AlertRule?> GetAlertRuleAsync(Guid floorId, Guid ruleId, CancellationToken ct = default) =>
        db.AlertRules.FirstOrDefaultAsync(r => r.FloorId == floorId && r.Id == ruleId, ct);

    public async Task AddAlertRuleAsync(AlertRule rule, CancellationToken ct = default) =>
        await db.AlertRules.AddAsync(rule, ct);

    public void RemoveAlertRule(AlertRule rule) => db.AlertRules.Remove(rule);
}
