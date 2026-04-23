using BlePositioning.Domain;

namespace BlePositioning.Application.Devices;

public record DeviceSummaryDto(Guid Id, string DeviceCode, string DisplayName, DeviceType Type, bool IsOnline);
