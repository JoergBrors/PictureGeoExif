namespace PictureExifclone.Services;

public sealed record RoutePoint(int Id, double Latitude, double Longitude);

/// <summary>
/// A side branch (e.g. a house connection). It leaves the trunk at <see cref="AttachLatitude"/>/<see cref="AttachLongitude"/>,
/// the foot of the perpendicular from the branch onto the trunk line, i.e. the direct way to the trunk.
/// </summary>
public sealed record RouteBranch(IReadOnlyList<RoutePoint> Points, double AttachLatitude, double AttachLongitude, double AttachMeters);

/// <summary>
/// A virtual trench/route. <see cref="Trunk"/> runs south→north (or west→east); <see cref="Branches"/> hang off it.
/// <see cref="Points"/> is the combined order used for the image list: trunk photos along the run, each branch's photos
/// directly after the trunk position where the branch leaves.
/// </summary>
public sealed record Route(int Number, IReadOnlyList<RoutePoint> Points, IReadOnlyList<RoutePoint> Trunk,
    IReadOnlyList<RouteBranch> Branches, bool NorthSouth, double LengthMeters)
{
    public bool IsBranchPoint(RoutePoint point) => Branches.Any(b => b.Points.Contains(point));
}

/// <summary>
/// Builds virtual routes from photo GPS positions:
/// 1. points closer than <c>maxGapMeters</c> (transitively) form one route;
/// 2. a minimum spanning tree connects them; the trunk is the tree path between the two leaves farthest apart;
/// 3. photos farther than <c>branchMinMeters</c> from the trunk line become branches attached perpendicularly,
///    nearer ones (GPS jitter) are treated as trunk photos;
/// 4. the trunk runs south→north if the route is mainly north-south, otherwise west→east.
/// Routes are numbered by their start point (south before north, then west before east).
/// </summary>
public static class RouteBuilder
{
    private const double MetersPerDegreeLatitude = 110_574;
    private const double MetersPerDegreeLongitudeAtEquator = 111_320;

    public static IReadOnlyList<Route> Build(IEnumerable<RoutePoint> input, double maxGapMeters, double branchMinMeters = 10)
    {
        var points = input.Where(p => PixelGeometry.ValidGps(p.Latitude, p.Longitude)).ToList();
        if (points.Count == 0) return [];
        if (!double.IsFinite(maxGapMeters) || maxGapMeters <= 0) throw new ArgumentOutOfRangeException(nameof(maxGapMeters));
        if (!double.IsFinite(branchMinMeters) || branchMinMeters < 0) throw new ArgumentOutOfRangeException(nameof(branchMinMeters));

        // Local equirectangular projection: accurate enough for trenches of a few kilometres.
        double lat0 = points.Average(p => p.Latitude);
        double kx = MetersPerDegreeLongitudeAtEquator * Math.Cos(lat0 * Math.PI / 180);
        var xy = points.Select(p => new Xy(p.Longitude * kx, p.Latitude * MetersPerDegreeLatitude)).ToArray();
        double Distance(int a, int b) => (xy[a] - xy[b]).Length;

        var routes = Cluster(points.Count, maxGapMeters, Distance)
            .Select(indices => BuildOne(indices, points, xy, Distance, branchMinMeters, maxGapMeters, kx))
            .OrderBy(r => r.Trunk[0].Latitude).ThenBy(r => r.Trunk[0].Longitude)
            .ToList();
        return routes.Select((r, i) => r with { Number = i + 1 }).ToList();
    }

    private readonly record struct Xy(double X, double Y)
    {
        public static Xy operator -(Xy a, Xy b) => new(a.X - b.X, a.Y - b.Y);
        public static Xy operator +(Xy a, Xy b) => new(a.X + b.X, a.Y + b.Y);
        public static Xy operator *(Xy a, double f) => new(a.X * f, a.Y * f);
        public double Length => Math.Sqrt(X * X + Y * Y);
        public double Dot(Xy o) => X * o.X + Y * o.Y;
    }

    private static Route BuildOne(List<int> indices, List<RoutePoint> points, Xy[] xy, Func<int, int, double> distance,
        double branchMin, double maxGap, double kx)
    {
        if (indices.Count == 1)
            return new Route(0, [points[indices[0]]], [points[indices[0]]], [], true, 0);

        var adjacency = MinimumSpanningTree(indices, distance);
        var trunk = TrunkPath(indices, adjacency, points, distance);

        double spanX = trunk.Max(i => xy[i].X) - trunk.Min(i => xy[i].X);
        double spanY = trunk.Max(i => xy[i].Y) - trunk.Min(i => xy[i].Y);
        bool northSouth = spanY >= spanX;
        double Axis(int i) => northSouth ? xy[i].Y : xy[i].X;
        if (Axis(trunk[^1]) < Axis(trunk[0]) || (Axis(trunk[^1]) == Axis(trunk[0]) && points[trunk[^1]].Id < points[trunk[0]].Id)) trunk.Reverse();

        RemoveSpikes(trunk, xy, branchMin, maxGap);

        var line = trunk.Select(i => xy[i]).ToList();
        var onTrunk = trunk.ToHashSet();
        var projection = indices.ToDictionary(i => i, i => Project(line, xy[i]));

        // Trunk members: the longest path plus photos within branchMin of it (GPS jitter), ordered along the line.
        var branchCandidates = indices.Where(i => !onTrunk.Contains(i) && projection[i].Distance > branchMin).ToHashSet();
        var trunkMembers = indices.Where(i => !branchCandidates.Contains(i))
            .OrderBy(i => projection[i].Along).ThenBy(i => points[i].Id).ToList();

        // Branches: connected groups of branch candidates in the spanning tree.
        var branches = new List<(List<int> Members, Projection Attach)>();
        var seen = new HashSet<int>();
        foreach (int start in branchCandidates.OrderBy(i => points[i].Id))
        {
            if (!seen.Add(start)) continue;
            var group = new List<int> { start };
            for (int q = 0; q < group.Count; q++)
                foreach (int next in adjacency[group[q]])
                    if (branchCandidates.Contains(next) && seen.Add(next)) group.Add(next);
            int root = group.MinBy(i => (projection[i].Distance, points[i].Id));
            var attach = projection[root];
            var ordered = group.OrderBy(i => (xy[i] - attach.Foot).Length).ThenBy(i => points[i].Id).ToList();
            branches.Add((ordered, attach));
        }
        branches = branches.OrderBy(b => b.Attach.Along).ThenBy(b => points[b.Members[0]].Id).ToList();

        // Combined order: at equal positions the trunk photo comes before the branch leaving there.
        var combined = trunkMembers.Select(i => (Key: projection[i].Along, Kind: 0, Rank: 0, Index: i))
            .Concat(branches.SelectMany((b, bi) => b.Members.Select((i, rank) => (Key: b.Attach.Along, Kind: 1 + bi, Rank: rank, Index: i))))
            .OrderBy(x => x.Key).ThenBy(x => x.Kind).ThenBy(x => x.Rank)
            .Select(x => points[x.Index]).ToList();

        double length = trunkMembers.Zip(trunkMembers.Skip(1), distance).Sum() +
                        branches.Sum(b => (xy[b.Members[0]] - b.Attach.Foot).Length + b.Members.Zip(b.Members.Skip(1), distance).Sum());

        var routeBranches = branches.Select(b => new RouteBranch(b.Members.Select(i => points[i]).ToList(),
            b.Attach.Foot.Y / MetersPerDegreeLatitude, b.Attach.Foot.X / kx, b.Attach.Along)).ToList();
        return new Route(0, combined, trunkMembers.Select(i => points[i]).ToList(), routeBranches, northSouth, length);
    }

    /// <summary>
    /// The spanning tree may route the trunk through a side photo when trunk photos are sparse (a "spike").
    /// Locally a spike looks like a 90° bend; the difference is the course around it: if the trunk keeps its heading
    /// before and after (within 30° of the shortcut), the photo is a branch and is taken out of the trunk.
    /// </summary>
    private static void RemoveSpikes(List<int> trunk, Xy[] xy, double branchMin, double maxGap)
    {
        static double Heading(Xy a, Xy b) => Math.Atan2(b.Y - a.Y, b.X - a.X);
        static double Turn(double h1, double h2) { double d = Math.Abs(h1 - h2) % (2 * Math.PI); return d > Math.PI ? 2 * Math.PI - d : d; }
        const double Straight = 30 * Math.PI / 180;
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int k = 1; k < trunk.Count - 1; k++)
            {
                Xy prev = xy[trunk[k - 1]], spike = xy[trunk[k]], next = xy[trunk[k + 1]];
                var chord = next - prev; double chordLength = chord.Length;
                if (chordLength == 0 || chordLength > maxGap) continue;
                double t = Math.Clamp((spike - prev).Dot(chord) / (chordLength * chordLength), 0, 1);
                if ((spike - (prev + chord * t)).Length <= branchMin) continue;

                double shortcut = Heading(prev, next);
                bool before = k >= 2, after = k + 2 < trunk.Count;
                // Neighbouring photos may be spikes themselves (zig-zag over branches on both sides),
                // so each side also looks one photo further.
                bool BeforeAgrees() => Turn(Heading(xy[trunk[k - 2]], prev), shortcut) <= Straight ||
                                       (k >= 3 && Turn(Heading(xy[trunk[k - 3]], prev), shortcut) <= Straight);
                bool AfterAgrees() => Turn(Heading(next, xy[trunk[k + 2]]), shortcut) <= Straight ||
                                      (k + 3 < trunk.Count && Turn(Heading(next, xy[trunk[k + 3]]), shortcut) <= Straight);
                bool keepsHeading = (!before || BeforeAgrees()) && (!after || AfterAgrees());
                // Without context on either side only an out-and-back (acute angle at the photo) counts as a spike.
                bool acute = (prev - spike).Dot(next - spike) > 0.5 * (prev - spike).Length * (next - spike).Length;
                if ((before || after) ? keepsHeading : acute) { trunk.RemoveAt(k); changed = true; break; }
            }
        }
    }

    private readonly record struct Projection(double Along, double Distance, Xy Foot);

    /// <summary>Nearest point on the polyline: arc length from its start, perpendicular distance and foot point.</summary>
    private static Projection Project(List<Xy> line, Xy p)
    {
        var best = new Projection(0, (p - line[0]).Length, line[0]);
        double walked = 0;
        for (int s = 0; s + 1 < line.Count; s++)
        {
            var a = line[s]; var d = line[s + 1] - a; double len = d.Length;
            double t = len == 0 ? 0 : Math.Clamp((p - a).Dot(d) / (len * len), 0, 1);
            var foot = a + d * t;
            double dist = (p - foot).Length;
            if (dist < best.Distance - 1e-9) best = new Projection(walked + t * len, dist, foot);
            walked += len;
        }
        return best;
    }

    /// <summary>Prim's algorithm, O(n²); deterministic tie-breaking by input order.</summary>
    private static Dictionary<int, List<int>> MinimumSpanningTree(List<int> indices, Func<int, int, double> distance)
    {
        var adjacency = indices.ToDictionary(i => i, _ => new List<int>());
        var inTree = new HashSet<int> { indices[0] };
        var best = indices.Skip(1).ToDictionary(i => i, i => (Cost: distance(indices[0], i), From: indices[0]));
        while (best.Count > 0)
        {
            var next = best.MinBy(kv => kv.Value.Cost);
            best.Remove(next.Key);
            inTree.Add(next.Key);
            adjacency[next.Key].Add(next.Value.From); adjacency[next.Value.From].Add(next.Key);
            foreach (var key in best.Keys.ToList())
            {
                double d = distance(next.Key, key);
                if (d < best[key].Cost) best[key] = (d, next.Key);
            }
        }
        return adjacency;
    }

    /// <summary>
    /// Trunk = tree path between the two leaves that are farthest apart as the crow flies. Unlike the tree diameter
    /// (longest path), this does not run into a house connection near the end just because the connection is longer
    /// than the remaining trunk: trench ends are far apart, connections are short and transverse.
    /// </summary>
    private static List<int> TrunkPath(List<int> indices, Dictionary<int, List<int>> adjacency, List<RoutePoint> points, Func<int, int, double> distance)
    {
        var leaves = indices.Where(i => adjacency[i].Count <= 1).OrderBy(i => points[i].Id).ToList();
        (int A, int B) best = (leaves[0], leaves[^1]);
        double bestDistance = -1;
        for (int x = 0; x < leaves.Count; x++)
            for (int y = x + 1; y < leaves.Count; y++)
            {
                double d = distance(leaves[x], leaves[y]);
                if (d > bestDistance + 1e-9) { bestDistance = d; best = (leaves[x], leaves[y]); }
            }

        var parent = new Dictionary<int, int?> { [best.A] = null };
        var stack = new Stack<int>(); stack.Push(best.A);
        while (stack.Count > 0)
        {
            int u = stack.Pop();
            foreach (int v in adjacency[u])
                if (!parent.ContainsKey(v)) { parent[v] = u; stack.Push(v); }
        }
        var path = new List<int>();
        for (int? node = best.B; node != null; node = parent[node.Value]) path.Add(node.Value);
        return path;
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
}
