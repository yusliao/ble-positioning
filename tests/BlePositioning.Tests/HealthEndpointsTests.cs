using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlePositioning.Tests;

public sealed class HealthEndpointsTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(ApiWebAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Get_Health_Returns_200_And_Json()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<HealthDto>(ClientJsonOptions);
        Assert.NotNull(json);
        Assert.Equal("Healthy", json.Status);
    }

    [Fact]
    public async Task Get_HealthReady_Returns_200_When_Db_And_Redis_Available()
    {
        var res = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<HealthDto>(ClientJsonOptions);
        Assert.NotNull(json);
        Assert.Equal("Healthy", json.Status);
    }

    private static readonly JsonSerializerOptions ClientJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record HealthDto(string Status);
}
