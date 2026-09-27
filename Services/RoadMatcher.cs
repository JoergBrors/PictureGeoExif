using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace PictureExifclone.Services;

/// <summary>A piece of a road-matched route: along roads/paths, or a straight gap where a photo is off the network.</summary>
public sealed record RoadSegment(bool OnRoad, IReadOnlyList<(double Lat, double Lon)> Points);

/// <summary>Result for one route. <see cref="Deviations"/> is aligned with the route's points (metres to the matched way, null = unmatched).</summary>
public sealed record RoadMatch(IReadOnlyList<RoadSegment> Segments, IReadOnlyList<double?> Deviations, string? Note);

public sealed class RoadMatchException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Map-matches a virtual route onto the OSM road/path network with a Valhalla-compatible server
/// (<c>POST {server}/trace_attributes</c>). Photo coordinates are never changed; this only produces display geometry.
/// Respects the FOSSGIS usage policy: identifying User-Agent, one connection, at most one request per second.
/// </summary>
public sealed class RoadMatcher(HttpClient http, Uri server, string costing, double maxDeviationMeters, TimeSpan? minInterval = null)
{
    public const string DefaultServer = "https://valhalla1.openstreetmap.de";
    public const int MaxPointsPerRoute = 250;
    private const int MaxRequestsPerRoute = 12;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime lastRequest = DateTime.MinValue;
    private readonly TimeSpan interval = minInterval ?? TimeSpan.FromMilliseconds(1100);

    public static readonly HttpClient SharedClient = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        return client;
    }

    /// <summary>Accepts HTTPS, and plain HTTP only for a server on this machine (e.g. a local Valhalla container).</summary>
    public static bool IsAllowedServer(string url, out Uri uri) =>
        Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out uri!) &&
        (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.UserInfo);

    public async Task<RoadMatch> MatchAsync(IReadOnlyList<RoutePoint> points, CancellationToken token)
    {
        if (points.Count < 2) return new([], points.Select(_ => (double?)null).ToList(), null);
        if (points.Count > MaxPointsPerRoute)
            return Straight(points, $"Mehr als {MaxPointsPerRoute} Punkte – nicht an Wege angelegt (Server schonen).");

        Trace first;
        try { first = await TraceAsync(points, token); }
        catch (RoadMatchException ex) when (ex.Message.StartsWith("Kein Weg", StringComparison.Ordinal))
        { return Straight(points, ex.Message); }

        var deviations = first.Distances;
        bool[] onRoad = deviations.Select(d => d is { } m && m <= maxDeviationMeters).ToArray();
        if (onRoad.All(x => x)) return new([new RoadSegment(true, first.Shape)], deviations, null);

        // Some photos are off the network: match only runs of on-road photos, connect the rest in straight lines.
        var segments = new List<RoadSegment>();
        int requests = 1;
        string? note = $"{onRoad.Count(x => !x)} Foto(s) weiter als {maxDeviationMeters:0} m vom Weg – dort gerade verbunden.";
        int i = 0;
        while (i < points.Count)
        {
            int start = i;
            if (onRoad[i])
            {
                while (i + 1 < points.Count && onRoad[i + 1]) i++;
                var run = points.Skip(start).Take(i - start + 1).ToList();
                if (run.Count >= 2 && requests < MaxRequestsPerRoute)
                {
                    try { AddConnected(segments, true, (await TraceAsync(run, token)).Shape); requests++; }
                    catch (RoadMatchException ex) when (ex.Message.StartsWith("Kein Weg", StringComparison.Ordinal))
                    { AddConnected(segments, false, run.Select(p => (p.Latitude, p.Longitude)).ToList()); }
                }
                else AddConnected(segments, false, run.Select(p => (p.Latitude, p.Longitude)).ToList());
            }
            else AddConnected(segments, false, [(points[i].Latitude, points[i].Longitude)]);
            i++;
        }
        if (requests >= MaxRequestsPerRoute) note += " Anfragelimit je Trasse erreicht.";
        return new(segments, deviations, note);
    }

    /// <summary>Appends a piece and bridges the gap to the previous piece with a straight connector.</summary>
    private static void AddConnected(List<RoadSegment> segments, bool onRoad, IReadOnlyList<(double Lat, double Lon)> points)
    {
        if (segments.Count > 0)
        {
            var last = segments[^1].Points[^1];
            if (last != points[0]) segments.Add(new RoadSegment(false, [last, points[0]]));
        }
        if (!onRoad && segments.Count > 0 && !segments[^1].OnRoad)
        {
            // Merge consecutive straight pieces into one polyline.
            var merged = segments[^1].Points.Concat(points.SkipWhile(p => p == segments[^1].Points[^1])).ToList();
            segments[^1] = new RoadSegment(false, merged);
        }
        else if (points.Count >= 2 || !onRoad) segments.Add(new RoadSegment(onRoad, points));
    }

    private static RoadMatch Straight(IReadOnlyList<RoutePoint> points, string note) =>
        new([new RoadSegment(false, points.Select(p => (p.Latitude, p.Longitude)).ToList())], points.Select(_ => (double?)null).ToList(), note);

    private sealed record Trace(IReadOnlyList<(double Lat, double Lon)> Shape, IReadOnlyList<double?> Distances);

    private async Task<Trace> TraceAsync(IReadOnlyList<RoutePoint> points, CancellationToken token)
    {
        var body = new JsonObject
        {
            ["shape"] = new JsonArray(points.Select(p => (JsonNode)new JsonObject { ["lat"] = p.Latitude, ["lon"] = p.Longitude }).ToArray()),
            ["costing"] = costing,
            ["shape_match"] = "map_snap",
            ["trace_options"] = new JsonObject { ["search_radius"] = Math.Clamp(maxDeviationMeters, 5, 200) },
            ["filters"] = new JsonObject
            {
                ["attributes"] = new JsonArray("shape", "matched.type", "matched.distance_from_trace_point"),
                ["action"] = "include"
            }
        };

        string text;
        await Gate.WaitAsync(token);
        try
        {
            var wait = lastRequest + interval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(server.AbsoluteUri.TrimEnd('/') + "/trace_attributes"))
            { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            try
            {
                using var response = await http.SendAsync(request, token);
                text = await response.Content.ReadAsStringAsync(token);
                if (!response.IsSuccessStatusCode)
                {
                    string error = TryError(text);
                    // Valhalla 4xx with an error_code means "no usable way nearby" – not a transport failure.
                    if ((int)response.StatusCode is >= 400 and < 500 && (int)response.StatusCode != 429 && error.Length > 0)
                        throw new RoadMatchException("Kein Weg in der Nähe gefunden: " + error);
                    throw new RoadMatchException($"Routing-Server antwortet mit HTTP {(int)response.StatusCode}{(error.Length > 0 ? ": " + error : "")}");
                }
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !token.IsCancellationRequested)
            { throw new RoadMatchException("Routing-Server nicht erreichbar: " + ex.Message, ex); }
        }
        finally { lastRequest = DateTime.UtcNow; Gate.Release(); }

        var root = JsonNode.Parse(text)?.AsObject() ?? throw new RoadMatchException("Leere Antwort vom Routing-Server.");
        string shape = root["shape"]?.GetValue<string>() ?? throw new RoadMatchException("Antwort ohne Geometrie.");
        var matched = root["matched_points"]?.AsArray() ?? throw new RoadMatchException("Antwort ohne matched_points.");
        if (matched.Count != points.Count) throw new RoadMatchException("Antwort passt nicht zu den gesendeten Punkten.");
        var distances = matched.Select(m => m?["type"]?.GetValue<string>() == "unmatched" ? null
            : m?["distance_from_trace_point"]?.GetValue<double>()).ToList();
        var line = DecodePolyline(shape, 6);
        return line.Count < 2 ? throw new RoadMatchException("Kein Weg in der Nähe gefunden.") : new Trace(line, distances);
    }

    private static string TryError(string text)
    {
        try { return JsonNode.Parse(text)?["error"]?.GetValue<string>() ?? ""; }
        catch (System.Text.Json.JsonException) { return ""; }
    }

    /// <summary>Google encoded polyline algorithm; Valhalla uses precision 6.</summary>
    public static List<(double Lat, double Lon)> DecodePolyline(string encoded, int precision)
    {
        var result = new List<(double, double)>();
        double factor = Math.Pow(10, precision);
        int index = 0, lat = 0, lon = 0;
        while (index < encoded.Length)
        {
            lat += Next(); lon += Next();
            result.Add((lat / factor, lon / factor));
        }
        return result;

        int Next()
        {
            int shift = 0, value = 0, b;
            do
            {
                if (index >= encoded.Length) throw new RoadMatchException("Ungültige Geometrie vom Routing-Server.");
                b = encoded[index++] - 63;
                value |= (b & 0x1F) << shift;
                shift += 5;
            } while (b >= 0x20);
            return (value & 1) != 0 ? ~(value >> 1) : value >> 1;
        }
    }
}
