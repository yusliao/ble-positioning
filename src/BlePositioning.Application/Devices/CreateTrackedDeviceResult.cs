using BlePositioning.Domain;

namespace BlePositioning.Application.Devices;

public sealed record CreateTrackedDeviceResult(TrackedDevice Device, string PlaintextApiKey);
