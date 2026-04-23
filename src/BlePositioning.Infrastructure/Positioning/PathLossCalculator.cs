namespace BlePositioning.Infrastructure.Positioning;

public static class PathLossCalculator
{
    public static double RssiToDistance(int rssi, int txPower, double pathLossExponent)
    {
        if (rssi == 0) return double.MaxValue;
        return Math.Pow(10, (txPower - rssi) / (10.0 * pathLossExponent));
    }
}
