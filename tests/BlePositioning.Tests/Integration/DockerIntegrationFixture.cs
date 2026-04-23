using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace BlePositioning.Tests.Integration;

/// <summary>启动 PostgreSQL 16 与 Redis 7，并构造带生产依赖的 <see cref="WebApplicationFactory{Program}"/>。</summary>
public sealed class DockerIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;
    public IntegrationWebAppFactory? Factory { get; private set; }

    public string PostgresConnectionString =>
        _postgres?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgreSQL container is not initialized.");

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("blepositioning")
            .WithUsername("postgres")
            .WithPassword("tc_integration")
            .Build();
        _redis = new RedisBuilder("redis:7-alpine")
            .Build();
        await _postgres.StartAsync();
        await _redis.StartAsync();
        var cfg = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
        };
        Factory = new IntegrationWebAppFactory(cfg);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        if (_redis is not null)
            await _redis.DisposeAsync();
    }
}

[CollectionDefinition("docker")]
public sealed class DockerCollection : ICollectionFixture<DockerIntegrationFixture>
{
}
