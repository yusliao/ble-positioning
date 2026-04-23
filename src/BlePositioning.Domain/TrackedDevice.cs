namespace BlePositioning.Domain;

public class TrackedDevice
{
    public Guid Id { get; private set; }
    public string DeviceCode { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public DeviceType Type { get; private set; }
    public string ApiKeyHash { get; private set; } = null!;
    public DateTime ApiKeyCreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TrackedDevice()
    {
    }

    public static TrackedDevice Create(string deviceCode, string displayName, DeviceType type, string apiKeyHash)
    {
        var now = DateTime.UtcNow;
        return new TrackedDevice
        {
            Id = Guid.NewGuid(),
            DeviceCode = deviceCode,
            DisplayName = displayName,
            Type = type,
            ApiKeyHash = apiKeyHash,
            ApiKeyCreatedAt = now,
            IsDeleted = false,
            CreatedAt = now,
        };
    }

    public void RotateApiKey(string newApiKeyHash)
    {
        ApiKeyHash = newApiKeyHash;
        ApiKeyCreatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
    }
}
