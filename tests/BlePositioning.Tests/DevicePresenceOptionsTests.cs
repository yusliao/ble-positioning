using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlePositioning.Tests;

public sealed class DevicePresenceOptionsTests
{
    [Fact]
    public void DevicePresence_section_binds_sweep_state_and_query_max()
    {
        var d = new Dictionary<string, string?>
        {
            ["DevicePresence:StateKeyTtl"] = "2.00:00:00",
            ["DevicePresence:SweepInterval"] = "00:00:12",
            ["DevicePresence:QueryMaxEvents"] = "5000",
        };
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(d).Build();
        var sp = new ServiceCollection()
            .AddOptions<DevicePresenceOptions>()
            .Bind(cfg.GetSection(DevicePresenceOptions.SectionName))
            .Services
            .BuildServiceProvider();
        var o = sp.GetRequiredService<IOptions<DevicePresenceOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(2), o.StateKeyTtl);
        Assert.Equal(TimeSpan.FromSeconds(12), o.SweepInterval);
        Assert.Equal(5000, o.QueryMaxEvents);
    }
}
