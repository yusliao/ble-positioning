namespace BlePositioning.Application.Positioning;

public interface IBeaconLookup
{
    Task<IReadOnlyList<BeaconPlacement>> ResolveAsync(IReadOnlyList<(string Uuid, int Major, int Minor)> keys, CancellationToken ct = default);
}
