using System.Threading.Channels;
using BlePositioning.Application.Positioning;

namespace BlePositioning.Infrastructure.Positioning;

public sealed class RssiIngestChannel : IRssiReportQueue
{
    private readonly Channel<RssiReportDto> _channel = Channel.CreateBounded<RssiReportDto>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<RssiReportDto> Reader => _channel.Reader;

    public bool TryEnqueue(RssiReportDto report) => _channel.Writer.TryWrite(report);
}
