using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Devices;

namespace BlePositioning.Tests;

public sealed class DeviceTrajectoryApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public DeviceTrajectoryApiTests(ApiWebAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Get_trajectory_requires_auth()
    {
        var id = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;
        var res = await _client.GetAsync(
            $"/api/v1/devices/{id}/trajectory?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_trajectory_invalid_interval_returns_400()
    {
        await using var auth = await AuthorizeAsync();
        var id = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;
        var res = await _client.GetAsync(
            $"/api/v1/devices/{id}/trajectory?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}&intervalSeconds=0");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Get_trajectory_unknown_device_returns_404()
    {
        await using var auth = await AuthorizeAsync();
        var id = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;
        var res = await _client.GetAsync(
            $"/api/v1/devices/{id}/trajectory?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Get_trajectory_existing_device_returns_envelope_with_empty_points()
    {
        await using var auth = await AuthorizeAsync();
        var code = $"traj-{Guid.NewGuid():N}";
        var createRes = await _client.PostAsJsonAsync(
            "/api/v1/devices",
            new { deviceCode = code, displayName = "T", type = 1 });
        createRes.EnsureSuccessStatusCode();
        var env = await createRes.Content.ReadFromJsonAsync<ApiResponse<CreateDevicePayload>>(ClientJsonOptions);
        Assert.NotNull(env?.Data);
        var id = env.Data.DeviceId;

        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;
        var res = await _client.GetAsync(
            $"/api/v1/devices/{id}/trajectory?startTime={Uri.EscapeDataString(start.ToString("O"))}&endTime={Uri.EscapeDataString(end.ToString("O"))}");
        res.EnsureSuccessStatusCode();
        var traj = await res.Content.ReadFromJsonAsync<ApiResponse<DeviceTrajectoryDto>>(ClientJsonOptions);
        Assert.NotNull(traj?.Data);
        Assert.Equal(id, traj.Data.DeviceId);
        Assert.Equal(0, traj.Data.TotalPoints);
        Assert.Empty(traj.Data.Points);
    }

    private async Task<AuthScope> AuthorizeAsync()
    {
        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<ApiResponse<LoginData>>(ClientJsonOptions);
        Assert.NotNull(login?.Data?.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
        return new AuthScope(_client);
    }

    private sealed class AuthScope(HttpClient client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            client.DefaultRequestHeaders.Authorization = null;
            await Task.CompletedTask;
        }
    }

    private sealed record LoginData(string AccessToken, DateTime ExpiresAtUtc, string Role);

    private sealed record CreateDevicePayload(Guid DeviceId, string DeviceCode, string ApiKey);

    private static readonly JsonSerializerOptions ClientJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
