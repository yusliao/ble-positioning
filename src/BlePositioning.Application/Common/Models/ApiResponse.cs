namespace BlePositioning.Application.Common.Models;

public record ApiResponse<T>(bool Success, T? Data, string? Error, string TraceId);
