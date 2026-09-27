namespace PictureExifclone.Services;

public sealed record RoutePoint(int Id, double Latitude, double Longitude);

/// <summary>A virtual trench/route: points ordered along its course, running south→north or west→east.</summary>
public sealed record Route(int Number, IReadOnlyList<RoutePoint> Points, bool NorthSouth, double LengthMeters);

/// <summary>
/// Builds virtual routes from photo GPS positions:
/// 1. points closer than <c>maxGapMeters</c> (transitively) form one route,
/// 2. each route is ordered as an open path (nearest neighbour from the southern/western end, then 2-opt),
/// 3. the path runs south→north if the route is mainly north-south, otherwise west→east.
/// Routes themselves are numbered by their start point (south before north, then west before east).
/// </summary>
public static class RouteBuilder
{
    private const double MetersPerDegreeLatitude = 110_574;
    private const double MetersPerDegreeLongitudeAtEquator = 111_320;

    public static IReadOnlyList<Route> Build(IEnumerable<RoutePoint> input, double maxGapMeters)
    {
        var points = input.Where(p => PixelGeometry.ValidGps(p.Latitude, p.Longitude)).ToList();
        if (points.Count == 0) return [];
        if (!double.IsFinite(maxGapMeters) || maxGapMeters <= 0) throw new ArgumentOutOfRangeException(nameof(maxGapMeters));

        // Local equirectangular projection: accurate enough for trenches of a few kilometres.
        double cosLat = Math.Cos(points.Average(p => p.Latitude) * Math.PI / 180);
        var xy = points.Select(p => (X: p.Longitude * MetersPerDegreeLongitudeAtEquator * cosLat, Y: p.Latitude * MetersPerDegreeLatitude)).ToArray();
        double Distance(int a, int b) => Math.Sqrt(Math.Pow(xy[a].X - xy[b].X, 2) + Math.Pow(xy[a].Y - xy[b].Y, 2));

        var clusters = Cluster(points.Count, maxGapMeters, Distance);
        var routes = clusters.Select(indices =>
        {
            double spanX = indices.Max(i => xy[i].X) - indices.Min(i => xy[i].X);
            double spanY = indices.Max(i => xy[i].Y) - indices.Min(i => xy[i].Y);
            bool northSouth = spanY >= spanX;
            double Axis(int i) => northSouth ? xy[i].Y : xy[i].X;

            var path = NearestNeighbourPath(indices, indices.OrderBy(Axis).ThenBy(i => points[i].Id).First(), Distance);
            TwoOpt(path, Distance);
            if (Axis(path[^1]) < Axis(path[0])) path.Reverse();
            double length = path.Zip(path.Skip(1), Distance).Sum();
            return (Points: path.Select(i => points[i]).ToList(), NorthSouth: northSouth, Length: length);
        })
        .OrderBy(r => r.Points[0].Latitude).ThenBy(r => r.Points[0].Longitude)
        .ToList();

        return routes.Select((r, i) => new Route(i + 1, r.Points, r.NorthSouth, r.Length)).ToList();
    }

    /// <summary>Single-linkage clustering (union-find); stable in input order.</summary>
    private static List<List<int>> Cluster(int count, double maxGap, Func<int, int, double> distance)
    {
        var parent = Enumerable.Range(0, count).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
                if (distance(a, b) <= maxGap) parent[Find(b)] = Find(a);
        return Enumerable.Range(0, count).GroupBy(Find).Select(g => g.ToList()).ToList();
    }

    private static List<int> NearestNeighbourPath(List<int> indices, int start, Func<int, int, double> distance)
    {
        var remaining = new List<int>(indices); remaining.Remove(start);
        var path = new List<int> { start };
        while (remaining.Count > 0)
        {
            int last = path[^1];
            int next = remaining.MinBy(i => distance(last, i)); // ties keep input order
            path.Add(next); remaining.Remove(next);
        }
        return path;
    }

    /// <summary>Open-path 2-opt: removes crossings and detours. Bounded passes keep it responsive.</summary>
    private static void TwoOpt(List<int> path, Func<int, int, double> distance)
    {
        if (path.Count < 4) return;
        for (int pass = 0; pass < 50; pass++)
        {
            bool improved = false;
            for (int i = 0; i < path.Count - 2; i++)
            for (int k = i + 1; k < path.Count - 1; k++)
            {
                // Reverse path[i+1..k]: edges (i,i+1),(k,k+1) become (i,k),(i+1,k+1).
                double delta = distance(path[i], path[k]) + distance(path[i + 1], path[k + 1])
                             - distance(path[i], path[i + 1]) - distance(path[k], path[k + 1]);
                if (delta < -1e-6) { path.Reverse(i + 1, k - i); improved = true; }
            }
            // Also allow flipping the head segment (open path: the start may be misplaced).
            for (int k = 1; k < path.Count - 1; k++)
            {
                double delta = distance(path[0], path[k + 1]) - distance(path[k], path[k + 1]);
                if (delta < -1e-6) { path.Reverse(0, k + 1); improved = true; }
            }
            for (int k = 1; k < path.Count - 1; k++)
            {
                double delta = distance(path[k - 1], path[^1]) - distance(path[k - 1], path[k]);
                if (delta < -1e-6) { path.Reverse(k, path.Count - k); improved = true; }
            }
            if (!improved) break;
        }
    }
}
