using System.Diagnostics;
using System.Text;
using BlePositioning.API.Extensions;
using BlePositioning.API.Hubs;
using BlePositioning.API.Middleware;
using BlePositioning.API.Options;
using BlePositioning.API.Positioning;
using BlePositioning.API.Security;
using BlePositioning.API.Storage;
using BlePositioning.Application.Floors;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Geofence;
using BlePositioning.Infrastructure.Extensions;
using BlePositioning.Infrastructure.Geofence;
using BlePositioning.Infrastructure.Options;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.Configure<DevAdminOptions>(builder.Configuration.GetSection(DevAdminOptions.SectionName));
builder.Services.Configure<FloorMapStorageOptions>(builder.Configuration.GetSection(FloorMapStorageOptions.SectionName));
builder.Services.Configure<SecurityHeadersOptions>(builder.Configuration.GetSection(SecurityHeadersOptions.SectionName));
builder.Services.AddBlePositioningRateLimiting(builder.Configuration);
builder.Services.AddSingleton<IFloorMapStorage, WebHostFloorMapStorage>();
builder.Services.AddSingleton<JwtTokenIssuer>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection.GetValue<string>(nameof(JwtOptions.SigningKey))
             ?? "CHANGE_ME_MINIMUM_32_CHARACTERS_LONG!!";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>(nameof(JwtOptions.Issuer)),
            ValidAudience = jwtSection.GetValue<string>(nameof(JwtOptions.Audience)),
            IssuerSigningKey = signingKey,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs", StringComparison.Ordinal)
                    && context.Request.Query.TryGetValue("access_token", out var token)
                    && !string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 6_000_000;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "BlePositioning API",
        Version = "v1",
        Description = "BLE 室内定位：健康检查、JWT / ApiKey、楼层/信标/围栏、设备与轨迹/进区/在线事件、RSSI 上报、SignalR。",
    });
});

builder.Services.AddCors(o => o.AddPolicy("SignalR", p =>
    p
        .SetIsOriginAllowed(origin =>
        {
            if (builder.Environment.IsDevelopment())
                return true;
            return Uri.TryCreate(origin, UriKind.Absolute, out var u) && (u.IsLoopback || u.Host is "localhost");
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddSignalR();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient(HttpGeofenceWebhookPublisher.HttpClientName);
builder.Services.AddSingleton<IPositioningNotificationService, SignalRPositioningNotificationService>();
builder.Services.AddSingleton<SignalRGeofenceEventPublisher>();
builder.Services.AddSingleton<HttpGeofenceWebhookPublisher>();
builder.Services.AddSingleton<IGeofenceEventPublisher>(sp => new CompositeGeofenceEventPublisher(
    sp.GetRequiredService<SignalRGeofenceEventPublisher>(),
    sp.GetRequiredService<HttpGeofenceWebhookPublisher>()));
builder.Services.AddSingleton<IDevicePresenceEventPublisher, SignalRDevicePresenceEventPublisher>();

var app = builder.Build();

app.UseCors("SignalR");
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
// 不依赖端点表匹配：在管道内直接处理 GET /，避免在部分配置下对 "/" 落 404
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var p = context.Request.Path;
        if (p == "/" || p == string.Empty)
        {
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var lines = new List<string> { "BlePositioning API" };
            if (env.IsDevelopment())
                lines.Add("  GET /swagger   — OpenAPI 调试");
            lines.Add("  GET /health     — liveness");
            lines.Add("  GET /health/ready — readiness (PostgreSQL + Redis)");
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(string.Join("\n", lines) + "\n", context.RequestAborted);
            return;
        }
    }
    await next();
});
app.UseMiddleware<XTraceIdResponseHeaderMiddleware>();
app.UseHttpMetrics();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

var rateLimitingEnabled = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value.Enabled;
if (rateLimitingEnabled)
    app.UseRateLimiter();

app.MapGet("/health", () => Results.Json(new { status = "Healthy" }))
    .DisableRateLimiting();

app.MapGet("/health/ready", async (AppDbContext db, IConnectionMultiplexer redis, HttpContext ctx) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        await redis.GetDatabase().PingAsync();
        return Results.Json(new { status = "Healthy" });
    }
    catch
    {
        var traceId = Activity.Current?.Id ?? ctx.TraceIdentifier;
        return Results.Json(
            new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                title = "Service Unavailable",
                status = 503,
                detail = "Readiness check failed.",
                traceId,
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).DisableRateLimiting();

app.MapControllers();

app.MapHub<PositioningHub>("/hubs/positioning").RequireAuthorization().DisableRateLimiting();
app.MapMetrics().DisableRateLimiting();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}

await app.RunAsync();
