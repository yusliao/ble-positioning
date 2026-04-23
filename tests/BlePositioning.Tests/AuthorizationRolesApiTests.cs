using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Floors;
using BlePositioning.Application.Security;

namespace BlePositioning.Tests;

public sealed class AuthorizationRolesApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AuthorizationRolesApiTests(ApiWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Admin_login_includes_role_Admin_in_envelope()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        login.EnsureSuccessStatusCode();
        var env = await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json);
        Assert.Equal(BlePositioningRoles.Admin, env?.Data?.Role);
    }

    [Fact]
    public async Task Viewer_gets_200_on_list_floors_and_403_on_create_floor()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "viewer", password = "ViewOnly!" });
        login.EnsureSuccessStatusCode();
        var env = await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json);
        Assert.Equal(BlePositioningRoles.Viewer, env?.Data?.Role);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);

        var list = await _client.GetAsync("/api/v1/floors");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest($"v-{Guid.NewGuid():N}", "b", 5, 5));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Viewer_gets_403_on_create_device()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "viewer", password = "ViewOnly!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json))!.Data!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync(
            "/api/v1/devices",
            new { deviceCode = $"d-{Guid.NewGuid():N}", displayName = "x", type = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Admin_token_can_create_floor()
    {
        await using var _ = await LoginAsAdminAsync();
        var create = await _client.PostAsJsonAsync(
            "/api/v1/floors",
            new CreateFloorRequest($"a-{Guid.NewGuid():N}", "b", 10, 10));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
    }

    private async Task<AuthScope> LoginAsAdminAsync()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        login.EnsureSuccessStatusCode();
        var env = await login.Content.ReadFromJsonAsync<ApiResponse<LoginRow>>(Json);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
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

    private sealed record LoginRow(string AccessToken, DateTime ExpiresAtUtc, string Role);
}
