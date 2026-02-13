using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Web.WebView2.Core;
using System.Linq;
using PictureExifclone.Services;
using PictureExifclone.Models;
using System.Windows.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;

namespace PictureExifclone
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ImageItem> images = new ObservableCollection<ImageItem>();
        private double currentLatitude = 0;
        private double currentLongitude = 0;
        private bool hasCoordinates = false;
        private AppSettings settings;
        private ImageService imageService;
        private ImageItem? selectedImage = null;

        public MainWindow()
        {
            InitializeComponent();
            ImageItemsControl.ItemsSource = images;
            
            imageService = new ImageService();
            
            settings = AppSettings.Load();
            if (string.IsNullOrEmpty(settings.OutputFolder))
            {
                settings.OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PictureExifclone_Output");
            }
            OutputFolderTextBox.Text = settings.OutputFolder;
            
            images.CollectionChanged += (s, e) => UpdateImageCount();
            
            InitializeWebView();
            
            Closed += (s, e) =>
            {
                imageService?.Dispose();
                settings.Save();
            };
        }

        private void UpdateImageCount()
        {
            ImageCountText.Text = $"{images.Count} Bild(er) geladen";
            DropHintText.Visibility = images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SaveAllButton.IsEnabled = images.Count > 0;
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
                if (parts.Length == 2 &&
                    double.TryParse(parts[0],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double lat) &&
                    double.TryParse(parts[1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double lng))
                {
                    currentLatitude = lat;
                    currentLongitude = lng;
                    hasCoordinates = true;
                    CurrentGpsText.Text = $"Lat: {lat:F6}, Lng: {lng:F6}";
                    UpdateButtonStates();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Verarbeiten der Koordinaten: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetLeafletHtml()
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='utf-8'>");
            html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />");
            html.AppendLine("    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { margin: 0; padding: 0; }");
            html.AppendLine("        #map { width: 100%; height: 100vh; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div id='map'></div>");
            html.AppendLine("    <script>");
            html.AppendLine("        var map = L.map('map').setView([51.1657, 10.4515], 6);");
            html.AppendLine("        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {");
            html.AppendLine("            attribution: '&copy; OpenStreetMap contributors',");
            html.AppendLine("            maxZoom: 19");
            html.AppendLine("        }).addTo(map);");
            html.AppendLine("");
            html.AppendLine("        var currentMarker = null;");
            html.AppendLine("        var imageMarkers = [];");
            html.AppendLine("        var selectedMarker = null;");
            html.AppendLine("        var gridLayer = null;");
            html.AppendLine("        var currentGridSize = 100;");
            html.AppendLine("");
            
            // SVG Icons als separate Variablen
            html.AppendLine("        var selectedIconSvg = 'PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIzMiIgaGVpZ2h0PSI0MiIgdmlld0JveD0iMCAwIDMyIDQyIj48cGF0aCBmaWxsPSIjMjE5NkYzIiBzdHJva2U9IiNGRkYiIHN0cm9rZS13aWR0aD0iMiIgZD0iTTE2IDBDOS40IDAgNCA1LjQgNCAxMmMwIDggMTIgMzAgMTIgMzBzMTItMjIgMTItMzBjMC02LjYtNS40LTEyLTEyLTEyeiIvPjxjaXJjbGUgY3g9IjE2IiBjeT0iMTIiIHI9IjYiIGZpbGw9IiNGRkYiLz48L3N2Zz4=';");
            html.AppendLine("        var normalIconSvg = 'PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNSIgaGVpZ2h0PSIzNSIgdmlld0JveD0iMCAwIDI1IDM1Ij48cGF0aCBmaWxsPSIjNENBRjUwIiBzdHJva2U9IiNGRkYiIHN0cm9rZS13aWR0aD0iMiIgZD0iTTEyLjUgMEM3LjI1IDAgMyA0LjI1IDMgOS41YzAgNi4yNSA5LjUgMjMuNSA5LjUgMjMuNVMyMiAxNS43NSAyMiA5LjVDMjIgNC4yNSAxNy43NSAwIDEyLjUgMHoiLz48Y2lyY2xlIGN4PSIxMi41IiBjeT0iOS41IiByPSI0IiBmaWxsPSIjRkZGIi8+PC9zdmc+';");
            html.AppendLine("        var clickIconSvg = 'PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNSIgaGVpZ2h0PSIzNSIgdmlld0JveD0iMCAwIDI1IDM1Ij48cGF0aCBmaWxsPSIjRkY1NzIyIiBzdHJva2U9IiNGRkYiIHN0cm9rZS13aWR0aD0iMiIgZD0iTTEyLjUgMEM3LjI1IDAgMyA0LjI1IDMgOS41YzAgNi4yNSA5LjUgMjMuNSA5LjUgMjMuNVMyMiAxNS43NSAyMiA5LjVDMjIgNC4yNSAxNy43NSAwIDEyLjUgMHoiLz48Y2lyY2xlIGN4PSIxMi41IiBjeT0iOS41IiByPSI0IiBmaWxsPSIjRkZGIi8+PC9zdmc+';");
            html.AppendLine("");
            html.AppendLine("        var selectedIcon = L.icon({");
            html.AppendLine("            iconUrl: 'data:image/svg+xml;base64,' + selectedIconSvg,");
            html.AppendLine("            iconSize: [32, 42],");
            html.AppendLine("            iconAnchor: [16, 42],");
            html.AppendLine("            popupAnchor: [0, -42]");
            html.AppendLine("        });");
            html.AppendLine("");
            html.AppendLine("        var normalIcon = L.icon({");
            html.AppendLine("            iconUrl: 'data:image/svg+xml;base64,' + normalIconSvg,");
            html.AppendLine("            iconSize: [25, 35],");
            html.AppendLine("            iconAnchor: [12.5, 35],");
            html.AppendLine("            popupAnchor: [0, -35]");
            html.AppendLine("        });");
            html.AppendLine("");
            html.AppendLine("        var clickIcon = L.icon({");
            html.AppendLine("            iconUrl: 'data:image/svg+xml;base64,' + clickIconSvg,");
            html.AppendLine("            iconSize: [25, 35],");
            html.AppendLine("            iconAnchor: [12.5, 35],");
            html.AppendLine("            popupAnchor: [0, -35]");
            html.AppendLine("        });");
            html.AppendLine("");
            html.AppendLine("        map.on('click', function(e) {");
            html.AppendLine("            var lat = e.latlng.lat;");
            html.AppendLine("            var lng = e.latlng.lng;");
            html.AppendLine("            if (currentMarker) { map.removeLayer(currentMarker); }");
            html.AppendLine("            currentMarker = L.marker([lat, lng], { icon: clickIcon }).addTo(map);");
            html.AppendLine("            currentMarker.bindPopup('<b>Neue Koordinaten:</b><br>Lat: ' + lat.toFixed(6) + '<br>Lng: ' + lng.toFixed(6)).openPopup();");
            html.AppendLine("            window.chrome.webview.postMessage(lat + ',' + lng);");
            html.AppendLine("        });");
            html.AppendLine("");
            html.AppendLine("        function addImageMarkers(markers) {");
            html.AppendLine("            imageMarkers.forEach(m => map.removeLayer(m));");
            html.AppendLine("            imageMarkers = [];");
            html.AppendLine("            markers.forEach(function(m) {");
            html.AppendLine("                var marker = L.marker([m.lat, m.lng], { icon: normalIcon }).addTo(map);");
            html.AppendLine("                marker.bindPopup('<b>' + m.name + '</b><br>Lat: ' + m.lat.toFixed(6) + '<br>Lng: ' + m.lng.toFixed(6));");
            html.AppendLine("                marker.imageId = m.id;");
            html.AppendLine("                imageMarkers.push(marker);");
            html.AppendLine("            });");
            html.AppendLine("            if (markers.length > 0) {");
            html.AppendLine("                var group = new L.featureGroup(imageMarkers);");
            html.AppendLine("                map.fitBounds(group.getBounds().pad(0.1));");
            html.AppendLine("            }");
            html.AppendLine("        }");
            html.AppendLine("");
            html.AppendLine("        function setSelectedMarker(lat, lng, name) {");
            html.AppendLine("            if (selectedMarker) { map.removeLayer(selectedMarker); }");
            html.AppendLine("            selectedMarker = L.marker([lat, lng], { icon: selectedIcon }).addTo(map);");
            html.AppendLine("            selectedMarker.bindPopup('<b>Ausgewählt:</b><br>' + name + '<br>Lat: ' + lat.toFixed(6) + '<br>Lng: ' + lng.toFixed(6)).openPopup();");
            html.AppendLine("            map.setView([lat, lng], Math.max(map.getZoom(), 13));");
            html.AppendLine("        }");
            html.AppendLine("");
            html.AppendLine("        function clearSelectedMarker() {");
            html.AppendLine("            if (selectedMarker) { map.removeLayer(selectedMarker); selectedMarker = null; }");
            html.AppendLine("        }");
            html.AppendLine("");
            html.AppendLine("        function updateGrid(enabled, size) {");
            html.AppendLine("            if (gridLayer) { map.removeLayer(gridLayer); gridLayer = null; }");
            html.AppendLine("            if (!enabled) return;");
            html.AppendLine("            currentGridSize = size;");
            html.AppendLine("            gridLayer = L.layerGroup();");
            html.AppendLine("            var bounds = map.getBounds();");
            html.AppendLine("            var metersPerDegree = 111000;");
            html.AppendLine("            var gridSize = size / metersPerDegree;");
            html.AppendLine("            var south = Math.floor(bounds.getSouth() / gridSize) * gridSize;");
            html.AppendLine("            var north = Math.ceil(bounds.getNorth() / gridSize) * gridSize;");
            html.AppendLine("            var west = Math.floor(bounds.getWest() / gridSize) * gridSize;");
            html.AppendLine("            var east = Math.ceil(bounds.getEast() / gridSize) * gridSize;");
            html.AppendLine("            for (var lat = south; lat <= north; lat += gridSize) {");
            html.AppendLine("                for (var lng = west; lng <= east; lng += gridSize) {");
            html.AppendLine("                    var rectBounds = [[lat, lng], [lat + gridSize, lng + gridSize]];");
            html.AppendLine("                    L.rectangle(rectBounds, {");
            html.AppendLine("                        color: '#2196F3',");
            html.AppendLine("                        weight: 1,");
            html.AppendLine("                        fillOpacity: 0.05,");
            html.AppendLine("                        fillColor: '#2196F3'");
            html.AppendLine("                    }).addTo(gridLayer);");
            html.AppendLine("                }");
            html.AppendLine("            }");
            html.AppendLine("            gridLayer.addTo(map);");
            html.AppendLine("        }");
            html.AppendLine("");
            html.AppendLine("        map.on('moveend', function() {");
            html.AppendLine("            if (gridLayer) { updateGrid(true, currentGridSize); }");
            html.AppendLine("        });");
            html.AppendLine("    </script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private void UpdateAllMarkersOnMap()
        {
            var markersData = images
                .Where(img => img.HasGpsData)
                .Select((img, index) => new {
                    id = index,
                    lat = img.Latitude!.Value,
                    lng = img.Longitude!.Value,
                    name = img.FileName
                })
                .ToList();

            if (markersData.Any())
            {
                var json = System.Text.Json.JsonSerializer.Serialize(markersData);
                string script = $"addImageMarkers({json});";
                MapWebView.CoreWebView2?.ExecuteScriptAsync(script);
            }
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
            int loadedCount = 0;
            foreach (var filePath in filePaths)
            {
                try
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
                        extension == ".bmp" || extension == ".tif" || extension == ".tiff")
                    {
                        if (images.Any(img => img.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var imageItem = new ImageItem
                        {
                            FilePath = filePath
                        };

                        // Thumbnail erstellen mit Fehlerbehandlung
                        try
                        {
                            imageItem.Thumbnail = imageService.CreateThumbnail(filePath);
                            
                            if (imageItem.Thumbnail == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Thumbnail ist NULL für: {Path.GetFileName(filePath)}");
                            }
                        }
                        catch (Exception thumbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen des Thumbnails für {Path.GetFileName(filePath)}: {thumbEx.Message}");
                            // Weiter ohne Thumbnail
                        }

                        ReadExifData(imageItem);
                        images.Add(imageItem);
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden von {Path.GetFileName(filePath)}: {ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            if (loadedCount > 0)
            {
                UpdateAllMarkersOnMap();
                MessageBox.Show($"{loadedCount} Bild(er) erfolgreich geladen!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UpdateButtonStates();
        }

        private void ReadExifData(ImageItem imageItem)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(imageItem.FilePath);
                var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();

                if (gpsDirectory != null && gpsDirectory.TryGetGeoLocation(out var location) && location != null)
                {
                    imageItem.Latitude = location.Latitude;
                    imageItem.Longitude = location.Longitude;
                }
            }
            catch
            {
                // GPS-Daten konnten nicht gelesen werden
            }
        }

        private void ImageTile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ImageItem imageItem)
            {
                if (selectedImage != null)
                {
                    selectedImage.IsSelected = false;
                }

                selectedImage = imageItem;
                selectedImage.IsSelected = true;

                if (selectedImage.HasGpsData)
                {
                    currentLatitude = selectedImage.Latitude!.Value;
                    currentLongitude = selectedImage.Longitude!.Value;
                    hasCoordinates = true;
                    CurrentGpsText.Text = $"Lat: {currentLatitude:F6}, Lng: {currentLongitude:F6}";

                    string script = $"setSelectedMarker({currentLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {currentLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{selectedImage.FileName.Replace("'", "\\'")}');";
                    MapWebView.CoreWebView2?.ExecuteScriptAsync(script);
                }
                else
                {
                    string script = "clearSelectedMarker();";
                    MapWebView.CoreWebView2?.ExecuteScriptAsync(script);
                }

                UpdateButtonStates();
            }
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = selectedImage != null;
            bool hasImages = images.Count > 0;

            if (SaveAllButton != null)
                SaveAllButton.IsEnabled = hasImages;
            if (ApplyGpsButton != null)
                ApplyGpsButton.IsEnabled = hasSelection && hasCoordinates;
        }

        private void EditImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                try
                {
                    // Verwende den vollständigen ImageEditorWindow mit allen Features:
                    // - Zuschneiden
                    // - Text/Wasserzeichen
                    // - Geo-Wasserzeichen
                    // - Verpixeln
                    var editor = new ImageEditorWindow(
                        imageItem.FilePath,
                        imageItem.Latitude,
                        imageItem.Longitude)
                    {
                        Owner = this
                    };

                    if (editor.ShowDialog() == true && editor.EditedImageBytes != null)
                    {
                        // Altes Thumbnail freigeben BEVOR das neue Bild gespeichert wird
                        if (imageItem.Thumbnail != null)
                        {
                            imageItem.Thumbnail = null;
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }

                        string newPath = imageService.SaveEditedImage(
                            editor.EditedImageBytes,
                            imageItem.FileName,
                            settings.OutputFolder);

                        if (imageItem.HasGpsData)
                        {
                            imageService.WriteGpsToImage(newPath, imageItem.Latitude!.Value, imageItem.Longitude!.Value);
                        }

                        // Pfad aktualisieren
                        imageItem.FilePath = newPath;
                        
                        // Kurze Verzögerung um sicherzustellen, dass die Datei verfügbar ist
                        System.Threading.Thread.Sleep(100);
                        
                        // Neues Thumbnail erstellen mit Cache-Umgehung
                        imageItem.Thumbnail = imageService.CreateThumbnail(newPath);

                        MessageBox.Show($"Bearbeitetes Bild gespeichert:\n{newPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Bearbeiten: {ex.Message}\n\nDetails: {ex.StackTrace}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

                    if (gpsDirectory != null && gpsDirectory.TryGetGeoLocation(out var location) && location != null)
                    {
                        currentLatitude = location.Latitude;
                        currentLongitude = location.Longitude;
                        hasCoordinates = true;
                        CurrentGpsText.Text = $"Lat: {location.Latitude:F6}, Lng: {location.Longitude:F6}";

                        string script = $"if (currentMarker) {{ map.removeLayer(currentMarker); }} currentMarker = L.marker([{location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], {{ icon: clickIcon }}).addTo(map); map.setView([{location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 13);";
                        MapWebView.CoreWebView2?.ExecuteScriptAsync(script);

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
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Lesen des Referenzbildes: {ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ApplyGpsButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedImage != null && hasCoordinates)
            {
                try
                {
                    string newPath = imageService.SaveSingleImage(
                        selectedImage.FilePath,
                        settings.OutputFolder,
                        currentLatitude,
                        currentLongitude);

                    selectedImage.FilePath = newPath;
                    selectedImage.Latitude = currentLatitude;
                    selectedImage.Longitude = currentLongitude;
                    selectedImage.Thumbnail = imageService.CreateThumbnail(newPath);

                    UpdateAllMarkersOnMap();
                    MessageBox.Show($"Bild mit GPS-Koordinaten gespeichert:\n{newPath}",
                        "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern: {ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

            int savedCount = 0;
            int errorCount = 0;

            foreach (var img in images)
            {
                try
                {
                    double? lat = hasCoordinates ? (double?)currentLatitude : img.Latitude;
                    double? lon = hasCoordinates ? (double?)currentLongitude : img.Longitude;

                    string newPath = imageService.SaveSingleImage(img.FilePath, settings.OutputFolder, lat, lon);

                    img.FilePath = newPath;
                    if (lat.HasValue && lon.HasValue)
                    {
                        img.Latitude = lat.Value;
                        img.Longitude = lon.Value;
                    }
                    img.Thumbnail = imageService.CreateThumbnail(newPath);

                    savedCount++;
                }
                catch (Exception)
                {
                    errorCount++;
                }
            }

            UpdateAllMarkersOnMap();
            MessageBox.Show($"{savedCount} Bild(er) erfolgreich gespeichert!\n{errorCount} Fehler.",
                "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                var result = MessageBox.Show(
                    $"Möchten Sie '{imageItem.FileName}' aus der Liste entfernen?\n\n(Die Datei wird nicht gelöscht)",
                    "Bild entfernen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (selectedImage == imageItem)
                    {
                        selectedImage = null;
                    }
                    images.Remove(imageItem);
                    UpdateAllMarkersOnMap();
                    UpdateButtonStates();
                }
            }
        }

        private void SaveSingleImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                try
                {
                    double? lat = imageItem.Latitude ?? (hasCoordinates ? (double?)currentLatitude : null);
                    double? lon = imageItem.Longitude ?? (hasCoordinates ? (double?)currentLongitude : null);

                    string newPath = imageService.SaveSingleImage(imageItem.FilePath, settings.OutputFolder, lat, lon);

                    imageItem.FilePath = newPath;
                    if (lat.HasValue && lon.HasValue)
                    {
                        imageItem.Latitude = lat.Value;
                        imageItem.Longitude = lon.Value;
                    }
                    imageItem.Thumbnail = imageService.CreateThumbnail(newPath);

                    UpdateAllMarkersOnMap();
                    MessageBox.Show($"Bild gespeichert:\n{newPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern von {imageItem.FileName}: {ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (images.Count == 0) return;

            var result = MessageBox.Show(
                $"Möchten Sie alle {images.Count} Bilder aus der Liste entfernen?\n\n(Die Dateien werden nicht gelöscht)",
                "Alle entfernen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                selectedImage = null;
                images.Clear();
                
                string script = "imageMarkers.forEach(m => map.removeLayer(m)); imageMarkers = []; clearSelectedMarker();";
                MapWebView.CoreWebView2?.ExecuteScriptAsync(script);
                
                UpdateButtonStates();
            }
        }

        private void ChangeOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = "Wählen Sie den Ausgabeordner für bearbeitete Bilder:",
                SelectedPath = settings.OutputFolder,
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog(this) == true)
            {
                settings.OutputFolder = folderDialog.SelectedPath;
                settings.Save();
                OutputFolderTextBox.Text = settings.OutputFolder;
                MessageBox.Show($"Speicherort geändert zu:\n{settings.OutputFolder}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GridEnabled_Changed(object sender, RoutedEventArgs e)
        {
            UpdateMapGrid();
        }

        private void GridSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateMapGrid();
        }

        private void UpdateMapGrid()
        {
            if (MapWebView?.CoreWebView2 == null) return;

            bool enabled = GridEnabledCheckBox?.IsChecked == true;
            double size = GridSizeSlider?.Value ?? 100;

            string script = $"updateGrid({enabled.ToString().ToLower()}, {size.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
            MapWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}
