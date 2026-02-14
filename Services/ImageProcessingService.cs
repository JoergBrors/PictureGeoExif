using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PictureExifclone.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PictureExifclone.Services
{
    public class ImageProcessingService
    {
        public async Task<bool> ApplyTextAsync(string filePath, ImageDocument document, ToolContext context)
        {
            if (!document.ClickPosition.HasValue)
                return false;

            Debug.WriteLine($"[IMG_PROC] Applying text at ({document.ClickPosition.Value.X:F0}, {document.ClickPosition.Value.Y:F0})");

            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);

                    var family = FindFont("Arial");
                    if (family == null)
                        throw new InvalidOperationException("No system font found");

                    var font = family.Value.CreateFont(context.FontSize);
                    var color = Color.FromRgba(context.Color.R, context.Color.G, context.Color.B, context.Color.A);

                    img.Mutate(x =>
                    {
                        // Shadow for better readability
                        x.DrawText(context.Text ?? "", font,
                            Color.Black.WithAlpha(0.6f),
                            new PointF((float)document.ClickPosition.Value.X + 2, (float)document.ClickPosition.Value.Y + 2));

                        // Main text
                        x.DrawText(context.Text ?? "", font, color,
                            new PointF((float)document.ClickPosition.Value.X, (float)document.ClickPosition.Value.Y));
                    });

                    SaveImage(img, filePath);

                    Debug.WriteLine("[IMG_PROC] Text applied successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error applying text: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> ApplyGeoWatermarkAsync(string filePath, ImageDocument document, ToolContext context)
        {
            if (!document.ClickPosition.HasValue || !document.Latitude.HasValue || !document.Longitude.HasValue)
                return false;

            var geoText = $"Lat: {document.Latitude.Value:F6}\nLon: {document.Longitude.Value:F6}";
            Debug.WriteLine($"[IMG_PROC] Applying geo watermark: {geoText}");

            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);

                    var family = FindFont("Arial");
                    if (family == null)
                        throw new InvalidOperationException("No system font found");

                    var font = family.Value.CreateFont(context.FontSize);
                    var color = Color.FromRgba(context.Color.R, context.Color.G, context.Color.B, context.Color.A);

                    img.Mutate(x =>
                    {
                        x.DrawText(geoText, font,
                            Color.Black.WithAlpha(0.6f),
                            new PointF((float)document.ClickPosition.Value.X + 2, (float)document.ClickPosition.Value.Y + 2));

                        x.DrawText(geoText, font, color,
                            new PointF((float)document.ClickPosition.Value.X, (float)document.ClickPosition.Value.Y));
                    });

                    SaveImage(img, filePath);

                    Debug.WriteLine("[IMG_PROC] Geo watermark applied successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error applying geo watermark: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> ApplyPixelateAsync(string filePath, ImageDocument document, ToolContext context)
        {
            if (!document.SelectionRect.HasValue)
                return false;

            var rect = document.SelectionRect.Value;
            Debug.WriteLine($"[IMG_PROC] Pixelating area: ({rect.X:F0}, {rect.Y:F0}, {rect.Width:F0}x{rect.Height:F0})");

            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);

                    var imgRect = new Rectangle(
                        (int)rect.X,
                        (int)rect.Y,
                        (int)rect.Width,
                        (int)rect.Height);

                    img.Mutate(x => x.Pixelate(context.PixelSize, imgRect));

                    SaveImage(img, filePath);

                    Debug.WriteLine("[IMG_PROC] Pixelate applied successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error applying pixelate: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> ApplyCropAsync(string filePath, ImageDocument document)
        {
            if (!document.SelectionRect.HasValue)
                return false;

            var rect = document.SelectionRect.Value;
            Debug.WriteLine($"[IMG_PROC] Cropping to: ({rect.X:F0}, {rect.Y:F0}, {rect.Width:F0}x{rect.Height:F0})");

            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);

                    var imgRect = new Rectangle(
                        (int)rect.X,
                        (int)rect.Y,
                        (int)rect.Width,
                        (int)rect.Height);

                    img.Mutate(x => x.Crop(imgRect));

                    SaveImage(img, filePath);

                    Debug.WriteLine("[IMG_PROC] Crop applied successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error applying crop: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> ApplyRotateAsync(string filePath, RotateMode rotateMode)
        {
            Debug.WriteLine($"[IMG_PROC] Rotating: {rotateMode}");

            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);
                    img.Mutate(x => x.Rotate(rotateMode));
                    SaveImage(img, filePath);

                    Debug.WriteLine("[IMG_PROC] Rotate applied successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error applying rotate: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<(int width, int height)> GetImageDimensionsAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var img = Image.Load<Rgba32>(filePath);
                    return (img.Width, img.Height);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IMG_PROC] Error getting dimensions: {ex.Message}");
                    return (0, 0);
                }
            });
        }

        private SixLabors.Fonts.FontFamily? FindFont(string preferredName)
        {
            foreach (var family in SixLabors.Fonts.SystemFonts.Collection.Families)
            {
                if (string.Equals(family.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                    return family;
            }

            return SixLabors.Fonts.SystemFonts.Collection.Families.FirstOrDefault();
        }

        private void SaveImage(Image<Rgba32> img, string filePath)
        {
            var encoder = new JpegEncoder { Quality = 95 };
            img.Save(filePath, encoder);
        }
    }
}
