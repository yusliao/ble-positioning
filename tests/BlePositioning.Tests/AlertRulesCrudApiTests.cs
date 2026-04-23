using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;

namespace BlePositioning.Tests;

public sealed class AlertRulesCrudApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public AlertRulesCrudApiTests(ApiWebAppFactory factory) => _client = factory.CreateClient();

    private const string ValidPolygon =
        """{"type":"Polygon","coordinates":[[[0,0],[5,0],[5,4],[0,4],[0,0]]]}""";

    [Fact]
    public async Task Alert_rule_crud_and_list_contains_rule()
    {
        await using var _ = await AuthorizeAdminAsync();

        var floorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest("L-rules", "B-rules", 20, 15));
        floorRes.EnsureSuccessStatusCode();
        var floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(Json))!.Data!.Id;

        var createRes = await _client.PostAsJsonAsync(
            $"/api/v1/floors/{floorId}/alert-rules",
            new CreateAlertRuleRequest("禁区-1", ValidPolygon, 0, true));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<AlertRuleListItemDto>>(Json);
        Assert.NotNull(created?.Data);
        var ruleId = created.Data.Id;

        var listRes = await _client.GetAsync($"/api/v1/floors/{floorId}/alert-rules");
        listRes.EnsureSuccessStatusCode();
        var list = await listRes.Content.ReadFromJsonAsync<ApiResponse<List<AlertRuleListItemDto>>>(Json);
        Assert.NotNull(list?.Data);
        Assert.Single(list.Data);
        Assert.Equal("禁区-1", list.Data[0].Name);

        var putRes = await _client.PutAsJsonAsync(
            $"/api/v1/floors/{floorId}/alert-rules/{ruleId}",
            new UpdateAlertRuleRequest("禁区-改", ValidPolygon, 1, false));
        putRes.EnsureSuccessStatusCode();
        var updated = await putRes.Content.ReadFromJsonAsync<ApiResponse<AlertRuleListItemDto>>(Json);
        Assert.Equal("禁区-改", updated?.Data?.Name);
        Assert.False(updated?.Data?.IsEnabled);
        Assert.Equal((short)1, updated?.Data?.TriggerOn);

        var del = await _client.DeleteAsync($"/api/v1/floors/{floorId}/alert-rules/{ruleId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var list2Res = await _client.GetAsync($"/api/v1/floors/{floorId}/alert-rules");
        list2Res.EnsureSuccessStatusCode();
        var list2 = await list2Res.Content.ReadFromJsonAsync<ApiResponse<List<AlertRuleListItemDto>>>(Json);
        Assert.NotNull(list2?.Data);
        Assert.Empty(list2.Data!);
    }

    [Fact]
    public async Task Invalid_zone_polygon_returns_400()
    {
        await using var _ = await AuthorizeAdminAsync();
        var floorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest($"L-badz-{Guid.NewGuid():N}", "B", 20, 15));
        floorRes.EnsureSuccessStatusCode();
        var floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(Json))!.Data!.Id;

        var res = await _client.PostAsJsonAsync(
            $"/api/v1/floors/{floorId}/alert-rules",
            new CreateAlertRuleRequest("bad", "not-json", 0, true));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_trigger_on_returns_400()
    {
        await using var _ = await AuthorizeAdminAsync();
        var floorRes = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest($"L-badt-{Guid.NewGuid():N}", "B", 20, 15));
        floorRes.EnsureSuccessStatusCode();
        var floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(Json))!.Data!.Id;

        var res = await _client.PostAsJsonAsync(
            $"/api/v1/floors/{floorId}/alert-rules",
            new CreateAlertRuleRequest("t", ValidPolygon, 9, true));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_create_alert_rule()
    {
        Guid floorId;
        await using (var _ = await AuthorizeAdminAsync())
        {
            var floorRes = await _client.PostAsJsonAsync(
                "/api/v1/floors",
                new CreateFloorRequest($"L-view-{Guid.NewGuid():N}", "B", 20, 15));
            floorRes.EnsureSuccessStatusCode();
            floorId = (await floorRes.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(Json))!.Data!.Id;
        }

        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "viewer", password = "ViewOnly!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json))!.Data!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.PostAsJsonAsync(
            $"/api/v1/floors/{floorId}/alert-rules",
            new CreateAlertRuleRequest("v", ValidPolygon, 0, true));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<AuthScope> AuthorizeAdminAsync()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        login.EnsureSuccessStatusCode();
        var t = (await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json))!.Data!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return new AuthScope(_client);
    }

    private sealed class AuthScope(HttpClient c) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            c.DefaultRequestHeaders.Authorization = null;
            await Task.CompletedTask;
        }
    }

    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
}
