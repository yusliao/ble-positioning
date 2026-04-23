using System.Diagnostics;

namespace BlePositioning.API.Extensions;

public static class HttpContextExtensions
{
    public static string GetTraceId(this HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
