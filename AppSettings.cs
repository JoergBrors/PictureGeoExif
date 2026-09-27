using System;
using System.IO;
using System.Text.Json;

namespace PictureExifclone
{
    public class AppSettings
    {
        public string TileUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        public string TileAttribution { get; set; } = "OpenStreetMap contributors";
        public string TileAttributionUrl { get; set; } = "https://www.openstreetmap.org/copyright";
        public string OutputFolder { get; set; } = string.Empty;
        /// <summary>Photos farther apart than this start a new virtual route (trench).</summary>
        public double RouteMaxGapMeters { get; set; } = 200;
        public bool SortImagesByRoute { get; set; } = true;
        /// <summary>Photos farther than this from the trunk line become branches (house connections); nearer ones count as GPS jitter.</summary>
        public double RouteBranchMinMeters { get; set; } = 10;
        /// <summary>Valhalla-compatible map-matching server. Default: public FOSSGIS demo server (fair use, max. 1 request/s).</summary>
        public string RoadMatchUrl { get; set; } = Services.RoadMatcher.DefaultServer;
        /// <summary>Valhalla costing: pedestrian (paths, sidewalks), bicycle or auto.</summary>
        public string RoadMatchProfile { get; set; } = "pedestrian";
        /// <summary>Photos farther from the nearest way are connected in a straight line instead.</summary>
        public double RoadMatchMaxDeviationMeters { get; set; } = 25;
        /// <summary>Per provider profile; prices are entered and dated by the user, never hard-coded.</summary>
        public Dictionary<string, AiPriceEntry> AiPrices { get; set; } = new();
        public string? AiTemplatePath { get; set; }
        
        /// <summary>Where this instance is persisted; tests use a temporary path so user settings are never touched.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string FilePath { get; init; } = DefaultPath;

        public static readonly string DefaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PictureExifclone",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(DefaultPath))
                {
                    var json = File.ReadAllText(DefaultPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Ignore errors, return default settings
            }
            
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                Services.AtomicFile.Write(FilePath, System.Text.Encoding.UTF8.GetBytes(json));
            }
            catch
            {
                // Ignore save errors
            }
        }
    }

    public sealed class AiPriceEntry
    {
        public decimal Input { get; set; }
        public decimal Cached { get; set; }
        public decimal Output { get; set; }
        public DateTime? VerifiedDate { get; set; }
    }
}
