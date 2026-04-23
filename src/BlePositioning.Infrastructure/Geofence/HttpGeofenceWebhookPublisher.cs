using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlePositioning.Application.Geofence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlePositioning.Infrastructure.Geofence;

/// <summary>将进/出区事件以 JSON POST 到配置的 URL；5xx/网络失败可重试，成功后不再抛错；用尽力而为，不阻塞其它发布器。</summary>
public sealed class HttpGeofenceWebhookPublisher : IGeofenceEventPublisher
{
    public const string HttpClientName = "GeofenceWebhook";

    private static readonly MediaTypeHeaderValue JsonMediaType =
        MediaTypeHeaderValue.Parse("application/json; charset=utf-8");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<GeofenceWebhookOptions> _options;
    private readonly ILogger<HttpGeofenceWebhookPublisher> _logger;

    public HttpGeofenceWebhookPublisher(
        IHttpClientFactory httpClientFactory,
        IOptions<GeofenceWebhookOptions> options,
        ILogger<HttpGeofenceWebhookPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task PublishAsync(GeofenceEventNotification n, CancellationToken ct = default)
    {
        var opt = _options.Value;
        if (!opt.Enabled || string.IsNullOrWhiteSpace(opt.Url) || !Uri.TryCreate(opt.Url, UriKind.Absolute, out var uri))
            return;

        if (n.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            _logger.LogWarning("Webhook: expected UTC OccurredAtUtc for device {DeviceId}, skipping", n.DeviceId);
            return;
        }

        var bodyModel = new GeofenceWebhookEventPayload
        {
            SchemaVersion = opt.SchemaVersion,
            DeviceId = n.DeviceId,
            FloorId = n.FloorId,
            AlertRuleId = n.AlertRuleId,
            EventKind = n.EventKind,
            X = n.X,
            Y = n.Y,
            OccurredAtUtc = n.OccurredAtUtc,
        };

        var json = JsonSerializer.Serialize(bodyModel, JsonOptions);
        var bodyBytes = Encoding.UTF8.GetBytes(json);
        var headerName = string.IsNullOrEmpty(opt.SignatureHeaderName) ? "X-Ble-Webhook-Signature" : opt.SignatureHeaderName;

        var max = Math.Max(1, opt.MaxAttempts);
        for (var attempt = 0; attempt < max; attempt++)
        {
            try
            {
                var (ok, terminal, retry) = await TrySendAsync(uri, bodyBytes, headerName, opt, ct).ConfigureAwait(false);
                if (ok)
                {
                    if (attempt > 0)
                    {
                        _logger.LogInformation(
                            "Geofence webhook delivered after {Attempt} attempt(s) to {Url}",
                            attempt + 1,
                            uri);
                    }
                    return;
                }

                if (terminal)
                    return;

                if (!retry || attempt >= max - 1)
                {
                    _logger.LogError(
                        "Geofence webhook failed for device {DeviceId} after {Attempt} attempt(s) to {Url}",
                        n.DeviceId,
                        attempt + 1,
                        uri);
                    return;
                }
            }
            catch (Exception ex) when (attempt < max - 1)
            {
                _logger.LogWarning(
                    ex,
                    "Geofence webhook attempt {Attempt}/{Max} failed for device {DeviceId}",
                    attempt + 1,
                    max,
                    n.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Geofence webhook failed after {Max} attempt(s) for device {DeviceId}",
                    max,
                    n.DeviceId);
                return;
            }

            if (attempt < max - 1)
            {
                var delay = ComputeDelay(opt.RetryBaseDelay, attempt);
                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
    }

    private async Task<(bool Ok, bool Terminal, bool Retry)> TrySendAsync(
        Uri uri,
        byte[] bodyBytes,
        string signatureHeaderName,
        GeofenceWebhookOptions opt,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new ByteArrayContent(bodyBytes),
        };
        request.Content!.Headers.ContentType = JsonMediaType;

        if (!string.IsNullOrEmpty(opt.Secret))
        {
            var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(opt.Secret), bodyBytes);
            var hex = Convert.ToHexString(mac).ToLowerInvariant();
            request.Headers.TryAddWithoutValidation(signatureHeaderName, $"sha256={hex}");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(opt.RequestTimeout);

        try
        {
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return (true, false, false);

            var code = (int)response.StatusCode;
            if (code >= 500 || code == 408)
            {
                _logger.LogWarning("Geofence webhook server/transient {Status} from {Url}", code, uri);
                return (false, false, true);
            }

            _logger.LogWarning("Geofence webhook non-retryable {Status} from {Url}", code, uri);
            return (false, true, false);
        }
        catch (HttpRequestException)
        {
            return (false, false, true);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, false, true);
        }
    }

    private static TimeSpan ComputeDelay(TimeSpan @base, int failedAttempt) =>
        TimeSpan.FromMilliseconds(
            Math.Min(
                @base.TotalMilliseconds * Math.Pow(2, failedAttempt),
                TimeSpan.FromSeconds(30).TotalMilliseconds));
}
