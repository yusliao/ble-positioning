using BlePositioning.Domain;

namespace BlePositioning.Application.Devices;

public record CreateTrackedDeviceRequest(string DeviceCode, string DisplayName, DeviceType Type);
