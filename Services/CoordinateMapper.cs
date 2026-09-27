using System;
using System.Windows;
using System.Windows.Media;

namespace PictureExifclone.Services
{
    /// <summary>
    /// Zentrale Klasse für Koordinaten-Transformation zwischen View und Pixel-Space
    /// </summary>
    public class CoordinateMapper
    {
        private readonly Func<Size> getActualImageSize;
        private readonly Func<Size> getImagePixelSize;
        private readonly Func<Stretch> getStretch;

        public CoordinateMapper(
            Func<Size> getActualImageSize,
            Func<Size> getImagePixelSize,
            Func<Stretch> getStretch)
        {
            this.getActualImageSize = getActualImageSize ?? throw new ArgumentNullException(nameof(getActualImageSize));
            this.getImagePixelSize = getImagePixelSize ?? throw new ArgumentNullException(nameof(getImagePixelSize));
            this.getStretch = getStretch ?? throw new ArgumentNullException(nameof(getStretch));
        }

        public Rect GetRenderedImageRect()
        {
            var actualSize = getActualImageSize();
            var pixelSize = getImagePixelSize();
            var stretch = getStretch();

            double controlW = Math.Max(1, actualSize.Width);
            double controlH = Math.Max(1, actualSize.Height);
            double imgW = pixelSize.Width;
            double imgH = pixelSize.Height;

            if (imgW <= 0 || imgH <= 0)
                return Rect.Empty;

            double scaleX = 1.0;
            double scaleY = 1.0;

            switch (stretch)
            {
                case Stretch.Fill:
                    scaleX = controlW / imgW;
                    scaleY = controlH / imgH;
                    break;

                case Stretch.Uniform:
                    {
                        var s = Math.Min(controlW / imgW, controlH / imgH);
                        scaleX = s;
                        scaleY = s;
                        break;
                    }

                case Stretch.UniformToFill:
                    {
                        var s = Math.Max(controlW / imgW, controlH / imgH);
                        scaleX = s;
                        scaleY = s;
                        break;
                    }

                case Stretch.None:
                default:
                    scaleX = 1.0;
                    scaleY = 1.0;
                    break;
            }

            double renderedW = imgW * scaleX;
            double renderedH = imgH * scaleY;

            double offsetX = (controlW - renderedW) / 2.0;
            double offsetY = (controlH - renderedH) / 2.0;

            return new Rect(offsetX, offsetY, renderedW, renderedH);
        }

        public bool TryViewToPixel(Point viewPoint, out Point pixelPoint)
        {
            pixelPoint = new Point();

            var rect = GetRenderedImageRect();
            var pixelSize = getImagePixelSize();

            if (rect == Rect.Empty || rect.Width <= 0 || rect.Height <= 0)
                return false;

            if (!rect.Contains(viewPoint))
                return false;

            double scaleX = rect.Width / pixelSize.Width;
            double scaleY = rect.Height / pixelSize.Height;

            double px = (viewPoint.X - rect.X) / scaleX;
            double py = (viewPoint.Y - rect.Y) / scaleY;

            px = Math.Max(0, Math.Min(px, pixelSize.Width - 1));
            py = Math.Max(0, Math.Min(py, pixelSize.Height - 1));

            pixelPoint = new Point(px, py);
            return true;
        }

        public Point PixelToView(Point pixelPoint)
        {
            var rect = GetRenderedImageRect();
            var pixelSize = getImagePixelSize();

            if (rect == Rect.Empty || rect.Width <= 0 || rect.Height <= 0)
                return new Point();

            double scaleX = rect.Width / pixelSize.Width;
            double scaleY = rect.Height / pixelSize.Height;

            return new Point(
                rect.X + pixelPoint.X * scaleX,
                rect.Y + pixelPoint.Y * scaleY
            );
        }

        public Point GetClampedPixelFromView(Point viewPoint)
        {
            var rect = GetRenderedImageRect();
            if (rect == Rect.Empty)
                return new Point();

            var clampedView = new Point(
                Math.Max(rect.Left, Math.Min(viewPoint.X, rect.Right)),
                Math.Max(rect.Top, Math.Min(viewPoint.Y, rect.Bottom))
            );

            if (TryViewToPixel(clampedView, out var pixel))
                return pixel;

            return new Point();
        }
    }
}
