using BlePositioning.Domain;

namespace BlePositioning.Application.Devices;

public interface ITrackedDeviceRepository
{
    Task<IReadOnlyList<TrackedDevice>> ListAsync(CancellationToken ct = default);

    Task<TrackedDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TrackedDevice?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default);
    Task AddAsync(TrackedDevice device, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
