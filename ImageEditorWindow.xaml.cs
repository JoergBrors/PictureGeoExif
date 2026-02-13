using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using System.Linq;
using WpfPoint = System.Windows.Point;
using ImageSharpFontFamily = SixLabors.Fonts.FontFamily;
using PictureExifclone.Services;

namespace PictureExifclone
{
    public partial class ImageEditorWindow : Window
    {
        private string workingImagePath;
        private Image<Rgba32>? image;
        private double? latitude;
        private double? longitude;
        private double currentZoom = 1.0;
        private bool isCropping = false;
        private WpfPoint cropStartPoint;
        private readonly ImageService imageService;
        private bool isDisposed = false;
        private bool isInitialized = false;

        public byte[]? EditedImageBytes { get; private set; }

        public ImageEditorWindow(string filePath, double? lat = null, double? lon = null)
        {
            InitializeComponent();
            this.latitude = lat;
            this.longitude = lon;

            imageService = new ImageService();

            try
            {
                // Create a temporary working copy
                workingImagePath = imageService.CreateTempCopy(filePath);

                // Load image from temp file with error handling
                image = SixLabors.ImageSharp.Image.Load<Rgba32>(workingImagePath);

                // Wichtig: Warte auf ContentRendered statt Loaded
                ContentRendered += (s, e) =>
                {
                    if (!isInitialized)
                    {
                        try
                        {
                            ShowPreview();
                            // Verzögere ZoomFit etwas, damit ScrollViewer vollständig geladen ist
                            Dispatcher.InvokeAsync(() => 
                            {
                                ZoomFit();
                                isInitialized = true;
                            }, System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Fehler beim Initialisieren: {ex.Message}\n\nStack: {ex.StackTrace}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };

                Closed += (s, e) =>
                {
                    Cleanup();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden des Bildes: {ex.Message}\n\nBitte versuchen Sie es mit einem anderen Bild.", 
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void Cleanup()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                image?.Dispose();
                image = null;
            }
            catch { }

            try
            {
                imageService?.Dispose();
            }
            catch { }
        }

        private void ShowPreview()
        {
            if (image == null || isDisposed) return;

            try
            {
                // Save current state to working file
                image.Save(workingImagePath);

                // Load and display
                var bitmap = imageService.LoadBitmapImage(workingImagePath);
                EditorImage.Source = bitmap;
                EditorImage.Width = image.Width;
                EditorImage.Height = image.Height;

                ImageCanvas.Width = image.Width;
                ImageCanvas.Height = image.Height;

                // Debug-Ausgabe
                System.Diagnostics.Debug.WriteLine($"ShowPreview: Image Size = {image.Width}x{image.Height}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Anzeigen der Vorschau: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZoomFit()
        {
            if (image == null || isDisposed) return;

            try
            {
                // Prüfe ob ScrollViewer bereits eine Größe hat
                if (ImageScrollViewer.ActualWidth <= 0 || ImageScrollViewer.ActualHeight <= 0)
                {
                    // Setze auf 100% wenn noch keine Größe vorhanden
                    currentZoom = 1.0;
                    ApplyZoom();
                    System.Diagnostics.Debug.WriteLine("ZoomFit: ScrollViewer noch nicht initialisiert, setze Zoom auf 100%");
                    return;
                }

                var viewportWidth = ImageScrollViewer.ActualWidth - 20;
                var viewportHeight = ImageScrollViewer.ActualHeight - 20;

                if (viewportWidth <= 0 || viewportHeight <= 0)
                {
                    currentZoom = 1.0;
                    ApplyZoom();
                    return;
                }

                var scaleX = viewportWidth / image.Width;
                var scaleY = viewportHeight / image.Height;
                currentZoom = Math.Min(scaleX, scaleY);
                currentZoom = Math.Max(0.1, Math.Min(currentZoom, 5.0));

                ApplyZoom();
                
                System.Diagnostics.Debug.WriteLine($"ZoomFit: Viewport = {viewportWidth}x{viewportHeight}, Image = {image.Width}x{image.Height}, Zoom = {currentZoom:P0}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Anpassen der Ansicht: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyZoom()
        {
            if (image == null || isDisposed) return;

            try
            {
                ImageScaleTransform.ScaleX = currentZoom;
                ImageScaleTransform.ScaleY = currentZoom;
                ZoomText.Text = $"{(int)(currentZoom * 100)}%";

                ImageCanvas.Width = image.Width * currentZoom;
                ImageCanvas.Height = image.Height * currentZoom;
            }
            catch { }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (isDisposed) return;
            currentZoom = Math.Min(currentZoom * 1.2, 5.0);
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (isDisposed) return;
            currentZoom = Math.Max(currentZoom / 1.2, 0.1);
            ApplyZoom();
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            ZoomFit();
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || isDisposed) return;

            if (!isCropping)
            {
                isCropping = true;
                CropRectangle.Visibility = Visibility.Visible;
                MessageBox.Show("Ziehen Sie mit der Maus einen Bereich auf, den Sie behalten möchten.", "Zuschneiden", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ApplyCrop();
            }
        }

        private void ImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isCropping && !isDisposed)
            {
                cropStartPoint = e.GetPosition(ImageCanvas);
                CropRectangle.Width = 0;
                CropRectangle.Height = 0;
                System.Windows.Controls.Canvas.SetLeft(CropRectangle, cropStartPoint.X);
                System.Windows.Controls.Canvas.SetTop(CropRectangle, cropStartPoint.Y);
                CropRectangle.Visibility = Visibility.Visible;
            }
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isCropping && e.LeftButton == MouseButtonState.Pressed && !isDisposed)
            {
                var currentPoint = e.GetPosition(ImageCanvas);
                var width = Math.Abs(currentPoint.X - cropStartPoint.X);
                var height = Math.Abs(currentPoint.Y - cropStartPoint.Y);

                CropRectangle.Width = width;
                CropRectangle.Height = height;

                System.Windows.Controls.Canvas.SetLeft(CropRectangle, Math.Min(cropStartPoint.X, currentPoint.X));
                System.Windows.Controls.Canvas.SetTop(CropRectangle, Math.Min(cropStartPoint.Y, currentPoint.Y));
            }
        }

        private void ImageCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isCropping && CropRectangle.Width > 10 && CropRectangle.Height > 10 && !isDisposed)
            {
                var result = MessageBox.Show("Möchten Sie diesen Bereich zuschneiden?", "Zuschneiden bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    ApplyCrop();
                }
                else
                {
                    CropRectangle.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ApplyCrop()
        {
            if (image == null || isDisposed) return;

            try
            {
                if (CropRectangle.Width > 10 && CropRectangle.Height > 10)
                {
                    var left = (int)(System.Windows.Controls.Canvas.GetLeft(CropRectangle) / currentZoom);
                    var top = (int)(System.Windows.Controls.Canvas.GetTop(CropRectangle) / currentZoom);
                    var width = (int)(CropRectangle.Width / currentZoom);
                    var height = (int)(CropRectangle.Height / currentZoom);

                    left = Math.Max(0, Math.Min(left, image.Width));
                    top = Math.Max(0, Math.Min(top, image.Height));
                    width = Math.Min(width, image.Width - left);
                    height = Math.Min(height, image.Height - top);

                    if (width > 0 && height > 0)
                    {
                        image.Mutate(x => x.Crop(new SixLabors.ImageSharp.Rectangle(left, top, width, height)));
                        ShowPreview();
                        Dispatcher.InvokeAsync(() => ZoomFit(), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }

                isCropping = false;
                CropRectangle.Visibility = Visibility.Collapsed;
                CropRectangle.Width = 0;
                CropRectangle.Height = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zuschneiden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                isCropping = false;
                CropRectangle.Visibility = Visibility.Collapsed;
            }
        }

        private ImageSharpFontFamily GetAnyFontFamily()
        {
            try
            {
                var sys = SixLabors.Fonts.SystemFonts.Collection;
                if (sys.Families.Any())
                    return sys.Families.First();
            }
            catch { }

            throw new InvalidOperationException("Keine Schriftarten verfügbar");
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || isDisposed) return;

            var text = TextOverlayInput.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Bitte geben Sie einen Text ein.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var family = GetAnyFontFamily();
                var font = family.CreateFont(36);

                image.Mutate(x => x.DrawText(
                    text,
                    font,
                    SixLabors.ImageSharp.Color.White,
                    new SixLabors.ImageSharp.PointF(20, 20)));

                ShowPreview();
                TextOverlayInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Hinzufügen von Text: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddGeoWatermark_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || isDisposed) return;

            if (latitude.HasValue && longitude.HasValue)
            {
                try
                {
                    var text = $"?? Lat: {latitude.Value:F6}  Lon: {longitude.Value:F6}";
                    var family = GetAnyFontFamily();
                    var font = family.CreateFont(24);

                    image.Mutate(x => x.DrawText(
                        text,
                        font,
                        SixLabors.ImageSharp.Color.White,
                        new SixLabors.ImageSharp.PointF(20, image.Height - 50)));

                    ShowPreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Hinzufügen des Wasserzeichens: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Keine Geo-Daten vorhanden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PixelateFaces_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || isDisposed) return;

            try
            {
                int block = 16;
                image.Mutate(ctx => ctx.Pixelate(block));
                ShowPreview();
                MessageBox.Show("Bild wurde verpixelt. Bitte beachten Sie, dass dies das gesamte Bild betrifft.", 
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Verpixeln: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || isDisposed) return;

            try
            {
                using (var ms = new MemoryStream())
                {
                    image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                    {
                        Quality = 95
                    });
                    EditedImageBytes = ms.ToArray();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
