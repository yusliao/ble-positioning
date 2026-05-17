using BlePositioning.API.Options;
using Microsoft.Extensions.Options;

namespace BlePositioning.API.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{
    public async Task Invoke(HttpContext context)
    {
        var opts = options.Value;
        if (opts.Enabled)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";

                if (opts.UseHsts && context.Request.IsHttps)
                {
                    headers["Strict-Transport-Security"] =
                        $"max-age={opts.HstsMaxAgeSeconds}; includeSubDomains";
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}
