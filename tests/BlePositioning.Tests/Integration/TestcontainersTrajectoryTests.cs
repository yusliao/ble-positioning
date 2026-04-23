using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace BlePositioning.Tests.Integration;

[Collection("docker")]
[Trait("Category", "docker")]
public sealed class TestcontainersTrajectoryTests
{
    private readonly DockerIntegrationFixture _fixture;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public TestcontainersTrajectoryTests(DockerIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Trajectory_returns_point_after_position_log_insert()
    {
        var factory = _fixture.Factory ?? throw new InvalidOperationException("Factory not initialized.");
        var client = factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<Envelope<LoginRow>>(Json);
        Assert.NotNull(login?.Data?.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);

        var floorRes = await client.PostAsJsonAsync(
            "api/v1/floors",
            new { name = "T-Floor", buildingCode = "TB", widthMeters = 100, heightMeters = 80 });
        floorRes.EnsureSuccessStatusCode();
        var floorEnv = await floorRes.Content.ReadFromJsonAsync<Envelope<FloorRow>>(Json);
        var floorId = floorEnv!.Data!.Id;

        var devRes = await client.PostAsJsonAsync(
            "api/v1/devices",
            new { deviceCode = $"traj-dev-{Guid.NewGuid():N}", displayName = "T", type = 1 });
        devRes.EnsureSuccessStatusCode();
        var devEnv = await devRes.Content.ReadFromJsonAsync<Envelope<DeviceCreateRow>>(Json);
        var deviceId = devEnv!.Data!.DeviceId;

        var ts = DateTime.UtcNow;
        await using (var conn = new NpgsqlConnection(_fixture.PostgresConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO position_logs (device_id, floor_id, x, y, accuracy, "timestamp")
                VALUES (@d, @f, 3.25, 4.50, 1.00, @t)
                """,
                conn);
            cmd.Parameters.AddWithValue("d", deviceId);
            cmd.Parameters.AddWithValue("f", floorId);
            cmd.Parameters.AddWithValue("t", ts);
            Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
        }

        var start = ts.AddMinutes(-5);
        var end = ts.AddMinutes(5);
        var url =
            $"api/v1/devices/{deviceId}/trajectory?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}&intervalSeconds=1";
        var trajRes = await client.GetAsync(url);
        trajRes.EnsureSuccessStatusCode();
        var traj = await trajRes.Content.ReadFromJsonAsync<Envelope<TrajectoryRow>>(Json);
        Assert.NotNull(traj?.Data);
        Assert.True(traj.Data.TotalPoints >= 1);
        Assert.NotEmpty(traj.Data.Points);
        var p = traj.Data.Points[0];
        Assert.Equal(floorId, p.FloorId);
        Assert.Equal(3.25, p.X);
        Assert.Equal(4.50, p.Y);
    }

    private sealed record Envelope<T>(bool Success, T? Data, string? Error, string? TraceId);
    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);
    private sealed record FloorRow(Guid Id, string Name, string BuildingCode, double WidthMeters, double HeightMeters, string? MapImageUrl);
    private sealed record DeviceCreateRow(Guid DeviceId, string DeviceCode, string ApiKey);
    private sealed record TrajectoryRow(Guid DeviceId, int TotalPoints, List<PointRow> Points);
    private sealed record PointRow(double X, double Y, Guid FloorId, DateTime Timestamp);
}
