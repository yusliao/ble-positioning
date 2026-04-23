using BlePositioning.API.Extensions;

namespace BlePositioning.API.Middleware;

public sealed class XTraceIdResponseHeaderMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        await next(context);

        if (!context.Response.HasStarted && !context.Response.Headers.ContainsKey("X-Trace-Id"))
            context.Response.Headers.Append("X-Trace-Id", context.GetTraceId());
    }
}
