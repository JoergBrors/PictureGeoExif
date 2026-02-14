using System.Windows;

namespace PictureExifclone.Models
{
    public class ImageDocument
    {
        public string FilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public Point? ClickPosition { get; set; }
        public Rect? SelectionRect { get; set; }
    }
}
