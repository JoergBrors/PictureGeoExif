using System.Windows.Media;

namespace PictureExifclone.Models
{
    public class ToolContext
    {
        public string? Text { get; set; }
        public float FontSize { get; set; } = 48f;
        public Color Color { get; set; } = Colors.White;
        public int PixelSize { get; set; } = 20;
    }
}
