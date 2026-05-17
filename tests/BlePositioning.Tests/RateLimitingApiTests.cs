using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Floors;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace BlePositioning.Tests;

public sealed class GeneralRateLimitingApiTests : IClassFixture<RateLimitingWebAppFactory>
{
    private readonly HttpClient _client;

    public GeneralRateLimitingApiTests(RateLimitingWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Login_burst_exceeds_general_limit_returns_429()
    {
        HttpStatusCode? thirdStatus = null;
        for (var i = 0; i < 3; i++)
        {
            var res = await _client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { username = "admin", password = "ChangeMe!" });
            thirdStatus = res.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, thirdStatus);
    }
}

public sealed class RssiRateLimitingApiTests : IClassFixture<RateLimitingWebAppFactory>
{
    private readonly RateLimitingWebAppFactory _factory;
    private readonly HttpClient _client;

    public RssiRateLimitingApiTests(RateLimitingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Rssi_report_burst_exceeds_rssi_limit_returns_429()
    {
        var loginRes = await _client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<Envelope<LoginRow>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Data!.AccessToken);

        var devRes = await _client.PostAsJsonAsync(
            "api/v1/devices",
            new { deviceCode = $"rl-{Guid.NewGuid():N}", displayName = "RL", type = 1 });
        devRes.EnsureSuccessStatusCode();
        var dev = await devRes.Content.ReadFromJsonAsync<Envelope<DeviceCreateRow>>();

        var rssiClient = _factory.CreateClient();
        rssiClient.DefaultRequestHeaders.Add("X-Api-Key", dev!.Data!.ApiKey);

        var body = new
        {
            deviceId = dev.Data.DeviceId,
            signals = new[] { new { uuid = "11111111-1111-1111-1111-111111111101", major = 1, minor = 1, rssi = -65 } },
            timestamp = DateTime.UtcNow,
        };

        var first = await rssiClient.PostAsJsonAsync("api/v1/rssi/report", body);
        var second = await rssiClient.PostAsJsonAsync("api/v1/rssi/report", body);
        var third = await rssiClient.PostAsJsonAsync("api/v1/rssi/report", body);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
    }

    private sealed record Envelope<T>(bool Success, T? Data, string? Error, string? TraceId);
    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);
    private sealed record DeviceCreateRow(Guid DeviceId, string DeviceCode, string ApiKey);
}

/// <summary>启用限流：general 与 RSSI 配额均为 2，便于断言 429。</summary>
public sealed class RateLimitingWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:RssiPermitLimit", "2");
        builder.UseSetting("RateLimiting:RssiWindowSeconds", "60");
        builder.UseSetting("RateLimiting:GeneralPermitLimit", "2");
        builder.UseSetting("RateLimiting:GeneralWindowMinutes", "1");
        builder.ConfigureTestServices(ConfigureTestServices);
    }

    internal static void ConfigureTestServices(IServiceCollection services)
    {
        foreach (var d in services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                     d.ServiceType == typeof(AppDbContext) ||
                     d.ServiceType == typeof(IDbContextFactory<AppDbContext>))
                 .ToList())
        {
            services.Remove(d);
        }

        var dbName = Guid.NewGuid().ToString("N");
        services.AddDbContext<AppDbContext>(
            o => o.UseInMemoryDatabase(dbName),
            ServiceLifetime.Scoped,
            ServiceLifetime.Singleton);
        services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

        services.RemoveAll<ITrajectoryQuery>();
        services.AddSingleton<ITrajectoryQuery, EmptyTrajectoryQuery>();

        services.RemoveAll<IFloorMapStorage>();
        services.AddSingleton<IFloorMapStorage, TestFloorMapStorage>();

        services.RemoveAll<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        database
            .Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(database.Object);
        services.AddSingleton(multiplexer.Object);
    }
}
