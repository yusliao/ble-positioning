using BlePositioning.Application.Devices;
using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;

namespace BlePositioning.Infrastructure.Persistence.Repositories;

public sealed class TrackedDeviceRepository(AppDbContext db) : ITrackedDeviceRepository
{
    public async Task<IReadOnlyList<TrackedDevice>> ListAsync(CancellationToken ct = default) =>
        await db.TrackedDevices.AsNoTracking()
            .OrderBy(d => d.DeviceCode)
            .ToListAsync(ct);

    public Task<TrackedDevice?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TrackedDevices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<TrackedDevice?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default) =>
        db.TrackedDevices.FirstOrDefaultAsync(d => d.ApiKeyHash == apiKeyHash, ct);

    public async Task AddAsync(TrackedDevice device, CancellationToken ct = default) =>
        await db.TrackedDevices.AddAsync(device, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
