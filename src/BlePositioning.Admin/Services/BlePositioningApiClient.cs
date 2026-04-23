using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Floors;
using BlePositioning.Application.Security;
using BlePositioning.Domain;

namespace BlePositioning.Admin.Services;

public sealed record ApiActionResult<T>(bool Ok, T? Data, string? Error);

public sealed record LoginAttemptResult(bool Ok, LoginData? Data, string? ErrorMessage);

public sealed class BlePositioningApiClient(
    IHttpClientFactory httpClientFactory,
    AdminAuthState auth)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<LoginAttemptResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        HttpResponseMessage res;
        try
        {
            var c = httpClientFactory.CreateClient("bleApi");
            res = await c.PostAsJsonAsync(
                "api/v1/auth/login",
                new { username, password },
                Json,
                ct);
        }
        catch (HttpRequestException ex)
        {
            return new LoginAttemptResult(
                false,
                null,
                $"无法连接到 API（{ex.Message}）。请确认 API 已启动，且 Admin 配置的 Api:Base 指向正确根地址。"
                + " 常见情况：API 在 docker compose 映射为 http://localhost:5000；仅 dotnet run API 时多为 http://localhost:5230。"
                + " Admin 在容器内时通过环境变量 Api__Base 连接 api 服务，无需改宿主机浏览器地址。");
        }
        catch (TaskCanceledException ex)
        {
            return new LoginAttemptResult(false, null, $"请求超时或已取消：{ex.Message}");
        }

        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new LoginAttemptResult(
                false,
                null,
                "账号或密码错误。开发默认为 admin / ChangeMe!（注意大小写与末尾 !）。");
        }

        if (!res.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(res, ct);
            return new LoginAttemptResult(
                false,
                null,
                !string.IsNullOrEmpty(detail) ? detail : $"登录失败（HTTP {(int)res.StatusCode}）。");
        }

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<LoginData>>(Json, cancellationToken: ct);
        if (body?.Data is null)
            return new LoginAttemptResult(false, null, "响应中无有效登录数据。请检查 API 与 Admin 是否版本匹配。");
        return new LoginAttemptResult(true, body.Data, null);
    }

    public async Task<ApiResponse<List<FloorDto>>?> GetFloorsAsync(CancellationToken ct = default) =>
        await GetAsync<ApiResponse<List<FloorDto>>>("api/v1/floors", true, ct);

    public async Task<ApiResponse<FloorDto>?> GetFloorByIdAsync(Guid id, CancellationToken ct = default) =>
        await GetAsync<ApiResponse<FloorDto>>($"api/v1/floors/{id}", true, ct);

    public async Task<ApiResponse<List<DeviceSummaryDto>>?> GetDevicesAsync(CancellationToken ct = default) =>
        await GetAsync<ApiResponse<List<DeviceSummaryDto>>>("api/v1/devices", true, ct);

    public async Task<(bool Ok, DevicePositionDto? Data, bool NotFound, string? Error)> GetDevicePositionAsync(
        Guid deviceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return (false, null, false, "未登录。");

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/devices/{deviceId}/position");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return (false, null, true, "设备不存在或尚无位姿。");
        if (!res.IsSuccessStatusCode)
            return (false, null, false, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
        var envelope = await res.Content.ReadFromJsonAsync<ApiResponse<DevicePositionDto>>(Json, cancellationToken: ct);
        if (envelope is { Success: true, Data: not null })
            return (true, envelope.Data, false, null);
        return (false, null, false, "响应数据无效。");
    }

    public Task<ApiActionResult<CreateDeviceApiData>> CreateDeviceAsync(
        string deviceCode,
        string displayName,
        DeviceType type,
        CancellationToken ct = default) =>
        SendEnvelopeAsync<CreateDeviceApiData>(
            HttpMethod.Post,
            "api/v1/devices",
            new { deviceCode, displayName, type = (int)type },
            ct);

    public async Task<ApiResponse<DeviceTrajectoryDto>?> GetTrajectoryAsync(
        Guid deviceId,
        DateTime startUtc,
        DateTime endUtc,
        int intervalSeconds = 1,
        Guid? floorId = null,
        CancellationToken ct = default)
    {
        var path =
            $"api/v1/devices/{deviceId}/trajectory?startTime={Uri.EscapeDataString(startUtc.ToString("O"))}&endTime={Uri.EscapeDataString(endUtc.ToString("O"))}&intervalSeconds={intervalSeconds}";
        if (floorId is not null)
            path += $"&floorId={floorId}";
        return await GetAsync<ApiResponse<DeviceTrajectoryDto>>(path, true, ct);
    }

    public async Task<ApiResponse<List<BeaconListItemDto>>?> GetBeaconsAsync(
        Guid floorId,
        CancellationToken ct = default) =>
        await GetAsync<ApiResponse<List<BeaconListItemDto>>>($"api/v1/floors/{floorId}/beacons", true, ct);

    public async Task<ApiResponse<List<AlertRuleListItemDto>>?> GetAlertRulesAsync(
        Guid floorId,
        CancellationToken ct = default) =>
        await GetAsync<ApiResponse<List<AlertRuleListItemDto>>>($"api/v1/floors/{floorId}/alert-rules", true, ct);

    public Task<ApiActionResult<AlertRuleListItemDto>> CreateAlertRuleAsync(
        Guid floorId,
        CreateAlertRuleRequest request,
        CancellationToken ct = default) =>
        SendEnvelopeAsync<AlertRuleListItemDto>(
            HttpMethod.Post,
            $"api/v1/floors/{floorId}/alert-rules",
            request,
            ct);

    public Task<ApiActionResult<AlertRuleListItemDto>> UpdateAlertRuleAsync(
        Guid floorId,
        Guid ruleId,
        UpdateAlertRuleRequest request,
        CancellationToken ct = default) =>
        SendEnvelopeAsync<AlertRuleListItemDto>(
            HttpMethod.Put,
            $"api/v1/floors/{floorId}/alert-rules/{ruleId}",
            request,
            ct);

    public async Task<ApiActionResult<Unit>> DeleteAlertRuleAsync(Guid floorId, Guid ruleId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return new ApiActionResult<Unit>(false, default, "未登录。");

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/v1/floors/{floorId}/alert-rules/{ruleId}");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.NoContent)
            return new ApiActionResult<Unit>(true, default, null);
        return new ApiActionResult<Unit>(false, default, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
    }

    public Task<ApiActionResult<FloorDto>> CreateFloorAsync(CreateFloorRequest request, CancellationToken ct = default) =>
        SendEnvelopeAsync<FloorDto>(HttpMethod.Post, "api/v1/floors", request, ct);

    public Task<ApiActionResult<FloorDto>> UpdateFloorAsync(Guid id, UpdateFloorRequest request, CancellationToken ct = default) =>
        SendEnvelopeAsync<FloorDto>(HttpMethod.Put, $"api/v1/floors/{id}", request, ct);

    public async Task<ApiActionResult<FloorDto>> UploadMapImageAsync(
        Guid floorId,
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return new ApiActionResult<FloorDto>(false, default, "未登录。");

        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        var ctOnly = contentType.Split(';')[0].Trim();
        if (!MediaTypeHeaderValue.TryParse(ctOnly, out var parsed))
            parsed = new MediaTypeHeaderValue("application/octet-stream");
        fileContent.Headers.ContentType = parsed;
        content.Add(fileContent, "file", fileName);

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"api/v1/floors/{floorId}/map-image");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        msg.Content = content;
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.IsSuccessStatusCode)
        {
            var envelope = await res.Content.ReadFromJsonAsync<ApiResponse<FloorDto>>(Json, cancellationToken: ct);
            if (envelope?.Success == true && envelope.Data is not null)
                return new ApiActionResult<FloorDto>(true, envelope.Data, null);
            return new ApiActionResult<FloorDto>(false, default, "响应数据无效。");
        }

        return new ApiActionResult<FloorDto>(false, default, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
    }

    public async Task<ApiActionResult<Unit>> DeleteFloorAsync(Guid id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return new ApiActionResult<Unit>(false, default, "未登录。");

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/floors/{id}");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.NoContent)
            return new ApiActionResult<Unit>(true, default, null);
        return new ApiActionResult<Unit>(false, default, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
    }

    public Task<ApiActionResult<BeaconListItemDto>> CreateBeaconAsync(
        Guid floorId,
        CreateBeaconRequest request,
        CancellationToken ct = default) =>
        SendEnvelopeAsync<BeaconListItemDto>(HttpMethod.Post, $"api/v1/floors/{floorId}/beacons", request, ct);

    public Task<ApiActionResult<BeaconListItemDto>> UpdateBeaconAsync(
        Guid floorId,
        Guid beaconId,
        UpdateBeaconRequest request,
        CancellationToken ct = default) =>
        SendEnvelopeAsync<BeaconListItemDto>(
            HttpMethod.Put,
            $"api/v1/floors/{floorId}/beacons/{beaconId}",
            request,
            ct);

    public async Task<ApiActionResult<Unit>> DeleteBeaconAsync(Guid floorId, Guid beaconId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return new ApiActionResult<Unit>(false, default, "未登录。");

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/v1/floors/{floorId}/beacons/{beaconId}");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.NoContent)
            return new ApiActionResult<Unit>(true, default, null);
        return new ApiActionResult<Unit>(false, default, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
    }

    private async Task<ApiActionResult<T>> SendEnvelopeAsync<T>(
        HttpMethod method,
        string path,
        object body,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(auth.BearerToken))
            return new ApiActionResult<T>(false, default, "未登录。");

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(method, path);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        msg.Content = JsonContent.Create(body, options: Json);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.IsSuccessStatusCode)
        {
            var envelope = await res.Content.ReadFromJsonAsync<ApiResponse<T>>(Json, cancellationToken: ct);
            if (envelope?.Success == true && envelope.Data is not null)
                return new ApiActionResult<T>(true, envelope.Data, null);
            return new ApiActionResult<T>(false, default, "响应数据无效。");
        }

        return new ApiActionResult<T>(false, default, await ReadProblemDetailAsync(res, ct) ?? res.ReasonPhrase);
    }

    private async Task<T?> GetAsync<T>(string path, bool requireAuth, CancellationToken ct)
    {
        if (requireAuth && string.IsNullOrEmpty(auth.BearerToken))
            return default;

        var c = httpClientFactory.CreateClient("bleApi");
        using var msg = new HttpRequestMessage(HttpMethod.Get, path);
        if (requireAuth)
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        var res = await c.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
            return default;
        return await res.Content.ReadFromJsonAsync<T>(Json, cancellationToken: ct);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var text = await res.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("detail", out var d))
                return d.GetString();
            if (doc.RootElement.TryGetProperty("title", out var t))
                return t.GetString();
        }
        catch (JsonException)
        {
            // ignore
        }

        return text.Length > 200 ? text[..200] : text;
    }
}

/// <summary>用于无返回体的成功占位（如 204）。</summary>
public readonly struct Unit;

public sealed class LoginData
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }

    public string Role { get; set; } = BlePositioningRoles.Admin;
}

public sealed class CreateDeviceApiData
{
    public Guid DeviceId { get; set; }
    public string DeviceCode { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
