namespace BlePositioning.Application.Positioning;

public interface IRssiReportQueue
{
    bool TryEnqueue(RssiReportDto report);
}
