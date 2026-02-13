using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Microsoft.Web.WebView2.Core;
using ImageSharpRational = SixLabors.ImageSharp.Rational;

namespace PictureExifclone
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ImageItem> images = new ObservableCollection<ImageItem>();
        private double currentLatitude = 0;
        private double currentLongitude = 0;
        private bool hasCoordinates = false;

        public MainWindow()
        {
            InitializeComponent();
            ImageListBox.ItemsSource = images;
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await MapWebView.EnsureCoreWebView2Async(null);
                MapWebView.CoreWebView2.NavigateToString(GetLeafletHtml());
                MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Initialisieren der Karte: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                var parts = message.Split(',');
                if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lng))
                {
                    currentLatitude = lat;
                    currentLongitude = lng;
                    hasCoordinates = true;
                    CurrentGpsText.Text = $"Lat: {lat:F6}, Lng: {lng:F6}";
                    ApplyGpsButton.IsEnabled = ImageListBox.SelectedItem != null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Verarbeiten der Koordinaten: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetLeafletHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body { margin: 0; padding: 0; }
        #map { width: 100%; height: 100vh; }
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([51.1657, 10.4515], 6);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(map);

        var marker = null;

        map.on('click', function(e) {
            var lat = e.latlng.lat;
            var lng = e.latlng.lng;
            
            if (marker) {
                map.removeLayer(marker);
            }
            
            marker = L.marker([lat, lng]).addTo(map);
            marker.bindPopup('<b>Ausgewählte Koordinaten:</b><br>Lat: ' + lat.toFixed(6) + '<br>Lng: ' + lng.toFixed(6)).openPopup();
            
            window.chrome.webview.postMessage(lat + ',' + lng);
        });
    </script>
</body>
</html>";
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadImages(files);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void LoadImagesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|Alle Dateien|*.*",
                Multiselect = true,
                Title = "Bilder auswählen"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImages(openFileDialog.FileNames);
            }
        }

        private void LoadImages(string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
                        extension == ".bmp" || extension == ".tif" || extension == ".tiff")
                    {
                        var imageItem = new ImageItem
                        {
                            FilePath = filePath,
                            FileName = Path.GetFileName(filePath),
                            Thumbnail = CreateThumbnail(filePath)
                        };

                        ReadExifData(imageItem);
                        images.Add(imageItem);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden von {Path.GetFileName(filePath)}: {ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            UpdateButtonStates();
        }

        private BitmapImage CreateThumbnail(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.DecodePixelWidth = 200;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void ReadExifData(ImageItem imageItem)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(imageItem.FilePath);
                var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();

                if (gpsDirectory != null)
                {
                    var latitude = gpsDirectory.TryGetGeoLocation(out var location);
                    if (latitude && location != null)
                    {
                        imageItem.Latitude = location.Latitude;
                        imageItem.Longitude = location.Longitude;
                        imageItem.HasGps = true;
                        imageItem.GpsInfo = $"GPS: {location.Latitude:F6}, {location.Longitude:F6}";
                    }
                    else
                    {
                        imageItem.GpsInfo = "Keine GPS-Daten";
                    }
                }
                else
                {
                    imageItem.GpsInfo = "Keine GPS-Daten";
                }
            }
            catch
            {
                imageItem.GpsInfo = "Fehler beim Lesen der GPS-Daten";
            }
        }

        private void ImageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = ImageListBox.SelectedItem != null;
            bool hasImages = images.Count > 0;

            EditImageButton.IsEnabled = hasSelection;
            SaveAllButton.IsEnabled = hasImages;
            ApplyGpsButton.IsEnabled = hasSelection && hasCoordinates;
        }

        private void EditImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListBox.SelectedItem is ImageItem selectedImage && selectedImage.HasGps)
            {
                string script = $"if (marker) {{ map.removeLayer(marker); }} marker = L.marker([{selectedImage.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {selectedImage.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}]).addTo(map); map.setView([{selectedImage.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {selectedImage.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 13);";
                MapWebView.CoreWebView2.ExecuteScriptAsync(script);

                currentLatitude = selectedImage.Latitude;
                currentLongitude = selectedImage.Longitude;
                hasCoordinates = true;
                CurrentGpsText.Text = $"Lat: {selectedImage.Latitude:F6}, Lng: {selectedImage.Longitude:F6}";
                ApplyGpsButton.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Das ausgewählte Bild hat keine GPS-Daten.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UseReferenceImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|Alle Dateien|*.*",
                Multiselect = false,
                Title = "Referenzbild mit GPS-Daten auswählen"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var directories = ImageMetadataReader.ReadMetadata(openFileDialog.FileName);
                    var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();

                    if (gpsDirectory != null)
                    {
                        var hasLocation = gpsDirectory.TryGetGeoLocation(out var location);
                        if (hasLocation && location != null)
                        {
                            currentLatitude = location.Latitude;
                            currentLongitude = location.Longitude;
                            hasCoordinates = true;
                            CurrentGpsText.Text = $"Lat: {location.Latitude:F6}, Lng: {location.Longitude:F6}";
                            
                            string script = $"if (marker) {{ map.removeLayer(marker); }} marker = L.marker([{location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}]).addTo(map); map.setView([{location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 13);";
                            MapWebView.CoreWebView2.ExecuteScriptAsync(script);

                            UpdateButtonStates();
                            MessageBox.Show("GPS-Koordinaten aus Referenzbild erfolgreich geladen!", 
                                "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Das Referenzbild enthält keine gültigen GPS-Daten.", 
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Das Referenzbild enthält keine GPS-Daten.", 
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Lesen des Referenzbildes: {ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ApplyGpsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListBox.SelectedItem is ImageItem selectedImage && hasCoordinates)
            {
                try
                {
                    WriteGpsToImage(selectedImage.FilePath, currentLatitude, currentLongitude);
                    selectedImage.Latitude = currentLatitude;
                    selectedImage.Longitude = currentLongitude;
                    selectedImage.HasGps = true;
                    selectedImage.GpsInfo = $"GPS: {currentLatitude:F6}, {currentLongitude:F6}";
                    selectedImage.IsModified = true;

                    ImageListBox.Items.Refresh();
                    MessageBox.Show("GPS-Koordinaten erfolgreich auf das Bild angewendet!", 
                        "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Schreiben der GPS-Daten: {ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void WriteGpsToImage(string filePath, double latitude, double longitude)
        {
            using (var image = SixLabors.ImageSharp.Image.Load(filePath))
            {
                var exifProfile = image.Metadata.ExifProfile ?? new ExifProfile();

                byte latRef = latitude >= 0 ? (byte)'N' : (byte)'S';
                byte lonRef = longitude >= 0 ? (byte)'E' : (byte)'W';

                exifProfile.SetValue(ExifTag.GPSLatitudeRef, new string(new[] { (char)latRef }));
                exifProfile.SetValue(ExifTag.GPSLongitudeRef, new string(new[] { (char)lonRef }));

                var latRational = ConvertToRational(Math.Abs(latitude));
                var lonRational = ConvertToRational(Math.Abs(longitude));

                exifProfile.SetValue(ExifTag.GPSLatitude, latRational);
                exifProfile.SetValue(ExifTag.GPSLongitude, lonRational);

                image.Metadata.ExifProfile = exifProfile;
                image.Save(filePath);
            }
        }

        private ImageSharpRational[] ConvertToRational(double value)
        {
            int degrees = (int)value;
            double minutesDecimal = (value - degrees) * 60;
            int minutes = (int)minutesDecimal;
            double seconds = (minutesDecimal - minutes) * 60;

            return new ImageSharpRational[]
            {
                new ImageSharpRational((uint)degrees, 1),
                new ImageSharpRational((uint)minutes, 1),
                new ImageSharpRational((uint)(seconds * 1000000), 1000000)
            };
        }

        private void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!hasCoordinates)
            {
                var result = MessageBox.Show(
                    "Sie haben keine GPS-Koordinaten ausgewählt. Möchten Sie die Bilder trotzdem speichern?",
                    "Bestätigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            var modifiedImages = images.Where(img => !img.IsModified).ToList();

            if (modifiedImages.Count > 0 && hasCoordinates)
            {
                var result = MessageBox.Show(
                    $"Möchten Sie die aktuellen GPS-Koordinaten auf alle {modifiedImages.Count} Bilder ohne GPS-Daten anwenden?",
                    "GPS-Daten anwenden", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var img in modifiedImages)
                    {
                        try
                        {
                            WriteGpsToImage(img.FilePath, currentLatitude, currentLongitude);
                            img.Latitude = currentLatitude;
                            img.Longitude = currentLongitude;
                            img.HasGps = true;
                            img.GpsInfo = $"GPS: {currentLatitude:F6}, {currentLongitude:F6}";
                            img.IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Fehler bei {img.FileName}: {ex.Message}", 
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }

            ImageListBox.Items.Refresh();
            MessageBox.Show($"Alle Bilder wurden erfolgreich verarbeitet!", 
                "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class ImageItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public BitmapImage? Thumbnail { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasGps { get; set; }
        public string GpsInfo { get; set; } = "Keine GPS-Daten";
        public bool IsModified { get; set; }
    }
}