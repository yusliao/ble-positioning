using BlePositioning.Application.Devices;
using BlePositioning.Application.Floors;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace BlePositioning.Tests;

/// <summary>
/// Testing 环境：InMemory EF + 模拟 Redis，避免集成测试必起 Docker。
/// </summary>
public sealed class ApiWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                         d.ServiceType == typeof(AppDbContext) ||
                         d.ServiceType == typeof(IDbContextFactory<AppDbContext>))
                     .ToList())
            {
                services.Remove(d);
            }

            services.AddDbContext<AppDbContext>(
                o => o.UseInMemoryDatabase("smoke_int"),
                ServiceLifetime.Scoped,
                ServiceLifetime.Singleton);
            services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase("smoke_int"));

            services.RemoveAll<ITrajectoryQuery>();
            services.AddSingleton<ITrajectoryQuery, EmptyTrajectoryQuery>();

            services.RemoveAll<IFloorMapStorage>();
            services.AddSingleton<IFloorMapStorage, TestFloorMapStorage>();

            services.RemoveAll<IConnectionMultiplexer>();
            var database = new Mock<IDatabase>();
            database
                .Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
                .ReturnsAsync(TimeSpan.FromMilliseconds(1));
            var multiplexer = new Mock<IConnectionMultiplexer>();
            multiplexer
                .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                .Returns(database.Object);
            services.AddSingleton(multiplexer.Object);
        });
    }
}
