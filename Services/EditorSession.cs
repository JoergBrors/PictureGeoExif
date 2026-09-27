using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PictureExifclone.Services;

/// <summary>Transactional lossless history. All mutations run on one background worker.</summary>
public sealed class EditorSession : IDisposable
{
    public const long MaximumPixels = 80_000_000;
    private const long HistoryBudget = 1_000_000_000;
    private readonly string folder = Path.Combine(Path.GetTempPath(), "PictureGeoExif", Guid.NewGuid().ToString("N"));
    private readonly List<string> history = [];
    private int position;
    private readonly SemaphoreSlim gate = new(1);
    public string OriginalPath { get; }
    public string CurrentPath => history[position];
    public bool CanUndo => position > 0;
    public bool CanRedo => position < history.Count - 1;
    public bool HasChanges => CurrentPath != initialPath;
    private string initialPath = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string ExportExtension { get; private set; } = ".png";

    private EditorSession(string path) { OriginalPath = path; Directory.CreateDirectory(folder); }

    public static async Task<EditorSession> OpenAsync(string path, CancellationToken token = default)
    {
        var session = new EditorSession(path);
        try
        {
            await Task.Run(() =>
            {
                var info = Image.Identify(path) ?? throw new InvalidDataException("Bildformat nicht erkannt.");
                if ((long)info.Width * info.Height > MaximumPixels) throw new InvalidDataException("Bild überschreitet das Editorlimit von 80 Megapixeln.");
                using var image = Image.Load<Rgba32>(path);
                if (image.Frames.Count != 1) throw new InvalidDataException("Mehrseitige oder animierte Bilder bitte zunächst als Einzelbild exportieren.");
                image.Mutate(x => x.AutoOrient());
                image.Metadata.ExifProfile?.SetValue(ExifTag.Orientation, (ushort)1);
                session.Width = image.Width; session.Height = image.Height;
                session.initialPath = session.NewPath();
                image.Save(session.initialPath, new PngEncoder());
                session.history.Add(session.initialPath);
            }, token);
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    private string NewPath() => Path.Combine(folder, Guid.NewGuid().ToString("N") + ".png");

    public async Task ApplyAsync(Action<Image<Rgba32>> operation, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        string next = NewPath();
        try
        {
            await Task.Run(() =>
            {
                using var image = Image.Load<Rgba32>(CurrentPath);
                operation(image);
                token.ThrowIfCancellationRequested();
                image.Save(next, new PngEncoder());
                token.ThrowIfCancellationRequested();
            }, token);
            // Commit only after successful processing and encoding.
            foreach (var item in history.Skip(position + 1)) File.Delete(item);
            history.RemoveRange(position + 1, history.Count - position - 1);
            history.Add(next); position++;
            while (history.Count > 2 && (history.Count > 31 || history.Sum(p => new FileInfo(p).Length) > HistoryBudget))
            { File.Delete(history[0]); history.RemoveAt(0); position--; }
            RefreshDimensions();
        }
        catch { if (File.Exists(next)) File.Delete(next); throw; }
        finally { gate.Release(); }
    }

    public void Undo() { if (CanUndo) { position--; RefreshDimensions(); } }
    public void Redo() { if (CanRedo) { position++; RefreshDimensions(); } }
    private void RefreshDimensions() { var info = Image.Identify(CurrentPath)!; Width = info.Width; Height = info.Height; }

    public async Task<byte[]> ExportAsync(string extension, int quality, CancellationToken token = default)
    {
        ExportExtension = extension.ToLowerInvariant();
        return await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(CurrentPath);
            IImageEncoder encoder = ExportExtension switch
            {
                ".jpg" or ".jpeg" => new JpegEncoder { Quality = Math.Clamp(quality, 1, 100) },
                ".png" => new PngEncoder(),
                ".bmp" => new BmpEncoder(),
                ".tif" or ".tiff" => new TiffEncoder(),
                _ => throw new InvalidOperationException("Nicht unterstütztes Exportformat.")
            };
            if (ExportExtension is ".jpg" or ".jpeg") image.Mutate(x => x.BackgroundColor(Color.White));
            using var stream = new MemoryStream();
            image.Save(stream, encoder);
            token.ThrowIfCancellationRequested();
            return stream.ToArray();
        }, token);
    }

    public static void Pixelate(Image<Rgba32> image, Rectangle selection, int block)
    {
        if (selection.IsEmpty) throw new ArgumentException("Bitte einen Bereich auswählen.");
        // Direct replacement, not alpha-compositing over the original.
        using var region = image.Clone(c => c.Crop(selection));
        region.Mutate(c => c.Pixelate(Math.Clamp(block, 1, Math.Min(selection.Width, selection.Height))));
        CopyRegion(region, new Rectangle(0, 0, region.Width, region.Height), image, selection.Location);
    }

    public static void Blur(Image<Rgba32> image, Rectangle selection, float sigma)
    {
        if (selection.IsEmpty) throw new ArgumentException("Bitte einen Bereich auswählen.");
        sigma = Math.Clamp(sigma, 0.1f, 100f);
        int margin = (int)Math.Ceiling(sigma * 3);
        var context = Rectangle.Intersect(image.Bounds, new Rectangle(selection.X - margin, selection.Y - margin,
            selection.Width + 2 * margin, selection.Height + 2 * margin));
        using var region = image.Clone(c => c.Crop(context).GaussianBlur(sigma));
        CopyRegion(region, new Rectangle(selection.X - context.X, selection.Y - context.Y, selection.Width, selection.Height), image, selection.Location);
    }

    private static void CopyRegion(Image<Rgba32> source, Rectangle rect, Image<Rgba32> target, Point origin)
    {
        source.ProcessPixelRows(target, (src, dst) =>
        {
            for (int y = 0; y < rect.Height; y++) src.GetRowSpan(rect.Y + y).Slice(rect.X, rect.Width).CopyTo(dst.GetRowSpan(origin.Y + y).Slice(origin.X, rect.Width));
        });
    }

    /// <summary>(x, y) is the top-left corner, or the right/bottom edge when aligned right/bottom.</summary>
    public static void Stamp(Image<Rgba32> image, string text, float size, Color color, double x, double y, bool alignRight = false, bool alignBottom = false)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Bitte Text eingeben.");
        if (!SystemFonts.TryGet("Segoe UI", out var family) && !SystemFonts.TryGet("Arial", out family))
            throw new InvalidOperationException("Segoe UI oder Arial wird zum Stempeln benötigt.");
        // Same pixel engine renders both temporary preview and committed result.
        float shadow = Math.Max(1, size / 24);
        Font font = family.CreateFont(size);
        var bounds = TextMeasurer.MeasureBounds(text, new TextOptions(font));
        float fit = Math.Min(1, Math.Min((image.Width - 2f) / (bounds.Width + shadow), (image.Height - 2f) / (bounds.Height + shadow)));
        if (fit <= 0 || size * fit < 4) throw new InvalidOperationException("Das Bild ist für einen lesbaren Stempel zu klein.");
        if (fit < 1) { font = family.CreateFont(size * fit); shadow *= fit; bounds = TextMeasurer.MeasureBounds(text, new TextOptions(font)); }
        if (alignRight) x -= bounds.Width + shadow;
        if (alignBottom) y -= bounds.Height + shadow;
        float left = (float)Math.Clamp(x, 0, Math.Max(0, image.Width - bounds.Width - shadow - 1));
        float top = (float)Math.Clamp(y, 0, Math.Max(0, image.Height - bounds.Height - shadow - 1));
        var origin = new PointF(left - bounds.X, top - bounds.Y);
        image.Mutate(c => c.DrawText(text, font, Color.Black.WithAlpha(0.7f), new PointF(origin.X + shadow, origin.Y + shadow))
            .DrawText(text, font, color, origin));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { }
        gate.Dispose();
    }
}
