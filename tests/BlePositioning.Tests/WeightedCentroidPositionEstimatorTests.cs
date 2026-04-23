using BlePositioning.Application.Positioning;
using BlePositioning.Infrastructure.Positioning;

namespace BlePositioning.Tests;

public sealed class WeightedCentroidPositionEstimatorTests
{
    private static BeaconPlacement B(double x, double y) =>
        new("uuid", 1, 1, x, y, -59, Guid.NewGuid(), 100, 100);

    [Fact]
    public void Estimate_LessThanThree_Returns_Null()
    {
        var r = WeightedCentroidPositionEstimator.Estimate([(B(0, 0), 1.0), (B(1, 0), 1.0)]);
        Assert.Null(r);
    }

    [Fact]
    public void Estimate_ThreeBeacons_Returns_WeightedCenter()
    {
        var placements = new List<(BeaconPlacement Beacon, double Distance)>
        {
            (B(0, 0), 1.0),
            (B(2, 0), 1.0),
            (B(0, 2), 1.0),
        };
        var r = WeightedCentroidPositionEstimator.Estimate(placements);
        Assert.NotNull(r);
        Assert.InRange(r.Value.X, 0.65, 0.75);
        Assert.InRange(r.Value.Y, 0.65, 0.75);
        Assert.True(r.Value.Accuracy >= 0.5);
    }

    [Fact]
    public void ClampToFloor_Clips_To_FloorBounds()
    {
        var b = B(0, 0);
        var (x, y) = WeightedCentroidPositionEstimator.ClampToFloor(150, 200, b);
        Assert.Equal(100, x);
        Assert.Equal(100, y);
    }
}
