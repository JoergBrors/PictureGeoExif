using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Web.WebView2.Core;
using System.Linq;
using PictureExifclone.Services;
using PictureExifclone.Models;
using System.Windows.Input;
using System.Globalization;

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
            RouteGapSlider.Value = Math.Clamp(settings.RouteMaxGapMeters, RouteGapSlider.Minimum, RouteGapSlider.Maximum);
            SortByRouteCheckBox.IsChecked = settings.SortImagesByRoute;
            BranchMinSlider.Value = Math.Clamp(settings.RouteBranchMinMeters, BranchMinSlider.Minimum, BranchMinSlider.Maximum);
            UpdateRoadServerText();
            
            images.CollectionChanged += (s, e) => UpdateImageCount();
            
            InitializeWebView();
            
            Closed += (s, e) =>
            {
                windowClosed = true;
                MapWebView.Dispose();
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

        private bool mapReady;
        private bool windowClosed;
        private const string MapOrigin = "https://picturegeoexif.local";
        private async void InitializeWebView()
        {
            try
            {
                var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PictureGeoExif", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, profile);
                if (windowClosed) return;
                await MapWebView.EnsureCoreWebView2Async(environment);
                MapWebView.CoreWebView2.Settings.UserAgent += " " + AppInfo.UserAgent;
                MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("picturegeoexif.local", Path.Combine(AppContext.BaseDirectory,"Resources"), CoreWebView2HostResourceAccessKind.DenyCors);
                MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                MapWebView.CoreWebView2.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith(MapOrigin + "/", StringComparison.Ordinal)) e.Cancel = true; };
                MapWebView.CoreWebView2.NewWindowRequested += (_, e) =>
                {
                    e.Handled = true;
                    if (Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri) && uri.Scheme == "https" &&
                        (uri.Host == "www.openstreetmap.org" || uri.Host == "leafletjs.com" ||
                         (Uri.TryCreate(settings.TileAttributionUrl, UriKind.Absolute, out var attribution) && uri.Host == attribution.Host)))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                };
                MapWebView.CoreWebView2.WebResourceResponseReceived += (_, e) =>
                {
                    if (e.Response.StatusCode is 403 or 429) MapStatusText.Text = $"Kartenserver: HTTP {e.Response.StatusCode}. Bitte später erneut versuchen oder Kartenanbieter in den Einstellungen wechseln.";
                };
                MapWebView.CoreWebView2.Navigate(MapOrigin + "/map.html");
            }
            catch (Exception ex) { MapStatusText.Text = "Karte nicht verfügbar (WebView2 Runtime prüfen): " + ex.Message; }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!e.Source.StartsWith(MapOrigin + "/",StringComparison.Ordinal)) return;
            try
            {
                using var message = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                var data=message.RootElement;
                switch (data.GetProperty("type").GetString())
                {
                    case "ready":
                        mapReady=true;
                        await RunMapScriptAsync("configure(" + System.Text.Json.JsonSerializer.Serialize(new { url=settings.TileUrl, attribution=settings.TileAttribution, attributionUrl=settings.TileAttributionUrl }) + ");");
                        ApplyLayerVisibility(); RebuildRoutes(fit: true); UpdateMapGrid(); break;
                    case "status": MapStatusText.Text=data.GetProperty("text").GetString(); break;
                    case "select":
                        int index=data.GetProperty("id").GetInt32();
                        if(index<0 || index>=images.Count) return;
                        SelectImage(images[index]); break;
                    case "hover":
                        int hover=data.GetProperty("id").GetInt32();
                        MapInfoText.Text = hover>=0 && hover<images.Count ? Describe(images[hover]) : SelectionInfo();
                        break;
                    case "routeHover":
                        int number=data.GetProperty("number").GetInt32();
                        var route=routes.FirstOrDefault(r=>r.Number==number);
                        MapInfoText.Text = route != null
                            ? string.Create(CultureInfo.InvariantCulture, $"Trasse {route.Number}: {route.Points.Count} Bild(er), {route.Branches.Count} Abzweig(e), ca. {route.LengthMeters:F0} m, Richtung {(route.NorthSouth ? "Süd → Nord" : "West → Ost")}")
                            : SelectionInfo();
                        break;
                    case "coordinates":
                        double lat=data.GetProperty("lat").GetDouble(), lon=data.GetProperty("lng").GetDouble();
                        if(!PixelGeometry.ValidGps(lat,lon)) return;
                        currentLatitude=lat; currentLongitude=lon; hasCoordinates=true;
                        CurrentGpsText.Text=$"Lat: {lat:F6}, Lng: {lon:F6}";
                        MapInfoText.Text=string.Create(CultureInfo.InvariantCulture, $"Neuer GPS-Punkt: {lat:F6}, {lon:F6} – mit „GPS auf ausgewähltes Bild anwenden“ übernehmen");
                        UpdateButtonStates(); break;
                }
            }
            catch (Exception ex) { MapStatusText.Text="Ungültige Kartennachricht: " + ex.Message; }
        }

        private async Task RunMapScriptAsync(string script)
        {
            if(!mapReady || windowClosed || MapWebView.CoreWebView2 == null) return;
            try { await MapWebView.CoreWebView2.ExecuteScriptAsync(script); }
            catch(Exception ex) { MapStatusText.Text="Karte: " + ex.Message; }
        }

        private void SelectImage(ImageItem item)
        {
            if(selectedImage != null) selectedImage.IsSelected=false;
            selectedImage=item; item.IsSelected=true;
            hasCoordinates=item.HasGpsData;
            if(hasCoordinates) { currentLatitude=item.Latitude!.Value; currentLongitude=item.Longitude!.Value; CurrentGpsText.Text=$"Lat: {currentLatitude:F6}, Lng: {currentLongitude:F6}"; }
            else CurrentGpsText.Text="Keine Koordinaten ausgewählt";
            _ = RunMapScriptAsync(hasCoordinates
                ? $"setSelectedMarker({Js(currentLatitude)},{Js(currentLongitude)},true);"
                : "clearSelectedMarker();");
            MapInfoText.Text = Describe(item);
            UpdateButtonStates();
            if(ImageItemsControl.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement element) element.BringIntoView();
        }

        private static string Js(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private IReadOnlyList<Route> routes = [];

        private string Describe(ImageItem item) =>
            item.FileName + (item.RouteNumber > 0 ? " · " + item.RouteInfo : "") +
            (item.HasGpsData ? string.Create(CultureInfo.InvariantCulture, $" · {item.Latitude:F6}, {item.Longitude:F6}") : " · keine GPS-Daten") +
            (roadDistance.TryGetValue(item, out var d) ? (d is { } m ? $" · {m:F0} m zum Weg" : " · kein Weg in der Nähe") : "");

        private string SelectionInfo() => selectedImage != null ? Describe(selectedImage)
            : routes.Count > 0 ? $"{routes.Count} Trasse(n) · {routes.Sum(r => r.Points.Count)} Bild(er) mit GPS" : "Kein Bild ausgewählt";

        /// <summary>
        /// Rebuilds the virtual routes (trunk + branches) from all GPS positions, numbers the images along them and
        /// (optionally) reorders the image list to follow the routes. Must run after every GPS or list change,
        /// because map markers address images by their list index.
        /// </summary>
        private void RebuildRoutes(bool fit = false)
        {
            var snapshot = images.ToList();
            var points = snapshot.Select((img, i) => (img, i)).Where(x => x.img.HasGpsData)
                .Select(x => new RoutePoint(x.i, x.img.Latitude!.Value, x.img.Longitude!.Value)).ToList();
            routes = RouteBuilder.Build(points, RouteGapSlider.Value, BranchMinSlider.Value);
            // Point ids are snapshot indices; keep the items because the list may be reordered below.
            pointItems = points.ToDictionary(p => p, p => snapshot[p.Id]);

            var position = new Dictionary<ImageItem, (int Route, int Index, bool Branch)>();
            foreach (var route in routes)
                for (int k = 0; k < route.Points.Count; k++)
                    position[pointItems[route.Points[k]]] = (route.Number, k + 1, route.IsBranchPoint(route.Points[k]));
            foreach (var img in images)
            {
                var (route, index, branch) = position.TryGetValue(img, out var p) ? p : (0, 0, false);
                img.SetRoute(route, index, branch);
            }

            if (SortByRouteCheckBox.IsChecked == true)
            {
                // Stable: images without GPS keep their relative order at the end.
                var ordered = images.OrderBy(i => i.RouteNumber == 0 ? int.MaxValue : i.RouteNumber).ThenBy(i => i.RouteIndex).ToList();
                for (int target = 0; target < ordered.Count; target++)
                {
                    int current = images.IndexOf(ordered[target]);
                    if (current != target) images.Move(current, target);
                }
            }

            var routeData = routes.Select(r => new
            {
                number = r.Number,
                lengthMeters = Math.Round(r.LengthMeters),
                points = r.Trunk.Select(p => new[] { p.Latitude, p.Longitude }),
                branches = r.Branches.Select(b => b.Points.Select(p => new[] { p.Latitude, p.Longitude })
                    .Prepend(new[] { b.AttachLatitude, b.AttachLongitude }))
            });
            _ = RunMapScriptAsync("setRoutes(" + System.Text.Json.JsonSerializer.Serialize(routeData) + ");");
            UpdateAllMarkersOnMap(fit);
            ApplyRoadLayer();
            if (selectedImage == null) MapInfoText.Text = SelectionInfo();
        }

        // ---------- Trassen an Wege anlegen (Map-Matching) ----------

        /// <summary>Road match of one route: the trunk and each branch separately (branch requests start at the attach point).</summary>
        private sealed record RouteRoadMatch(RoadMatch Trunk, IReadOnlyList<RoadMatch> Branches);

        private Dictionary<RoutePoint, ImageItem> pointItems = [];
        private readonly Dictionary<string, RouteRoadMatch> roadCache = [];
        private readonly Dictionary<ImageItem, double?> roadDistance = [];
        private string? roadConsentServer; // consent is per server and session

        private static string Coordinates(IEnumerable<(double Lat, double Lon)> points) =>
            string.Join(";", points.Select(p => $"{Js(p.Lat)},{Js(p.Lon)}"));

        /// <summary>Cache key: exact trunk/branch geometry plus all matching settings, so any change needs a new explicit request.</summary>
        private string RoadKey(Route route) =>
            string.Join("|", settings.RoadMatchUrl, settings.RoadMatchProfile, settings.RoadMatchMaxDeviationMeters.ToString(CultureInfo.InvariantCulture),
                "T:" + Coordinates(route.Trunk.Select(p => (p.Latitude, p.Longitude))),
                string.Join("", route.Branches.Select(b => "B:" + Coordinates(b.Points.Select(p => (p.Latitude, p.Longitude))
                    .Prepend((b.AttachLatitude, b.AttachLongitude))))));

        private static IReadOnlyList<RoutePoint> BranchRequest(RouteBranch branch) =>
            branch.Points.Prepend(new RoutePoint(-1, branch.AttachLatitude, branch.AttachLongitude)).ToList();

        private void UpdateRoadServerText()
        {
            string host = Uri.TryCreate(settings.RoadMatchUrl, UriKind.Absolute, out var uri) ? uri.Host : settings.RoadMatchUrl;
            RoadServerText.Text = $"Server: {host}{(settings.RoadMatchUrl.TrimEnd('/') == RoadMatcher.DefaultServer ? " (öffentlicher Demo-Server, fair use)" : "")}";
        }

        /// <summary>Shows cached matches for the current routes; never contacts the server.</summary>
        private void ApplyRoadLayer()
        {
            roadDistance.Clear();
            var data = new List<object>();
            foreach (var route in routes)
            {
                if (!roadCache.TryGetValue(RoadKey(route), out var match)) continue;
                var segments = new List<(bool OnRoad, bool Branch, IReadOnlyList<(double Lat, double Lon)> Points)>();

                for (int k = 0; k < route.Trunk.Count && k < match.Trunk.Deviations.Count; k++)
                    roadDistance[pointItems[route.Trunk[k]]] = match.Trunk.Deviations[k];
                segments.AddRange(match.Trunk.Segments.Select(g => (g.OnRoad, false, g.Points)));

                for (int b = 0; b < route.Branches.Count && b < match.Branches.Count; b++)
                {
                    var branch = route.Branches[b]; var branchMatch = match.Branches[b];
                    // Deviation index 0 is the attach point on the trunk, not a photo.
                    for (int k = 0; k < branch.Points.Count && k + 1 < branchMatch.Deviations.Count; k++)
                        roadDistance[pointItems[branch.Points[k]]] = branchMatch.Deviations[k + 1];
                    segments.AddRange(branchMatch.Segments.Select(g => (g.OnRoad, true, g.Points)));
                    // A connection ends at the photo (e.g. inside the property), not where the road does.
                    var end = branchMatch.Segments.LastOrDefault()?.Points[^1];
                    var photo = (branch.Points[^1].Latitude, branch.Points[^1].Longitude);
                    if (end is { } last && last != photo) segments.Add((false, true, new[] { last, photo }));
                }

                data.Add(new
                {
                    number = route.Number,
                    segments = segments.Where(g => g.Points.Count >= 2)
                        .Select(g => new { onRoad = g.OnRoad, branch = g.Branch, points = g.Points.Select(p => new[] { p.Lat, p.Lon }) })
                });
            }
            _ = RunMapScriptAsync("setRoadRoutes(" + System.Text.Json.JsonSerializer.Serialize(data) + ");");
        }

        private async void RoadMatch_Click(object sender, RoutedEventArgs e)
        {
            var pending = routes.Where(r => r.Points.Count >= 2 && !roadCache.ContainsKey(RoadKey(r))).ToList();
            if (routes.All(r => r.Points.Count < 2)) { MapStatusText.Text = "Keine Trasse mit mindestens zwei GPS-Punkten vorhanden."; return; }
            if (pending.Count == 0) { MapStatusText.Text = "Alle Trassen sind bereits an Wege angelegt."; ApplyRoadLayer(); return; }
            if (!RoadMatcher.IsAllowedServer(settings.RoadMatchUrl, out var server))
            { MapStatusText.Text = "Routing-Server ungültig – bitte über ⚙ einstellen."; return; }

            int requests = pending.Sum(r => (r.Trunk.Count >= 2 ? 1 : 0) + r.Branches.Count);
            if (roadConsentServer != server.AbsoluteUri)
            {
                bool demo = settings.RoadMatchUrl.TrimEnd('/') == RoadMatcher.DefaultServer;
                var answer = MessageBox.Show(this,
                    $"Die Koordinaten von {pending.Sum(r => r.Points.Count)} Fotopunkten ({pending.Count} Trasse(n), {pending.Sum(r => r.Branches.Count)} Abzweig(e), mind. {requests} Anfrage(n)) werden an\n{server.Host}\ngesendet, um den Verlauf an Straßen und Wege anzulegen.\n\n" +
                    (demo ? "Das ist der öffentliche Demo-Server des FOSSGIS e.V.: nur faire, gelegentliche Nutzung, höchstens 1 Anfrage pro Sekunde, keine Verfügbarkeitszusage. Für den Firmeneinsatz einen eigenen Server über ⚙ eintragen.\n\n" : "") +
                    "Die GPS-Daten der Fotos werden nicht verändert. Fortfahren?",
                    "Trassen an Wege anlegen", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
                roadConsentServer = server.AbsoluteUri;
            }

            var matcher = new RoadMatcher(RoadMatcher.SharedClient, server, settings.RoadMatchProfile, settings.RoadMatchMaxDeviationMeters);
            RoadMatchButton.IsEnabled = false;
            var notes = new List<string>();
            try
            {
                foreach (var route in pending)
                {
                    string key = RoadKey(route);
                    MapStatusText.Text = $"Trasse {route.Number} wird an Wege angelegt … ({pending.IndexOf(route) + 1}/{pending.Count})";
                    var trunk = await matcher.MatchAsync(route.Trunk, CancellationToken.None);
                    if (trunk.Note != null) notes.Add($"Trasse {route.Number}: {trunk.Note}");
                    var branches = new List<RoadMatch>();
                    foreach (var branch in route.Branches)
                    {
                        MapStatusText.Text = $"Trasse {route.Number}: Abzweig {branches.Count + 1}/{route.Branches.Count} wird angelegt …";
                        branches.Add(await matcher.MatchAsync(BranchRequest(branch), CancellationToken.None));
                    }
                    roadCache[key] = new RouteRoadMatch(trunk, branches);
                    ApplyRoadLayer();
                }
                MapStatusText.Text = notes.Count == 0 ? $"{pending.Count} Trasse(n) an Wege angelegt." : string.Join(" ", notes);
            }
            catch (RoadMatchException ex)
            {
                // Stop on transport/server errors instead of retrying: protects the public server.
                MapStatusText.Text = "Anlegen an Wege abgebrochen: " + ex.Message;
            }
            finally
            {
                RoadMatchButton.IsEnabled = true;
                if (selectedImage != null) MapInfoText.Text = Describe(selectedImage);
            }
        }

        private void RoadSettings_Click(object sender, RoutedEventArgs e)
        {
            if (new RoadMatchSettingsWindow(settings) { Owner = this }.ShowDialog() == true)
            {
                UpdateRoadServerText();
                ApplyRoadLayer(); // settings are part of the cache key: old results disappear until matched again
            }
        }

        private void UpdateAllMarkersOnMap(bool fit = false)
        {
            var data=images.Select((img,index)=>new { id=index, lat=img.Latitude, lng=img.Longitude })
                .Where(x=>x.lat.HasValue && x.lng.HasValue && PixelGeometry.ValidGps(x.lat.Value,x.lng.Value));
            _ = RunMapScriptAsync("addImageMarkers(" + System.Text.Json.JsonSerializer.Serialize(data) + "," + (fit ? "true" : "false") + ");");
        }

        private void ApplyLayerVisibility()
        {
            // Checked events fire while InitializeComponent is still creating the controls;
            // the map applies the state itself on its "ready" message.
            if (!mapReady) return;
            _ = RunMapScriptAsync($"setLayerVisible('routes',{(ShowRoutesCheckBox.IsChecked == true ? "true" : "false")});" +
                                  $"setLayerVisible('road',{(ShowRoadCheckBox.IsChecked == true ? "true" : "false")});" +
                                  $"setLayerVisible('images',{(ShowImagesCheckBox.IsChecked == true ? "true" : "false")});");
        }

        private void Layer_Changed(object sender, RoutedEventArgs e) => ApplyLayerVisibility();

        private void RouteSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (settings is null) return; // fired during InitializeComponent
            settings.SortImagesByRoute = SortByRouteCheckBox.IsChecked == true;
            RebuildRoutes();
        }

        private void RouteGap_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (settings is null) return;
            settings.RouteMaxGapMeters = RouteGapSlider.Value;
            RebuildRoutes();
        }

        private void BranchMin_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (settings is null) return; // fired during InitializeComponent
            settings.RouteBranchMinMeters = BranchMinSlider.Value;
            RebuildRoutes();
        }

        private async void UndoImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ImageItem item } || !item.CanUndo) return;
            string? replaced = item.Undo();
            if (replaced != null) imageService.InvalidateThumbnailCache(replaced);
            if (!File.Exists(item.FilePath))
                MessageBox.Show($"Die vorherige Datei existiert nicht mehr:\n{item.FilePath}", "Rückgängig", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                item.Thumbnail = await imageService.CreateThumbnailAsync(item.FilePath);
            if (selectedImage == item) SelectImage(item);
            RebuildRoutes();
            MapStatusText.Text = $"Rückgängig: {item.FileName} zeigt wieder den vorherigen Stand. Die zuvor gespeicherte Kopie bleibt erhalten: {replaced}";
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

        private async void LoadImages(string[] filePaths)
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

                        // Thumbnail erstellen mit Fehlerbehandlung (jetzt cached)
                        try
                        {
                            imageItem.Thumbnail = await imageService.CreateThumbnailAsync(filePath);
                            
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

                        await Task.Run(() => ReadExifData(imageItem));
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
                RebuildRoutes(fit: true);
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

                if (gpsDirectory != null && gpsDirectory.TryGetGeoLocation(out var location))
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
                SelectImage(imageItem);
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = selectedImage != null;
            bool hasImages = images.Count > 0;

            if (SaveAllButton != null)
                SaveAllButton.IsEnabled = hasImages;
            if (CommitGPSToAllPicturesButton != null)
                CommitGPSToAllPicturesButton.IsEnabled = hasImages && hasCoordinates;
            if (ApplyGpsButton != null)
                ApplyGpsButton.IsEnabled = hasSelection && hasCoordinates;
        }

        private async void EditImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                try
                {
                    var editor = new ImageEditorWindow(
                        imageItem.FilePath,
                        imageItem.Latitude,
                        imageItem.Longitude)
                    {
                        Owner = this
                    };

                    if (editor.ShowDialog() == true && editor.EditedImageBytes != null)
                    {
                        // Altes Thumbnail freigeben und Cache invalidieren
                        if (imageItem.Thumbnail != null)
                        {
                            imageService.InvalidateThumbnailCache(imageItem.FilePath);
                            imageItem.Thumbnail = null;
    
                        }

                        string newPath = imageService.SaveEditedImage(
                            editor.EditedImageBytes,
                            Path.ChangeExtension(imageItem.FileName, editor.EditedExtension),
                            settings.OutputFolder);

                        if (imageItem.HasGpsData && editor.EditedExtension is not ".bmp")
                        {
                            imageService.WriteGpsToImage(newPath, imageItem.Latitude!.Value, imageItem.Longitude!.Value);
                        }

                        imageItem.PushHistory();
                    imageItem.FilePath = newPath;
                        
                        
                        
                        // Neues Thumbnail erstellen (wird automatisch gecached)
                        imageItem.Thumbnail = await imageService.CreateThumbnailAsync(newPath);

                        MessageBox.Show($"Bearbeitetes Bild gespeichert:\n{newPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    MessageBox.Show($"Fehler beim Bearbeiten: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            int savedCount = 0;
            int errorCount = 0;

            foreach (var img in images)
            {
                try
                {
                    // Preserve each image's own coordinates; do NOT overwrite with currentLatitude/currentLongitude.
                    double? lat = img.Latitude;
                    double? lon = img.Longitude;

                    string oldPath = img.FilePath;
                    string newPath = imageService.SaveSingleImage(img.FilePath, settings.OutputFolder, lat, lon);

                    // Invalidiere Cache für alten Pfad
                    imageService.InvalidateThumbnailCache(oldPath);

                    img.PushHistory();
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

            RebuildRoutes();
            MessageBox.Show($"{savedCount} Bild(er) erfolgreich gespeichert!\n{errorCount} Fehler.",
                "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void CommitGPSToAllPictures_Click(object sender, RoutedEventArgs e)
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

                    string oldPath = img.FilePath;
                    string newPath = imageService.SaveSingleImage(img.FilePath, settings.OutputFolder, lat, lon);

                    // Invalidiere Cache für alten Pfad
                    imageService.InvalidateThumbnailCache(oldPath);

                    img.PushHistory();
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

            RebuildRoutes();
            MessageBox.Show($"{savedCount} Bild(er) erfolgreich gespeichert!\n{errorCount} Fehler.",
                "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void SaveSingleImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageItem imageItem)
            {
                try
                {
                    double? lat = imageItem.Latitude ?? (hasCoordinates ? (double?)currentLatitude : null);
                    double? lon = imageItem.Longitude ?? (hasCoordinates ? (double?)currentLongitude : null);

                    string oldPath = imageItem.FilePath;
                    string newPath = imageService.SaveSingleImage(imageItem.FilePath, settings.OutputFolder, lat, lon);

                    // Invalidiere Cache für alten Pfad
                    imageService.InvalidateThumbnailCache(oldPath);
                    
                    imageItem.PushHistory();
                    imageItem.FilePath = newPath;
                    if (lat.HasValue && lon.HasValue)
                    {
                        imageItem.Latitude = lat.Value;
                        imageItem.Longitude = lon.Value;
                    }
                    imageItem.Thumbnail = await imageService.CreateThumbnailAsync(newPath);

                    RebuildRoutes();
                    MessageBox.Show($"Bild gespeichert:\n{newPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern von {imageItem.FileName}: {ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    if (gpsDirectory != null && gpsDirectory.TryGetGeoLocation(out var location))
                    {
                        currentLatitude = location.Latitude;
                        currentLongitude = location.Longitude;
                        hasCoordinates = true;
                        CurrentGpsText.Text = $"Lat: {location.Latitude:F6}, Lng: {location.Longitude:F6}";

                        _ = RunMapScriptAsync($"setCurrentMarker({Js(location.Latitude)},{Js(location.Longitude)});");

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
                    // Check if image already has GPS data and ask for confirmation
                    if (selectedImage.HasGpsData)
                    {
                        var result = MessageBox.Show(
                            $"Das Bild '{selectedImage.FileName}' hat bereits GPS-Koordinaten:\n\n" +
                            $"Aktuell: Lat {selectedImage.Latitude:F6}, Lng {selectedImage.Longitude:F6}\n" +
                            $"Neu: Lat {currentLatitude:F6}, Lng {currentLongitude:F6}\n\n" +
                            "Möchten Sie die vorhandenen Koordinaten ersetzen?",
                            "GPS-Daten ersetzen?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                            return;
                    }

                    string oldPath = selectedImage.FilePath;
                    
                    string newPath = imageService.SaveSingleImage(
                        selectedImage.FilePath,
                        settings.OutputFolder,
                        currentLatitude,
                        currentLongitude);

                    // Invalidiere Cache für alten Pfad
                    imageService.InvalidateThumbnailCache(oldPath);
                    
                    selectedImage.PushHistory();
                    selectedImage.FilePath = newPath;
                    selectedImage.Latitude = currentLatitude;
                    selectedImage.Longitude = currentLongitude;
                    selectedImage.Thumbnail = imageService.CreateThumbnail(newPath);

                    RebuildRoutes();
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
                    RebuildRoutes();
                    UpdateButtonStates();
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
            if (result != MessageBoxResult.Yes) return;

            selectedImage = null;
            images.Clear();
            _ = RunMapScriptAsync("addImageMarkers([]);clearSelectedMarker();clearCurrentMarker();");
            UpdateButtonStates();
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

            string script = $"updateGrid({(enabled ? "true" : "false")}, {Js(size)});";
            _ = RunMapScriptAsync(script);
        }

        private void AiMetadata_Click(object sender, RoutedEventArgs e)
        {
            if(images.Count==0) { MessageBox.Show(this,"Bitte zuerst Bilder laden."); return; }
            new AiMetadataWindow(images.ToList(), settings) { Owner=this }.Show();
        }

        private void ShowLicensesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseWindow = new LicenseViewer
                {
                    Owner = this
                };
                licenseWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen des Lizenz-Fensters: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
