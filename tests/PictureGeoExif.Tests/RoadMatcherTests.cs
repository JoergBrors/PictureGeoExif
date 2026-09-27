using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using PictureExifclone.Services;

namespace PictureGeoExif.Tests;

/// <summary>Offline tests with a fake HTTP handler – never contacts a real routing server.</summary>
public class RoadMatcherTests
{
    private sealed class FakeServer : HttpMessageHandler
    {
        public readonly Queue<(HttpStatusCode Status, string Body)> Responses = new();
        public readonly List<(Uri Uri, JsonObject Body, string UserAgent)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
            Requests.Add((request.RequestUri!, body, request.Headers.UserAgent.ToString()));
            var (status, text) = Responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly RoutePoint[] Points =
        Enumerable.Range(0, 4).Select(i => new RoutePoint(i, 50.504 + i * 0.0005, 8.194 - i * 0.0003)).ToArray();

    private static string Encode(IEnumerable<(double Lat, double Lon)> points, int precision = 6)
    {
        var sb = new StringBuilder(); long lastLat = 0, lastLon = 0; double f = Math.Pow(10, precision);
        foreach (var (lat, lon) in points)
        {
            long la = (long)Math.Round(lat * f), lo = (long)Math.Round(lon * f);
            Write(la - lastLat); Write(lo - lastLon); lastLat = la; lastLon = lo;
        }
        return sb.ToString();
        void Write(long v) { v = v < 0 ? ~(v << 1) : v << 1; while (v >= 0x20) { sb.Append((char)((0x20 | (v & 0x1f)) + 63)); v >>= 5; } sb.Append((char)(v + 63)); }
    }

    private static string Response(IEnumerable<RoutePoint> pts, params double?[] distances)
    {
        var list = pts.ToList();
        var shape = Encode(list.Select(p => (p.Latitude + 0.00001, p.Longitude)));
        var matched = new JsonArray(distances.Select(d => (JsonNode)(d == null
            ? new JsonObject { ["type"] = "unmatched" }
            : new JsonObject { ["type"] = "matched", ["distance_from_trace_point"] = d })).ToArray());
        return new JsonObject { ["shape"] = shape, ["matched_points"] = matched }.ToJsonString();
    }

    private static (RoadMatcher Matcher, FakeServer Server) Create(double maxDeviation = 25)
    {
        var server = new FakeServer();
        var client = new HttpClient(server);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        return (new RoadMatcher(client, new Uri("https://routing.example/valhalla"), "pedestrian", maxDeviation, TimeSpan.Zero), server);
    }

    [Fact]
    public void DecodePolyline_MatchesGoogleReferenceExample()
    {
        var points = RoadMatcher.DecodePolyline("_p~iF~ps|U_ulLnnqC_mqNvxq`@", 5);
        Assert.Equal([(38.5, -120.2), (40.7, -120.95), (43.252, -126.453)], points);
    }

    [Fact]
    public async Task AllPhotosNearWays_OneRoadSegment_OneRequest()
    {
        var (matcher, server) = Create();
        server.Responses.Enqueue((HttpStatusCode.OK, Response(Points, 3, 4, 15, 2)));
        var result = await matcher.MatchAsync(Points, CancellationToken.None);

        var segment = Assert.Single(result.Segments);
        Assert.True(segment.OnRoad);
        Assert.Equal([3, 4, 15, 2], result.Deviations.Select(d => d!.Value));
        Assert.Null(result.Note);

        var request = Assert.Single(server.Requests);
        Assert.Equal("https://routing.example/valhalla/trace_attributes", request.Uri.AbsoluteUri);
        Assert.Equal("pedestrian", request.Body["costing"]!.GetValue<string>());
        Assert.Equal(4, request.Body["shape"]!.AsArray().Count);
        Assert.StartsWith("PictureGeoExif/", request.UserAgent);
    }

    [Fact]
    public async Task PhotoFarFromWay_IsConnectedStraight_OthersMatchedInRuns()
    {
        var (matcher, server) = Create(maxDeviation: 25);
        server.Responses.Enqueue((HttpStatusCode.OK, Response(Points, 3, 4, 60, 2)));   // point 2 is off the network
        server.Responses.Enqueue((HttpStatusCode.OK, Response(Points[..2], 3, 4)));      // run 0..1
        var result = await matcher.MatchAsync(Points, CancellationToken.None);

        Assert.Equal(2, server.Requests.Count);                 // run [3] has one point → no request
        Assert.True(result.Segments[0].OnRoad);
        Assert.All(result.Segments.Skip(1), s => Assert.False(s.OnRoad));
        Assert.Contains(result.Segments, s => s.Points.Contains((Points[2].Latitude, Points[2].Longitude)));
        Assert.Equal(60, result.Deviations[2]);
        Assert.Contains("1 Foto", result.Note);
    }

    [Fact]
    public async Task NoWayNearby_FallsBackToStraightLine()
    {
        var (matcher, server) = Create();
        server.Responses.Enqueue((HttpStatusCode.BadRequest, """{"error_code":171,"error":"No suitable edges near location"}"""));
        var result = await matcher.MatchAsync(Points, CancellationToken.None);
        var segment = Assert.Single(result.Segments);
        Assert.False(segment.OnRoad);
        Assert.Equal(4, segment.Points.Count);
        Assert.Contains("Kein Weg", result.Note);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ServerProblems_StopWithoutRetry(HttpStatusCode status)
    {
        var (matcher, server) = Create();
        server.Responses.Enqueue((status, "{}"));
        await Assert.ThrowsAsync<RoadMatchException>(() => matcher.MatchAsync(Points, CancellationToken.None));
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task TooManyPoints_NoRequest()
    {
        var (matcher, server) = Create();
        var many = Enumerable.Range(0, RoadMatcher.MaxPointsPerRoute + 1).Select(i => new RoutePoint(i, 50 + i * 1e-4, 8)).ToList();
        var result = await matcher.MatchAsync(many, CancellationToken.None);
        Assert.Empty(server.Requests);
        Assert.False(Assert.Single(result.Segments).OnRoad);
    }

    [Theory]
    [InlineData("https://valhalla1.openstreetmap.de", true)]
    [InlineData("http://localhost:8002", true)]
    [InlineData("http://127.0.0.1:8002/valhalla", true)]
    [InlineData("http://routing.example.com", false)]
    [InlineData("ftp://routing.example.com", false)]
    [InlineData("https://user:pw@routing.example.com", false)]
    [InlineData("kein url", false)]
    public void ServerUrlValidation(string url, bool allowed) => Assert.Equal(allowed, RoadMatcher.IsAllowedServer(url, out _));
}
