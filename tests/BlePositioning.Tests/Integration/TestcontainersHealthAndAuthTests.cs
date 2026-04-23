using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlePositioning.Tests.Integration;

[Collection("docker")]
[Trait("Category", "docker")]
public sealed class TestcontainersHealthAndAuthTests
{
    private readonly DockerIntegrationFixture _fixture;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public TestcontainersHealthAndAuthTests(DockerIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Health_And_Ready_Return_200_With_Real_Pg_And_Redis()
    {
        var factory = _fixture.Factory ?? throw new InvalidOperationException("Factory not initialized.");
        var client = factory.CreateClient();
        var h = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, h.StatusCode);
        var r = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<HealthDto>(Json);
        Assert.Equal("Healthy", body?.Status);
    }

    [Fact]
    public async Task Login_Then_ListFloors_Returns_200_Envelope()
    {
        var factory = _fixture.Factory ?? throw new InvalidOperationException("Factory not initialized.");
        var client = factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { username = "admin", password = "ChangeMe!" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var envelope = await loginRes.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>(Json);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.False(string.IsNullOrEmpty(envelope.Data?.AccessToken));
        Assert.Equal("Admin", envelope.Data?.Role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", envelope.Data!.AccessToken);
        var floorsRes = await client.GetAsync("api/v1/floors");
        Assert.Equal(HttpStatusCode.OK, floorsRes.StatusCode);
        var floorsBody = await floorsRes.Content.ReadFromJsonAsync<ApiEnvelope<List<FloorRow>>>(Json);
        Assert.NotNull(floorsBody);
        Assert.True(floorsBody.Success);
        Assert.NotNull(floorsBody.Data);
    }

    private sealed record HealthDto(string Status);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, string? Error, string? TraceId);
    private sealed record LoginData(string AccessToken, DateTime ExpiresAtUtc, string Role);
    private sealed record FloorRow(Guid Id, string Name, string BuildingCode, double WidthMeters, double HeightMeters, string? MapImageUrl);
}
