using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Devices;

namespace BlePositioning.Tests;

public sealed class DevicesListApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public DevicesListApiTests(ApiWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_devices_requires_auth()
    {
        var res = await _client.GetAsync("/api/v1/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Get_devices_returns_envelope()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json))!.Data!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/devices");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<List<DeviceSummaryDto>>>(Json);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
    }

    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);
}
