using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlePositioning.Tests.Integration;

[Collection("docker")]
[Trait("Category", "docker")]
public sealed class TestcontainersRssiPositioningTests
{
    private readonly DockerIntegrationFixture _fixture;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public TestcontainersRssiPositioningTests(DockerIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Rssi_report_then_position_in_Redis()
    {
        var factory = _fixture.Factory ?? throw new InvalidOperationException("Factory not initialized.");
        var client = factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" },
            Json);
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<Envelope<LoginRow>>(Json);
        Assert.NotNull(login?.Data?.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Data!.AccessToken);

        var floorRes = await client.PostAsJsonAsync(
            "api/v1/floors",
            new { name = "Rssi-F", buildingCode = "RF", widthMeters = 100, heightMeters = 100 },
            Json);
        floorRes.EnsureSuccessStatusCode();
        var floorEnv = await floorRes.Content.ReadFromJsonAsync<Envelope<FloorRow>>(Json);
        var floorId = floorEnv!.Data!.Id;
        const int tx = -59;
        const string u1 = "11111111-1111-1111-1111-111111111101";
        const string u2 = "11111111-1111-1111-1111-111111111102";
        const string u3 = "11111111-1111-1111-1111-111111111103";

        foreach (var b in new[]
                 {
                     (u1, 1, 1, 0.0, 0.0),
                     (u2, 1, 2, 100.0, 0.0),
                     (u3, 1, 3, 50.0, 100.0),
                 })
        {
            var br = await client.PostAsJsonAsync(
                $"api/v1/floors/{floorId}/beacons",
                new
                {
                    uuid = b.Item1, major = b.Item2, minor = b.Item3, x = b.Item4, y = b.Item5, txPower = tx,
                },
                Json);
            br.EnsureSuccessStatusCode();
        }

        var devRes = await client.PostAsJsonAsync(
            "api/v1/devices",
            new { deviceCode = $"rssi-e2e-{Guid.NewGuid():N}", displayName = "E2E", type = 1 },
            Json);
        devRes.EnsureSuccessStatusCode();
        var devEnv = await devRes.Content.ReadFromJsonAsync<Envelope<DeviceCreateRow>>(Json);
        var deviceId = devEnv!.Data!.DeviceId;
        var apiKey = devEnv.Data.ApiKey;
        const double posX = 25.0;
        const double posY = 25.0;
        var signals = new List<object>();
        foreach (var b in new[]
                 {
                     (u1, 1, 1, 0.0, 0.0),
                     (u2, 1, 2, 100.0, 0.0),
                     (u3, 1, 3, 50.0, 100.0),
                 })
        {
            var dx = posX - b.Item4;
            var dy = posY - b.Item5;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var rssi = RssiFromDistanceMeters(d, tx, 2.0);
            signals.Add(new { uuid = b.Item1, major = b.Item2, minor = b.Item3, rssi });
        }

        var rssiClient = factory.CreateClient();
        rssiClient.DefaultRequestHeaders.Remove("Authorization");
        rssiClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        var reportRes = await rssiClient.PostAsJsonAsync(
            "api/v1/rssi/report",
            new
            {
                deviceId,
                signals,
                timestamp = DateTime.UtcNow,
            },
            Json);
        Assert.Equal(HttpStatusCode.Accepted, reportRes.StatusCode);

        // 背景管道为异步
        const int maxTries = 40;
        HttpStatusCode lastCode = 0;
        PositionRow? lastPos = null;
        for (var i = 0; i < maxTries; i++)
        {
            await Task.Delay(100);
            var posRes = await client.GetAsync($"api/v1/devices/{deviceId}/position");
            lastCode = posRes.StatusCode;
            if (posRes.IsSuccessStatusCode)
            {
                var env = await posRes.Content.ReadFromJsonAsync<Envelope<PositionRow>>(Json);
                if (env?.Data is not null)
                {
                    lastPos = env.Data;
                    break;
                }
            }
        }

        Assert.Equal(HttpStatusCode.OK, lastCode);
        Assert.NotNull(lastPos);
        Assert.Equal(floorId, lastPos.FloorId);
        Assert.InRange(lastPos.X, 0, 100);
        Assert.InRange(lastPos.Y, 0, 100);
    }

    private static int RssiFromDistanceMeters(double dMeters, int txPower, double pathLossN)
    {
        var d = Math.Max(0.1, dMeters);
        var r = txPower - 10.0 * pathLossN * Math.Log10(d);
        var ri = (int)Math.Round(r);
        return Math.Clamp(ri, -100, 0);
    }

    private sealed record Envelope<T>(bool Success, T? Data, string? Error, string? TraceId);
    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);
    private sealed record FloorRow(Guid Id, string Name, string BuildingCode, double WidthMeters, double HeightMeters, string? MapImageUrl);
    private sealed record DeviceCreateRow(Guid DeviceId, string DeviceCode, string ApiKey);
    private sealed record PositionRow(
        Guid DeviceId,
        Guid FloorId,
        double X,
        double Y,
        double Accuracy,
        DateTime Timestamp,
        bool IsOnline);
}
