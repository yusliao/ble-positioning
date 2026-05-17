using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using BlePositioning.API.Options;
using BlePositioning.API.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BlePositioning.API.Extensions;

public static class RateLimitingServiceExtensions
{
    public const string RssiPolicyName = "rssi-report";
    public const string GeneralPolicyName = "general";

    public static IServiceCollection AddBlePositioningRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        if (!configuration.GetValue($"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.Enabled)}", true))
            return services;

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json; charset=utf-8";
                var traceId = context.HttpContext.TraceIdentifier;
                var body = JsonSerializer.Serialize(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded.",
                    traceId,
                });
                await context.HttpContext.Response.WriteAsync(body, token);
            };

            limiter.AddPolicy(RssiPolicyName, httpContext =>
            {
                var opts = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
                var deviceId = httpContext.User.FindFirst(ApiKeyAuthenticationHandler.DeviceIdClaimType)?.Value
                               ?? httpContext.Connection.RemoteIpAddress?.ToString()
                               ?? "anonymous";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    deviceId,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = opts.RssiPermitLimit,
                        Window = TimeSpan.FromSeconds(Math.Max(1, opts.RssiWindowSeconds)),
                        SegmentsPerWindow = 2,
                        QueueLimit = 0,
                    });
            });

            limiter.AddPolicy(GeneralPolicyName, httpContext =>
            {
                var opts = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
                var partitionKey = ResolveGeneralPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = opts.GeneralPermitLimit,
                        Window = TimeSpan.FromMinutes(Math.Max(1, opts.GeneralWindowMinutes)),
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }

    private static string ResolveGeneralPartitionKey(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var name = httpContext.User.Identity.Name;
            if (!string.IsNullOrEmpty(name))
                return $"user:{name}";

            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub))
                return $"sub:{sub}";
        }

        return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
