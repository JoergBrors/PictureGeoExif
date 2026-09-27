using SixLabors.ImageSharp;

namespace PictureExifclone.Services;

/// <summary>Exclusive right/bottom edges; independent of WPF, zoom and monitor DPI.</summary>
public static class PixelGeometry
{
    public static Rectangle Selection(double x1, double y1, double x2, double y2, int width, int height)
    {
        if (width <= 0 || height <= 0 || !double.IsFinite(x1) || !double.IsFinite(x2) ||
            !double.IsFinite(y1) || !double.IsFinite(y2)) return Rectangle.Empty;
        if (x1 == x2 || y1 == y2) return Rectangle.Empty;
        int left = (int)Math.Clamp(Math.Floor(Math.Min(x1, x2)), 0, width);
        int top = (int)Math.Clamp(Math.Floor(Math.Min(y1, y2)), 0, height);
        int right = (int)Math.Clamp(Math.Ceiling(Math.Max(x1, x2)), 0, width);
        int bottom = (int)Math.Clamp(Math.Ceiling(Math.Max(y1, y2)), 0, height);
        return right > left && bottom > top ? new(left, top, right - left, bottom - top) : Rectangle.Empty;
    }

    public static double Fit(int width, int height, double viewportWidth, double viewportHeight) =>
        Math.Max(0.00001, Math.Min(Math.Max(1, viewportWidth - 32) / Math.Max(1, width),
                                 Math.Max(1, viewportHeight - 32) / Math.Max(1, height)));

    public static bool ValidGps(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180;
}
