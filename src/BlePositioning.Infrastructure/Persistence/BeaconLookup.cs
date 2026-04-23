using BlePositioning.Application.Positioning;
using Microsoft.EntityFrameworkCore;

namespace BlePositioning.Infrastructure.Persistence;

public sealed class BeaconLookup(IDbContextFactory<AppDbContext> dbFactory) : IBeaconLookup
{
    public async Task<IReadOnlyList<BeaconPlacement>> ResolveAsync(
        IReadOnlyList<(string Uuid, int Major, int Minor)> keys,
        CancellationToken ct = default)
    {
        if (keys.Count == 0)
            return [];

        var distinct = keys.Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await (
            from b in db.Beacons.AsNoTracking()
            join f in db.Floors.AsNoTracking() on b.FloorId equals f.Id
            select new { b, f }).ToListAsync(ct);

        var set = distinct.ToHashSet();
        return rows
            .Where(x => set.Contains((x.b.Uuid, x.b.Major, x.b.Minor)))
            .Select(x => new BeaconPlacement(
                x.b.Uuid,
                x.b.Major,
                x.b.Minor,
                x.b.X,
                x.b.Y,
                x.b.TxPower,
                x.b.FloorId,
                x.f.WidthMeters,
                x.f.HeightMeters))
            .ToList();
    }
}
