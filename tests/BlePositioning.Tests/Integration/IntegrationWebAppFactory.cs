using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BlePositioning.Tests.Integration;

/// <summary>真实 Npgsql + Redis：连接串由 Testcontainers 注入。环境为 Integration 以执行 EF 迁移。</summary>
public sealed class IntegrationWebAppFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configuration;

    public IntegrationWebAppFactory(IReadOnlyDictionary<string, string?> configuration) =>
        _configuration = configuration;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");
        // UseSetting 优先级高于 content root 的 appsettings，避免仍连 localhost:5432
        foreach (var kv in _configuration)
            builder.UseSetting(kv.Key, kv.Value ?? "");
    }
}
