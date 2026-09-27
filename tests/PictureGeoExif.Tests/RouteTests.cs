using PictureExifclone.Models;
using PictureExifclone.Services;

namespace PictureGeoExif.Tests;

public class RouteTests
{
    // ~1 m in degrees at 50°N
    private const double LatMeter = 1 / 110_574.0, LonMeter = 1 / (111_320.0 * 0.6428);

    private static RoutePoint P(int id, double northMeters, double eastMeters, double lat0 = 50.5, double lon0 = 8.19) =>
        new(id, lat0 + northMeters * LatMeter, lon0 + eastMeters * LonMeter);

    private static int[] Ids(Route route) => route.Points.Select(p => p.Id).ToArray();

    [Fact]
    public void NorthSouthTrench_IsOrderedSouthToNorth()
    {
        var shuffled = new[] { P(3, 90, 2), P(0, 0, 0), P(4, 120, -1), P(1, 30, 1), P(2, 60, 0) };
        var route = Assert.Single(RouteBuilder.Build(shuffled, 200));
        Assert.True(route.NorthSouth);
        Assert.Equal([0, 1, 2, 3, 4], Ids(route));
        Assert.InRange(route.LengthMeters, 119, 125);
    }

    [Fact]
    public void WestEastTrench_IsOrderedWestToEast()
    {
        var points = new[] { P(2, 1, 80), P(0, 0, 0), P(1, -2, 40), P(3, 0, 120) };
        var route = Assert.Single(RouteBuilder.Build(points, 200));
        Assert.False(route.NorthSouth);
        Assert.Equal([0, 1, 2, 3], Ids(route));
    }

    [Fact]
    public void LShapedTrench_FollowsTheCourse_NotJustTheAxis()
    {
        // North 0..100 m, then east 20..100 m at the top: the path must stay continuous.
        var points = new List<RoutePoint>();
        int id = 0;
        for (int n = 0; n <= 100; n += 20) points.Add(P(id++, n, 0));
        for (int e = 20; e <= 100; e += 20) points.Add(P(id++, 100, e));
        var route = Assert.Single(RouteBuilder.Build(points.OrderBy(p => (p.Id * 7) % 11), 50));
        Assert.Equal(Enumerable.Range(0, points.Count).ToArray(), Ids(route));
    }

    [Fact]
    public void DistantGroups_BecomeSeparateRoutes_NumberedSouthFirst()
    {
        var points = new[]
        {
            P(0, 0, 0), P(1, 50, 0), P(2, 100, 0),                     // northern trench
            P(3, 0, 0, lat0: 50.4566, lon0: 8.1827), P(4, 40, 0, lat0: 50.4566, lon0: 8.1827) // ~5 km south
        };
        var routes = RouteBuilder.Build(points, 200);
        Assert.Equal(2, routes.Count);
        Assert.Equal([3, 4], Ids(routes[0]));
        Assert.Equal([0, 1, 2], Ids(routes[1]));
        Assert.Equal(1, routes[0].Number);
    }

    [Theory]
    [InlineData(200, 2)]
    [InlineData(400, 1)]
    public void GapThreshold_SplitsOrJoins(double gap, int expectedRoutes)
    {
        var points = new[] { P(0, 0, 0), P(1, 300, 0) };
        Assert.Equal(expectedRoutes, RouteBuilder.Build(points, gap).Count);
    }

    [Fact]
    public void InvalidOrMissingCoordinates_AreIgnored()
    {
        Assert.Empty(RouteBuilder.Build([], 200));
        var route = Assert.Single(RouteBuilder.Build([new RoutePoint(0, double.NaN, 8), new RoutePoint(1, 91, 8), P(2, 0, 0)], 200));
        Assert.Equal([2], Ids(route));
    }

    [Fact]
    public void IdenticalPositions_KeepLoadOrder()
    {
        var route = Assert.Single(RouteBuilder.Build([P(0, 0, 0), P(1, 0, 0), P(2, 50, 0)], 200));
        Assert.Equal([0, 1, 2], Ids(route));
    }

    [Fact]
    public void ImageItem_UndoRestoresPathAndCoordinates()
    {
        var item = new ImageItem { FilePath = @"C:\a.jpg", Latitude = 1, Longitude = 2 };
        Assert.False(item.CanUndo);
        item.PushHistory();
        item.FilePath = @"D:\out\a_copy.jpg"; item.Latitude = 50; item.Longitude = 8;
        Assert.True(item.CanUndo);
        Assert.Equal(@"D:\out\a_copy.jpg", item.Undo());
        Assert.Equal((@"C:\a.jpg", 1.0, 2.0), (item.FilePath, item.Latitude!.Value, item.Longitude!.Value));
        Assert.False(item.CanUndo);
        Assert.Null(item.Undo());
    }
}
