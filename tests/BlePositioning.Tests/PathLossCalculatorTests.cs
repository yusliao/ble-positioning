using BlePositioning.Infrastructure.Positioning;

namespace BlePositioning.Tests;

public sealed class PathLossCalculatorTests
{
    [Fact]
    public void RssiZero_Returns_MaxValue()
    {
        var d = PathLossCalculator.RssiToDistance(0, -59, 2.0);
        Assert.Equal(double.MaxValue, d);
    }

    [Theory]
    [InlineData(-59, -59, 2.0, 1.0)]
    // (txPower - rssi) / (10n) = 1 => 10^1 = 10 m
    [InlineData(-79, -59, 2.0, 10.0)]
    public void RssiToDistance_KnownCases(int rssi, int txPower, double n, double expected)
    {
        var d = PathLossCalculator.RssiToDistance(rssi, txPower, n);
        Assert.Equal(expected, d, precision: 10);
    }
}
