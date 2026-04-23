using System.Net;
using System.Net.Http.Json;

namespace BlePositioning.Tests;

/// <summary>无需 Docker：验证未带 <c>X-Api-Key</c> 时 RSSI 入口为 401。</summary>
public sealed class RssiUnauthenticatedTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public RssiUnauthenticatedTests(ApiWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Post_Report_Without_ApiKey_Returns_401()
    {
        var body = new
        {
            deviceId = Guid.NewGuid(),
            signals = new[] { new { uuid = "00000000-0000-0000-0000-000000000001", major = 1, minor = 1, rssi = -70 } },
            timestamp = DateTime.UtcNow,
        };
        var res = await _client.PostAsJsonAsync("api/v1/rssi/report", body);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
