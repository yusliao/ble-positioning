using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;

namespace BlePositioning.Tests;

public sealed class FloorsAndBeaconsApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public FloorsAndBeaconsApiTests(ApiWebAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Floor_and_beacon_crud_roundtrip()
    {
        await using var auth = await AuthorizeAsync();

        var createFloorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest("L1", "B1", 100, 80));
        createFloorRes.EnsureSuccessStatusCode();
        var floorEnvelope = await createFloorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(ClientJsonOptions);
        Assert.NotNull(floorEnvelope?.Data);
        var floorId = floorEnvelope.Data.Id;

        var createBeaconRes = await _client.PostAsJsonAsync(
            $"/api/v1/floors/{floorId}/beacons",
            new CreateBeaconRequest("550e8400-e29b-41d4-a716-446655440000", 1, 2, 10, 20, -59));
        Assert.Equal(HttpStatusCode.Created, createBeaconRes.StatusCode);
        var createdBeacon = await createBeaconRes.Content.ReadFromJsonAsync<ApiResponse<BeaconListItemDto>>(ClientJsonOptions);
        Assert.NotNull(createdBeacon?.Data);
        var beaconId = createdBeacon.Data.Id;

        var updateRes = await _client.PutAsJsonAsync(
            $"/api/v1/floors/{floorId}/beacons/{beaconId}",
            new UpdateBeaconRequest(11, 22, -60));
        updateRes.EnsureSuccessStatusCode();
        var updated = await updateRes.Content.ReadFromJsonAsync<ApiResponse<BeaconListItemDto>>(ClientJsonOptions);
        Assert.NotNull(updated?.Data);
        Assert.Equal(11, updated.Data.X);
        Assert.Equal(22, updated.Data.Y);
        Assert.Equal(-60, updated.Data.TxPower);

        var listRes = await _client.GetAsync($"/api/v1/floors/{floorId}/beacons");
        listRes.EnsureSuccessStatusCode();
        var list = await listRes.Content.ReadFromJsonAsync<ApiResponse<List<BeaconListItemDto>>>(ClientJsonOptions);
        Assert.NotNull(list?.Data);
        Assert.Single(list.Data);

        var delBeacon = await _client.DeleteAsync($"/api/v1/floors/{floorId}/beacons/{beaconId}");
        Assert.Equal(HttpStatusCode.NoContent, delBeacon.StatusCode);

        var delFloor = await _client.DeleteAsync($"/api/v1/floors/{floorId}");
        Assert.Equal(HttpStatusCode.NoContent, delFloor.StatusCode);
    }

    [Fact]
    public async Task List_alert_rules_unknown_floor_returns_404()
    {
        await using var auth = await AuthorizeAsync();
        var res = await _client.GetAsync($"/api/v1/floors/{Guid.NewGuid()}/alert-rules");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task List_alert_rules_empty_for_new_floor()
    {
        await using var auth = await AuthorizeAsync();
        var floorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest("L-ar", "B-ar", 10, 10));
        floorRes.EnsureSuccessStatusCode();
        var floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(ClientJsonOptions))!.Data!.Id;
        var res = await _client.GetAsync($"/api/v1/floors/{floorId}/alert-rules");
        res.EnsureSuccessStatusCode();
        var env = await res.Content.ReadFromJsonAsync<ApiResponse<List<AlertRuleListItemDto>>>(ClientJsonOptions);
        Assert.NotNull(env?.Data);
        Assert.Empty(env.Data);
    }

    [Fact]
    public async Task Create_beacon_duplicate_identity_returns_409()
    {
        await using var auth = await AuthorizeAsync();

        var floorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest("L-dup", "B-dup", 50, 50));
        floorRes.EnsureSuccessStatusCode();
        var floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(ClientJsonOptions))!.Data!.Id;

        var key = new CreateBeaconRequest("550e8400-e29b-41d4-a716-446655440099", 9, 9, 1, 1);
        Assert.True((await _client.PostAsJsonAsync($"/api/v1/floors/{floorId}/beacons", key)).IsSuccessStatusCode);

        var dup = await _client.PostAsJsonAsync($"/api/v1/floors/{floorId}/beacons", key);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    private async Task<AuthScope> AuthorizeAsync()
    {
        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginPayload("admin", "ChangeMe!"));
        loginRes.EnsureSuccessStatusCode();
        var login = await loginRes.Content.ReadFromJsonAsync<ApiResponse<LoginPayloadResponse>>(ClientJsonOptions);
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

    private sealed record LoginPayload(string Username, string Password);

    private sealed record LoginPayloadResponse(string AccessToken, DateTime ExpiresAtUtc, string Role);

    private static readonly JsonSerializerOptions ClientJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
