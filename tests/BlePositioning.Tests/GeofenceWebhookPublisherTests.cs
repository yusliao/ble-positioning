using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlePositioning.Application.Geofence;
using BlePositioning.Infrastructure.Geofence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlePositioning.Tests;

public sealed class GeofenceWebhookPublisherTests
{
    private static readonly GeofenceEventNotification SampleNotification = new(
        Guid.Parse("a0000000-0000-0000-0000-000000000001"),
        Guid.Parse("b0000000-0000-0000-0000-000000000002"),
        Guid.Parse("c0000000-0000-0000-0000-000000000003"),
        0,
        1.5,
        2.5,
        new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task When_disabled_does_not_invoke_http()
    {
        var handler = new CountingHandler();
        var publisher = CreatePublisher(handler, o =>
        {
            o.Enabled = false;
            o.Url = "https://example.test/hook";
        });

        await publisher.PublishAsync(SampleNotification, CancellationToken.None);

        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task When_enabled_posts_json_and_signature()
    {
        const string secret = "test-secret-please-change";
        string? method = null;
        string? uri = null;
        IReadOnlyList<string>? signatureValues = null;
        string? body = null;
        var handler = new DelegatingHandlerWithCapture(msg =>
        {
            method = msg.Method.Method;
            uri = msg.RequestUri?.ToString();
            signatureValues = msg.Headers.TryGetValues("X-Ble-Webhook-Signature", out var s)
                ? s.ToList()
                : (IReadOnlyList<string>)Array.Empty<string>();
            body = msg.Content != null
                ? msg.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var publisher = CreatePublisher(handler, o =>
        {
            o.Enabled = true;
            o.Url = "https://webhook.test/path";
            o.Secret = secret;
            o.RequestTimeout = TimeSpan.FromSeconds(5);
        });

        await publisher.PublishAsync(SampleNotification, CancellationToken.None);

        Assert.Equal("POST", method);
        Assert.Equal("https://webhook.test/path", uri);
        Assert.NotNull(body);
        using var doc = JsonDocument.Parse(body!);
        Assert.Equal("1.0", doc.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("a0000000-0000-0000-0000-000000000001", doc.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("eventKind").GetInt32());
        Assert.NotNull(signatureValues);
        Assert.Single(signatureValues!);
        var expectedHex = ToSha256HmacHex(secret, body!);
        Assert.Equal($"sha256={expectedHex}", signatureValues![0], ignoreCase: false);
    }

    [Fact]
    public async Task Composite_invokes_both_in_order()
    {
        var log = new List<string>();
        var first = new SequencePublisher(() => log.Add("A"));
        var second = new SequencePublisher(() => log.Add("B"));
        var composite = new CompositeGeofenceEventPublisher(first, second);

        await composite.PublishAsync(SampleNotification, CancellationToken.None);

        Assert.Equal(new[] { "A", "B" }, log);
    }

    private static string ToSha256HmacHex(string secret, string payload) =>
        Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

    private static HttpGeofenceWebhookPublisher CreatePublisher(
        HttpMessageHandler primaryHandler,
        Action<GeofenceWebhookOptions> configure)
    {
        var o = new GeofenceWebhookOptions();
        configure(o);
        var options = Options.Create(o);
        var client = new HttpClient(primaryHandler) { BaseAddress = new Uri("https://webhook.test/") };
        IHttpClientFactory factory = new FixedHttpClientFactory(client);
        return new HttpGeofenceWebhookPublisher(
            factory,
            options,
            NullLogger<HttpGeofenceWebhookPublisher>.Instance);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class DelegatingHandlerWithCapture(
        Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }

    private sealed class SequencePublisher : IGeofenceEventPublisher
    {
        private readonly Action _onPublish;

        public SequencePublisher(Action onPublish) => _onPublish = onPublish;

        public Task PublishAsync(GeofenceEventNotification notification, CancellationToken ct = default)
        {
            _onPublish();
            return Task.CompletedTask;
        }
    }
}
