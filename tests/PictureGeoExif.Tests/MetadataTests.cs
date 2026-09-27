using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PictureExifclone.Models;
using PictureExifclone.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace PictureGeoExif.Tests;

public sealed class MetadataTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "pge-tests-" + Guid.NewGuid().ToString("N"));
    public MetadataTests() => System.IO.Directory.CreateDirectory(folder);
    public void Dispose() => System.IO.Directory.Delete(folder, true);

    private string Jpeg(bool withExif = true, int w = 64, int h = 48)
    {
        using var image = new Image<Rgba32>(w, h, new Rgba32(10, 120, 200));
        if (withExif)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "Testautor");
            image.Metadata.ExifProfile.SetValue(ExifTag.DateTimeOriginal, "2024:01:15 10:20:30");
        }
        string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jpg");
        image.SaveAsJpeg(path, new JpegEncoder { Quality = 90 });
        return path;
    }

    private static (double Lat, double Lon)? Gps(string path)
    {
        var gps = ImageMetadataReader.ReadMetadata(path).OfType<GpsDirectory>().FirstOrDefault();
        return gps != null && gps.TryGetGeoLocation(out var l) ? (l.Latitude, l.Longitude) : null;
    }

    /// <summary>Compressed scan data from SOS to EOI must be byte-identical: no re-encoding.</summary>
    private static byte[] ScanData(byte[] jpeg)
    {
        for (int i = 2; i < jpeg.Length - 1; i++)
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDA) return jpeg[i..];
        throw new InvalidDataException("no SOS");
    }

    [Theory]
    [InlineData(true, 52.520008, 13.404954)]
    [InlineData(true, -33.868820, -151.209296)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 90, -180)]
    public void WriteGps_Jpeg_IsLosslessAndVerified(bool withExif, double lat, double lon)
    {
        string path = Jpeg(withExif);
        byte[] before = File.ReadAllBytes(path);
        using (var service = new ImageService()) service.WriteGpsToImage(path, lat, lon);
        byte[] after = File.ReadAllBytes(path);

        Assert.Equal(ScanData(before), ScanData(after));
        var gps = Gps(path);
        Assert.NotNull(gps);
        Assert.Equal(lat, gps.Value.Lat, 5);
        Assert.Equal(lon, gps.Value.Lon, 5);
        if (withExif)
            Assert.Contains(ImageMetadataReader.ReadMetadata(path).OfType<ExifIfd0Directory>().Single().Tags, t => t.Description == "Testautor");
    }

    [Fact]
    public void WriteGps_Twice_IsIdempotent()
    {
        string path = Jpeg();
        using var service = new ImageService();
        service.WriteGpsToImage(path, 48.1, 11.5);
        byte[] first = File.ReadAllBytes(path);
        service.WriteGpsToImage(path, 48.1, 11.5);
        Assert.Equal(first, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(91, 0)]
    [InlineData(0, 180.5)]
    public void WriteGps_InvalidCoordinates_LeaveFileUntouched(double lat, double lon)
    {
        string path = Jpeg();
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using var service = new ImageService();
        Assert.ThrowsAny<ArgumentException>(() => service.WriteGpsToImage(path, lat, lon));
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    [Fact]
    public void WriteGps_Png_KeepsPixelsAndAlpha()
    {
        string path = Path.Combine(folder, "alpha.png");
        using (var image = new Image<Rgba32>(5, 3, new Rgba32(1, 2, 3, 40))) image.SaveAsPng(path);
        using (var service = new ImageService()) service.WriteGpsToImage(path, -1.5, 2.25);
        using var reloaded = Image.Load<Rgba32>(path);
        Assert.Equal(new Rgba32(1, 2, 3, 40), reloaded[4, 2]);
        Assert.Equal(-1.5, Gps(path)!.Value.Lat, 5);
    }

    [Fact]
    public void WriteGps_Bmp_IsRefused()
    {
        string path = Path.Combine(folder, "x.bmp");
        using (var image = new Image<Rgba32>(4, 4)) image.SaveAsBmp(path);
        using var service = new ImageService();
        Assert.Throws<InvalidOperationException>(() => service.WriteGpsToImage(path, 1, 1));
    }

    [Fact]
    public void SaveSingleImage_NeverOverwrites_AndKeepsOriginal()
    {
        string source = Jpeg();
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        using var service = new ImageService();
        string output = Path.Combine(folder, "out");
        string a = service.SaveSingleImage(source, output, 1, 2);
        string b = service.SaveSingleImage(source, output, 1, 2);
        Assert.NotEqual(a, b);
        Assert.True(File.Exists(a) && File.Exists(b));
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
    }

    [Fact]
    public void JpegExifWriter_RejectsCorruptInput()
    {
        Assert.Throws<InvalidDataException>(() => JpegExifWriter.ReplaceExif([0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF], [1, 2]));
        Assert.Throws<InvalidDataException>(() => JpegExifWriter.ReplaceExif([0x89, 0x50, 0x4E, 0x47], [1, 2]));
    }

    [Fact]
    public void Sidecar_MergesKeywords_PreservesExisting_AndRefusesChangedImage()
    {
        string path = Jpeg();
        var row = new AiImageRow { FilePath = path, Revision = AiMetadataService.FileRevision(path) };
        var local = AiMetadataService.Read(row);
        Assert.Equal("2024-01-15", local.CaptureDate);
        var mappings = AiMetadataService.LocalMappings(local).ToList();
        Assert.Contains(mappings, m => m.Target == "XMP.dc:creator" && m.Proposed == "Testautor");

        AiMetadataService.WriteSidecar(row, [
            new AiFieldChange { Field = "J", Target = "XMP.pge:Season", Kind = XmpValueKind.Text, Proposed = "Winter", Source = "t" },
            new AiFieldChange { Field = "S", Target = "XMP.dc:subject", Kind = XmpValueKind.Bag, Proposed = "Schnee; Weg", Source = "t" },
            .. mappings]);
        AiMetadataService.WriteSidecar(row, [new AiFieldChange { Field = "S", Target = "XMP.dc:subject", Kind = XmpValueKind.Bag, Proposed = "weg; Bäume", Source = "t" }]);

        var again = new AiImageRow { FilePath = path };
        var reread = AiMetadataService.Read(again);
        Assert.Equal("Winter", again.ExistingSeason);
        Assert.Equal(["Schnee", "Weg", "Bäume"], again.ExistingKeywords);
        Assert.Empty(AiMetadataService.LocalMappings(reread)); // fillMissing: nothing left to map

        File.AppendAllText(path, "x");
        Assert.Throws<InvalidOperationException>(() => AiMetadataService.WriteSidecar(row, [new AiFieldChange { Field = "J", Target = "XMP.pge:Season", Proposed = "Sommer", Source = "t" }]));
    }

    [Fact]
    public void Sidecar_RejectsUnregisteredTarget()
    {
        string path = Jpeg();
        var row = new AiImageRow { FilePath = path };
        Assert.Throws<InvalidOperationException>(() =>
            AiMetadataService.WriteSidecar(row, [new AiFieldChange { Field = "x", Target = "XMP.CipaExif31LearningPreferences", Proposed = "optIn", Source = "t" }]));
        Assert.False(File.Exists(AiMetadataService.SidecarPath(path)));
    }

    [Fact]
    public void Preview_IsSmallOrientedAndMetadataFree()
    {
        string path = Jpeg(true, 3000, 1000);
        var template = AiTemplate.Parse(File.ReadAllText(TemplatePath));
        byte[] preview = AiMetadataService.Preview(path, template);
        var info = Image.Identify(preview);
        Assert.Equal(1024, info.Width);
        Assert.Null(info.Metadata.ExifProfile);
        Assert.Empty(ImageMetadataReader.ReadMetadata(new MemoryStream(preview)).OfType<ExifIfd0Directory>());
    }

    [Theory]
    [InlineData(52.5, 'N', 'S', "52,30.000000N")]
    [InlineData(-0.5, 'N', 'S', "0,30.000000S")]
    [InlineData(179.9999999999, 'E', 'W', "180,0.000000E")]
    public void XmpCoordinate_Format(double value, char pos, char neg, string expected) =>
        Assert.Equal(expected, AiMetadataService.XmpCoordinate(value, pos, neg));

    internal static string TemplatePath => Path.Combine(AppContext.BaseDirectory, "Templates", "ai-metadata.template.json");
}
