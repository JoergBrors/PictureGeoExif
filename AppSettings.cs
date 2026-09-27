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
