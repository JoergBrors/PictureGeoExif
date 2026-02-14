using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.Fonts;
using System.Linq;
using System.Collections.Generic;
using MetadataExtractor;

namespace PictureExifclone
{
    public partial class ImageEditorWindow : Window
    {
        private string originalFilePath;
        private string currentTempFilePath;
        private readonly string tempDirectory;
        private readonly LinkedList<string> tempFileHistory = new LinkedList<string>();
        private const int MAX_HISTORY_STEPS = 10;
        
        private double? latitude;
        private double? longitude;
        private bool hasChanges = false;
        
        private double currentZoom = 1.0;
        private bool isSelecting = false;
        private System.Windows.Point startPoint;
        private System.Windows.Point? clickPoint = null;
        
        private double imageOriginalWidth = 0;
        private double imageOriginalHeight = 0;

        public byte[]? EditedImageBytes { get; private set; }
        
        private double savedScrollOffsetX = 0;
        private double savedScrollOffsetY = 0;
        private bool restoreScrollPosition = false;

        public ImageEditorWindow(string filePath, double? lat = null, double? lon = null)
        {
            InitializeComponent();
            
            originalFilePath = filePath;
            latitude = lat;
            longitude = lon;

            // Erstelle dediziertes Temp-Verzeichnis für diese Sitzung
            tempDirectory = Path.Combine(Path.GetTempPath(), $"imgedit_{Guid.NewGuid()}");
            System.IO.Directory.CreateDirectory(tempDirectory);

            System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            System.Diagnostics.Debug.WriteLine("BILDEDITOR START (Temp-Dateien-System)");
            System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            System.Diagnostics.Debug.WriteLine($"[INIT] Temp-Verzeichnis: {tempDirectory}");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");
            }

            try
            {
                // Erstelle initiale Kopie (Version 0)
                CreateInitialTempFile();
                
                System.Diagnostics.Debug.WriteLine($"[INIT] Original: {filePath}");
                System.Diagnostics.Debug.WriteLine($"[INIT] Aktuelle Version: {currentTempFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Initialisieren: {ex.Message}");
                throw;
            }

            LoadAndDisplayImage();
            
            ImageScrollViewer.PreviewMouseWheel += ImageScrollViewer_PreviewMouseWheel;

            Loaded += (s, e) =>
            {
                UpdateDimensions();
                CenterImage();
            };
            
            Closed += (s, e) =>
            {
                CleanupTempFiles();
            };
            
            UpdateUndoButton();
        }

        private void CreateInitialTempFile()
        {
            try
            {
                string ext = Path.GetExtension(originalFilePath);
                currentTempFilePath = Path.Combine(tempDirectory, $"version_000{ext}");
                
                File.Copy(originalFilePath, currentTempFilePath, true);
                tempFileHistory.AddLast(currentTempFilePath);
                
                using (var testImg = SixLabors.ImageSharp.Image.Load(currentTempFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[TEMP] Initiale Version erstellt: {Path.GetFileName(currentTempFilePath)} ({testImg.Width}x{testImg.Height})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Erstellen der initialen Temp-Datei: {ex.Message}");
                throw new InvalidOperationException($"Fehler beim Erstellen der Arbeitsdatei: {ex.Message}", ex);
            }
        }

        private void CreateNewTempVersion()
        {
            try
            {
                if (string.IsNullOrEmpty(currentTempFilePath) || !File.Exists(currentTempFilePath))
                    return;

                string ext = Path.GetExtension(originalFilePath);
                string newTempFile = Path.Combine(tempDirectory, $"version_{tempFileHistory.Count:000}{ext}");
                
                // Kopiere aktuelle Version zur neuen Version
                File.Copy(currentTempFilePath, newTempFile, true);
                
                currentTempFilePath = newTempFile;
                tempFileHistory.AddLast(newTempFile);

                // Alte Versionen löschen wenn Limit erreicht
                while (tempFileHistory.Count > MAX_HISTORY_STEPS)
                {
                    var oldest = tempFileHistory.First;
                    if (oldest != null && oldest.Value != currentTempFilePath)
                    {
                        tempFileHistory.RemoveFirst();
                        try
                        {
                            if (File.Exists(oldest.Value))
                            {
                                File.Delete(oldest.Value);
                                System.Diagnostics.Debug.WriteLine($"[TEMP] Alte Version gelöscht: {Path.GetFileName(oldest.Value)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Löschen alter Version: {ex.Message}");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[TEMP] Neue Version erstellt: {Path.GetFileName(newTempFile)} (Gesamt: {tempFileHistory.Count})");
                UpdateUndoButton();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Erstellen neuer Version: {ex.Message}");
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CLEANUP] Beginne Aufräumen...");

                if (System.IO.Directory.Exists(tempDirectory))
                {
                    System.IO.Directory.Delete(tempDirectory, true);
                    System.Diagnostics.Debug.WriteLine($"[CLEANUP] Temp-Verzeichnis gelöscht: {tempDirectory}");
                }

                System.Diagnostics.Debug.WriteLine("[CLEANUP] Fertig");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Aufräumen: {ex.Message}");
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (tempFileHistory.Count <= 1) // Mindestens 2 Versionen nötig (current + previous)
                return;

            try
            {
                // Aktuelle Version aus History entfernen (aber Datei behalten)
                var current = tempFileHistory.Last;
                if (current == null)
                    return;

                tempFileHistory.RemoveLast();

                // Zur vorherigen Version wechseln
                var previous = tempFileHistory.Last;
                if (previous != null)
                {
                    currentTempFilePath = previous.Value;
                    System.Diagnostics.Debug.WriteLine($"[UNDO] Wechsle zu vorheriger Version: {Path.GetFileName(currentTempFilePath)}");
                    System.Diagnostics.Debug.WriteLine($"[UNDO] Verbleibende Versionen: {tempFileHistory.Count}");

                    LoadAndDisplayImage();
                    hasChanges = true;
                    StatusText.Text = "Letzte Änderung rückgängig gemacht";
                    UpdateUndoButton();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Rückgängig machen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUndoButton()
        {
            if (UndoButton != null)
            {
                int canUndoSteps = tempFileHistory.Count - 1; // -1 weil current nicht gezählt wird
                UndoButton.IsEnabled = canUndoSteps > 0;
                UndoButton.Content = canUndoSteps > 0 ? $"? UNDO ({canUndoSteps})" : "? UNDO";
            }
        }

        private void LoadAndDisplayImage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("-".PadRight(80, '-'));
                System.Diagnostics.Debug.WriteLine($"[LOAD] Lade Bild aus Version: {Path.GetFileName(currentTempFilePath)}");
                
                double previousZoom = currentZoom;
                System.Diagnostics.Debug.WriteLine($"[LOAD] Vorheriger Zoom: {previousZoom:F2}");
                
                // WICHTIG: Source auf null setzen und GC forcieren um Cache zu leeren
                DisplayImage.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                if (!File.Exists(currentTempFilePath))
                {
                    throw new FileNotFoundException($"Arbeitsdatei nicht gefunden: {currentTempFilePath}");
                }

                // WICHTIG: Lade Bild-Daten komplett in Memory, um Cache-Probleme zu vermeiden
                byte[] imageData = File.ReadAllBytes(currentTempFilePath);
                System.Diagnostics.Debug.WriteLine($"[LOAD] Datei gelesen: {imageData.Length} bytes");
                
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Lade komplett in Memory
                bitmap.CreateOptions = BitmapCreateOptions.None; // KEINE IgnoreImageCache (verursacht Fehler)
                
                using (var memoryStream = new MemoryStream(imageData))
                {
                    memoryStream.Position = 0;
                    bitmap.StreamSource = memoryStream;
                    bitmap.EndInit();
                }
                
                bitmap.Freeze(); // Macht Bitmap thread-safe und verhindert weitere Änderungen
                
                System.Diagnostics.Debug.WriteLine($"[LOAD] Bitmap erstellt: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
                
                DisplayImage.Source = bitmap;
                
                var newWidth = bitmap.PixelWidth;
                var newHeight = bitmap.PixelHeight;
                
                if (imageOriginalWidth > 0 && (newWidth != imageOriginalWidth || newHeight != imageOriginalHeight))
                {
                    System.Diagnostics.Debug.WriteLine($"[LOAD] WARNUNG: Bildgröße hat sich geändert!");
                    System.Diagnostics.Debug.WriteLine($"[LOAD]   Alt: {imageOriginalWidth}x{imageOriginalHeight}");
                    System.Diagnostics.Debug.WriteLine($"[LOAD]   Neu: {newWidth}x{newHeight}");
                    
                    previousZoom = 0; // Erzwinge Auto-Zoom bei Größenänderung
                    restoreScrollPosition = false; // Kein Restore bei Größenänderung
                }
                
                imageOriginalWidth = newWidth;
                imageOriginalHeight = newHeight;

                ImageInfoText.Text = $"{Path.GetFileName(originalFilePath)}\n{imageOriginalWidth} x {imageOriginalHeight} px\nVersion: {tempFileHistory.Count}/{MAX_HISTORY_STEPS}";
                GpsInfoText.Text = (latitude.HasValue && longitude.HasValue) 
                    ? $"Lat: {latitude.Value:F6}\nLon: {longitude.Value:F6}" 
                    : "Keine GPS-Daten";
                
                UpdateDimensions();
                
                if (IsLoaded && previousZoom > 0)
                {
                    currentZoom = previousZoom;
                    ImageScaleTransform.ScaleX = currentZoom;
                    ImageScaleTransform.ScaleY = currentZoom;
                    ZoomText.Text = $"{(int)(currentZoom * 100)}%";
                    System.Diagnostics.Debug.WriteLine($"[LOAD] Zoom wiederhergestellt: {currentZoom:F2}");
                    
                    if (restoreScrollPosition)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            ImageScrollViewer.ScrollToHorizontalOffset(savedScrollOffsetX);
                            ImageScrollViewer.ScrollToVerticalOffset(savedScrollOffsetY);
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Scroll wiederhergestellt: X={savedScrollOffsetX:F1}, Y={savedScrollOffsetY:F1}");
                            restoreScrollPosition = false;
                        }, System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        CenterImage();
                    }
                }
                else if (!restoreScrollPosition)
                {
                    System.Diagnostics.Debug.WriteLine("[LOAD] Initialer Load - berechne Auto-Zoom");
                    Dispatcher.InvokeAsync(() => 
                    {
                        if (ImageScrollViewer.ActualWidth > 0 && imageOriginalWidth > 0)
                        {
                            // Berechne Zoom so, dass das GESAMTE Bild sichtbar ist (mit Padding)
                            var availableWidth = ImageScrollViewer.ActualWidth - 40;  // 20px Padding links+rechts
                            var availableHeight = ImageScrollViewer.ActualHeight - 40; // 20px Padding oben+unten
                            
                            var scaleX = availableWidth / imageOriginalWidth;
                            var scaleY = availableHeight / imageOriginalHeight;
                            
                            // Wähle den KLEINEREN Zoom-Faktor, damit ALLES passt
                            currentZoom = Math.Max(0.1, Math.Min(Math.Min(scaleX, scaleY), 1.0));
                            
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Auto-Zoom berechnet:");
                            System.Diagnostics.Debug.WriteLine($"[LOAD]   Verfügbar: {availableWidth:F0}x{availableHeight:F0}");
                            System.Diagnostics.Debug.WriteLine($"[LOAD]   Original: {imageOriginalWidth}x{imageOriginalHeight}");
                            System.Diagnostics.Debug.WriteLine($"[LOAD]   scaleX={scaleX:F4}, scaleY={scaleY:F4}");
                            System.Diagnostics.Debug.WriteLine($"[LOAD]   ? Zoom={currentZoom:F4} ({currentZoom*100:F1}%)");
                        }
                        else
                        {
                            currentZoom = 1.0;
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Standard-Zoom: 1.0");
                        }
                        
                        ImageScaleTransform.ScaleX = currentZoom;
                        ImageScaleTransform.ScaleY = currentZoom;
                        ZoomText.Text = $"{(int)(currentZoom * 100)}%";
                        
                        CenterImage();
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LOAD] Überspringe Auto-Zoom (Scroll-Wiederherstellung aktiv)");
                }
                
                StatusText.Text = $"Bild geladen - Version {tempFileHistory.Count} von {MAX_HISTORY_STEPS}";
                System.Diagnostics.Debug.WriteLine("[LOAD] Erfolgreich geladen");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler in LoadAndDisplayImage: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Fehler beim Laden: {ex.Message}\n\nDetails: {ex.GetType().Name}", 
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDimensions()
        {
            if (imageOriginalWidth > 0 && DisplayImage.Source != null)
            {
                // WICHTIG: Setze KEINE feste Größe für DisplayImage!
                // Lasse WPF das Layout automatisch berechnen, um Clipping zu vermeiden
                DisplayImage.Width = double.NaN;  // Auto
                DisplayImage.Height = double.NaN; // Auto
                
                // AUCH Container auf Auto setzen, damit er sich dem Bild anpasst
                ImageContainer.Width = double.NaN;  // Auto
                ImageContainer.Height = double.NaN; // Auto
                
                // PreviewCanvas wird an DisplayImage.ActualWidth/Height gebunden (siehe XAML)
                // Dadurch hat es immer die gleiche Größe wie das angezeigte Bild
                
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Original-Bild={imageOriginalWidth}x{imageOriginalHeight}");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] DisplayImage=Auto x Auto (kein Clipping!)");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Container=Auto x Auto (passt sich an!)");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] PreviewCanvas wird an DisplayImage gebunden");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Zoom={currentZoom:F2}");
            }
        }

        private void CenterImage()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (ImageScrollViewer.ScrollableWidth > 0)
                    ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.ScrollableWidth / 2);
                if (ImageScrollViewer.ScrollableHeight > 0)
                    ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.ScrollableHeight / 2);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            currentZoom = Math.Min(currentZoom * 1.2, 10.0);
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            currentZoom = Math.Max(currentZoom / 1.2, 0.1);
            ApplyZoom();
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (ImageScrollViewer.ActualWidth > 0 && imageOriginalWidth > 0)
            {
                // Berechne Zoom so, dass das GESAMTE Bild sichtbar ist
                var availableWidth = ImageScrollViewer.ActualWidth - 40;
                var availableHeight = ImageScrollViewer.ActualHeight - 40;
                
                var scaleX = availableWidth / imageOriginalWidth;
                var scaleY = availableHeight / imageOriginalHeight;
                
                // Wähle den KLEINEREN Faktor, damit alles passt
                currentZoom = Math.Max(0.1, Math.Min(Math.Min(scaleX, scaleY), 10.0));
                
                System.Diagnostics.Debug.WriteLine($"[ZOOM FIT] Verfügbar: {availableWidth:F0}x{availableHeight:F0}");
                System.Diagnostics.Debug.WriteLine($"[ZOOM FIT] Original: {imageOriginalWidth}x{imageOriginalHeight}");
                System.Diagnostics.Debug.WriteLine($"[ZOOM FIT] scaleX={scaleX:F4}, scaleY={scaleY:F4}");
                System.Diagnostics.Debug.WriteLine($"[ZOOM FIT] ? Zoom={currentZoom:F4}");
                
                ApplyZoom();
                CenterImage();
            }
        }

        private void ApplyZoom()
        {
            ImageScaleTransform.ScaleX = currentZoom;
            ImageScaleTransform.ScaleY = currentZoom;
            ZoomText.Text = $"{(int)(currentZoom * 100)}%";
            System.Diagnostics.Debug.WriteLine($"[ZOOM] Neuer Zoom: {currentZoom:F2}");
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    currentZoom = Math.Min(currentZoom * 1.1, 10.0);
                else
                    currentZoom = Math.Max(currentZoom / 1.1, 0.1);
                ApplyZoom();
                e.Handled = true;
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var displayPosition = e.GetPosition(DisplayImage);
            
            // Berechne die Position im Original-Bild
            // DisplayImage.ActualWidth/Height ist die tatsächlich gerenderte Größe
            // imageOriginalWidth/Height ist die Pixel-Größe des Bildes
            
            // Wichtig: Wenn DisplayImage.ActualWidth/Height 0 oder ungültig sind, verwende imageOriginalWidth/Height
            double actualWidth = DisplayImage.ActualWidth > 0 ? DisplayImage.ActualWidth : imageOriginalWidth;
            double actualHeight = DisplayImage.ActualHeight > 0 ? DisplayImage.ActualHeight : imageOriginalHeight;
            
            double scaleX = imageOriginalWidth / actualWidth;
            double scaleY = imageOriginalHeight / actualHeight;
            
            var imagePosition = new System.Windows.Point(
                displayPosition.X * scaleX,
                displayPosition.Y * scaleY
            );
            
            // Begrenze auf Bildgrenzen
            imagePosition.X = Math.Max(0, Math.Min(imagePosition.X, imageOriginalWidth - 1));
            imagePosition.Y = Math.Max(0, Math.Min(imagePosition.Y, imageOriginalHeight - 1));
            
            startPoint = imagePosition;
            clickPoint = imagePosition;
            
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Display-Position: ({displayPosition.X:F1}, {displayPosition.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Display.ActualSize: {actualWidth:F1}x{actualHeight:F1}");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Scale: {scaleX:F4}x{scaleY:F4}");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] ? Pixel-Position: ({imagePosition.X:F1}, {imagePosition.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Original-Größe: {imageOriginalWidth}x{imageOriginalHeight}");

            if (ModePixelate.IsChecked == true || ModeCrop.IsChecked == true)
            {
                isSelecting = true;
                SelectionRect.Visibility = Visibility.Visible;
                
                // Setze Rechteck auf DISPLAY-Koordinaten (nicht Pixel!)
                Canvas.SetLeft(SelectionRect, displayPosition.X);
                Canvas.SetTop(SelectionRect, displayPosition.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
                
                StatusText.Text = ModeCrop.IsChecked == true 
                    ? "Ziehen Sie Rechteck auf - was behalten werden soll" 
                    : "Ziehen Sie Rechteck auf - Bereich zum Verpixeln";
            }
            else
            {
                ClickMarker.Visibility = Visibility.Visible;
                
                // Marker-Position: Display-Koordinaten (Canvas ist an DisplayImage gebunden)
                Canvas.SetLeft(ClickMarker, displayPosition.X - 10);
                Canvas.SetTop(ClickMarker, displayPosition.Y - 10);
                
                System.Diagnostics.Debug.WriteLine($"[MOUSE] Marker bei Display-Pos: ({displayPosition.X:F1}, {displayPosition.Y:F1})");
                
                StatusText.Text = ModeText.IsChecked == true 
                    ? "Position gewaehlt - Klicken Sie 'TEXT EINFUEGEN'" 
                    : "Position gewaehlt - Klicken Sie 'GEO-DATEN EINFUEGEN'.";
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting || e.LeftButton != MouseButtonState.Pressed) return;
            
            var displayPosition = e.GetPosition(DisplayImage);
            
            double actualWidth = DisplayImage.ActualWidth > 0 ? DisplayImage.ActualWidth : imageOriginalWidth;
            double actualHeight = DisplayImage.ActualHeight > 0 ? DisplayImage.ActualHeight : imageOriginalHeight;
            
            double scaleX = imageOriginalWidth / actualWidth;
            double scaleY = imageOriginalHeight / actualHeight;
            
            var imagePosition = new System.Windows.Point(
                displayPosition.X * scaleX,
                displayPosition.Y * scaleY
            );
            
            // Berechne Display-Koordinaten für das Rechteck
            var startDisplayX = startPoint.X / scaleX;
            var startDisplayY = startPoint.Y / scaleY;
            
            var x = Math.Min(startDisplayX, displayPosition.X);
            var y = Math.Min(startDisplayY, displayPosition.Y);
            
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = Math.Abs(displayPosition.X - startDisplayX);
            SelectionRect.Height = Math.Abs(displayPosition.Y - startDisplayY);
            
            PositionText.Text = $"Position: {(int)imagePosition.X}, {(int)imagePosition.Y}";
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            if (SelectionRect.Visibility == Visibility.Visible && SelectionRect.Width > 10)
            {
                StatusText.Text = ModeCrop.IsChecked == true 
                    ? "Rechteck fertig - Klicken Sie 'BILD ZUSCHNEIDEN'" 
                    : "Rechteck fertig - Klicken Sie 'BEREICH VERPIXELN'.";
            }
        }

        private void ApplyText_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextInput.Text))
            {
                MessageBox.Show("Bitte Text eingeben!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!clickPoint.HasValue)
            {
                MessageBox.Show("Bitte erst auf das Bild klicken um die Position zu waehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[TEXT] Beginne Text-Einfügung");
                
                savedScrollOffsetX = ImageScrollViewer.HorizontalOffset;
                savedScrollOffsetY = ImageScrollViewer.VerticalOffset;
                restoreScrollPosition = true;
                
                // Erstelle neue Version VOR der Bearbeitung
                CreateNewTempVersion();

                var imageX = clickPoint.Value.X;
                var imageY = clickPoint.Value.Y;

                System.Diagnostics.Debug.WriteLine($"[TEXT] Bearbeite Version: {Path.GetFileName(currentTempFilePath)}");
                System.Diagnostics.Debug.WriteLine($"[TEXT] Position: ({imageX:F1}, {imageY:F1})");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    var family = SixLabors.Fonts.SystemFonts.Collection.Families.FirstOrDefault();
                    if (family == null)
                    {
                        MessageBox.Show("Keine Systemschrift gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var font = family.CreateFont((float)FontSizeSlider.Value);
                    var color = TextColorPicker.SelectedColor;
                    var imgColor = SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);

                    img.Mutate(x =>
                    {
                        x.DrawText(TextInput.Text, font, SixLabors.ImageSharp.Color.Black, 
                            new SixLabors.ImageSharp.PointF((float)imageX + 2, (float)imageY + 2));
                        x.DrawText(TextInput.Text, font, imgColor, 
                            new SixLabors.ImageSharp.PointF((float)imageX, (float)imageY));
                    });

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                ClearMarkers();
                LoadAndDisplayImage();
                
                StatusText.Text = "Text eingefuegt!";
                System.Diagnostics.Debug.WriteLine("[TEXT] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
                MessageBox.Show($"Fehler beim Hinzufuegen des Textes: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyGeo_Click(object sender, RoutedEventArgs e)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                MessageBox.Show("Keine GPS-Daten vorhanden!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!clickPoint.HasValue)
            {
                MessageBox.Show("Bitte erst auf das Bild klicken um die Position zu waehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[GEO] Beginne Geo-Daten-Einfügung");
                
                CreateNewTempVersion();

                var imageX = clickPoint.Value.X;
                var imageY = clickPoint.Value.Y;

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    var text = $"Lat: {latitude.Value:F6}  Lon: {longitude.Value:F6}";
                    var family = SixLabors.Fonts.SystemFonts.Collection.Families.FirstOrDefault();
                    if (family == null)
                    {
                        MessageBox.Show("Keine Systemschrift gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var font = family.CreateFont((float)GeoSizeSlider.Value);
                    var color = GeoColorPicker.SelectedColor;
                    var imgColor = SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);

                    img.Mutate(x =>
                    {
                        x.DrawText(text, font, SixLabors.ImageSharp.Color.Black, 
                            new SixLabors.ImageSharp.PointF((float)imageX + 2, (float)imageY + 2));
                        x.DrawText(text, font, imgColor, 
                            new SixLabors.ImageSharp.PointF((float)imageX, (float)imageY));
                    });

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                ClearMarkers();
                LoadAndDisplayImage();
                StatusText.Text = "Geo-Wasserzeichen eingefuegt!";
                System.Diagnostics.Debug.WriteLine("[GEO] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
                MessageBox.Show($"Fehler beim Hinzufuegen der Geo-Daten: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyPixelate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionRect.Visibility != Visibility.Visible || SelectionRect.Width < 10)
            {
                MessageBox.Show("Bitte erst einen Bereich auf dem Bild aufziehen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[PIXELATE] Beginne Verpixelung");
                
                CreateNewTempVersion();

                // Display-Koordinaten vom Canvas
                var displayX = Canvas.GetLeft(SelectionRect);
                var displayY = Canvas.GetTop(SelectionRect);
                var displayWidth = SelectionRect.Width;
                var displayHeight = SelectionRect.Height;

                // Konvertiere zu Pixel-Koordinaten
                double actualWidth = DisplayImage.ActualWidth > 0 ? DisplayImage.ActualWidth : imageOriginalWidth;
                double actualHeight = DisplayImage.ActualHeight > 0 ? DisplayImage.ActualHeight : imageOriginalHeight;
                double scaleX = imageOriginalWidth / actualWidth;
                double scaleY = imageOriginalHeight / actualHeight;
                
                var pixelX = (int)(displayX * scaleX);
                var pixelY = (int)(displayY * scaleY);
                var pixelWidth = (int)(displayWidth * scaleX);
                var pixelHeight = (int)(displayHeight * scaleY);

                System.Diagnostics.Debug.WriteLine($"[PIXELATE] Display-Rechteck: ({displayX:F0}, {displayY:F0}, {displayWidth:F0}x{displayHeight:F0})");
                System.Diagnostics.Debug.WriteLine($"[PIXELATE] Pixel-Rechteck: ({pixelX}, {pixelY}, {pixelWidth}x{pixelHeight})");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    var rect = new SixLabors.ImageSharp.Rectangle(pixelX, pixelY, pixelWidth, pixelHeight);

                    rect.X = Math.Max(0, Math.Min(rect.X, img.Width));
                    rect.Y = Math.Max(0, Math.Min(rect.Y, img.Height));
                    rect.Width = Math.Min(rect.Width, img.Width - rect.X);
                    rect.Height = Math.Min(rect.Height, img.Height - rect.Y);

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        using (var sub = img.Clone(ctx => ctx.Crop(rect)))
                        {
                            sub.Mutate(x => x.Pixelate((int)PixelSizeSlider.Value));
                            img.Mutate(x => x.DrawImage(sub, new SixLabors.ImageSharp.Point(rect.X, rect.Y), 1f));
                        }
                    }

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                SelectionRect.Visibility = Visibility.Collapsed;
                LoadAndDisplayImage();
                StatusText.Text = "Bereich verpixelt!";
                System.Diagnostics.Debug.WriteLine("[PIXELATE] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
                MessageBox.Show($"Fehler beim Verpixeln: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyCrop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionRect.Visibility != Visibility.Visible || SelectionRect.Width < 10)
            {
                MessageBox.Show("Bitte erst einen Bereich auf dem Bild aufziehen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Moechten Sie das Bild wirklich zuschneiden?\n\nNur der markierte Bereich bleibt erhalten!",
                "Zuschneiden bestaetigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[CROP] Beginne Zuschneiden");
                
                CreateNewTempVersion();

                // Display-Koordinaten vom Canvas
                var displayX = Canvas.GetLeft(SelectionRect);
                var displayY = Canvas.GetTop(SelectionRect);
                var displayWidth = SelectionRect.Width;
                var displayHeight = SelectionRect.Height;

                // Konvertiere zu Pixel-Koordinaten
                double actualWidth = DisplayImage.ActualWidth > 0 ? DisplayImage.ActualWidth : imageOriginalWidth;
                double actualHeight = DisplayImage.ActualHeight > 0 ? DisplayImage.ActualHeight : imageOriginalHeight;
                double scaleX = imageOriginalWidth / actualWidth;
                double scaleY = imageOriginalHeight / actualHeight;
                
                var pixelX = (int)(displayX * scaleX);
                var pixelY = (int)(displayY * scaleY);
                var pixelWidth = (int)(displayWidth * scaleX);
                var pixelHeight = (int)(displayHeight * scaleY);

                System.Diagnostics.Debug.WriteLine($"[CROP] Display-Rechteck: ({displayX:F0}, {displayY:F0}, {displayWidth:F0}x{displayHeight:F0})");
                System.Diagnostics.Debug.WriteLine($"[CROP] Pixel-Rechteck: ({pixelX}, {pixelY}, {pixelWidth}x{pixelHeight})");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[CROP] Bild VOR Crop: {img.Width}x{img.Height}");
                    
                    var rect = new SixLabors.ImageSharp.Rectangle(pixelX, pixelY, pixelWidth, pixelHeight);

                    rect.X = Math.Max(0, Math.Min(rect.X, img.Width));
                    rect.Y = Math.Max(0, Math.Min(rect.Y, img.Height));
                    rect.Width = Math.Min(rect.Width, img.Width - rect.X);
                    rect.Height = Math.Min(rect.Height, img.Height - rect.Y);

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        img.Mutate(x => x.Crop(rect));
                        System.Diagnostics.Debug.WriteLine($"[CROP] Bild NACH Crop: {img.Width}x{img.Height}");
                    }

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                SelectionRect.Visibility = Visibility.Collapsed;
                LoadAndDisplayImage();
                StatusText.Text = "Bild zugeschnitten!";
                System.Diagnostics.Debug.WriteLine("[CROP] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
                MessageBox.Show($"Fehler beim Zuschneiden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[ROTATE] Rotation nach links");
                
                CreateNewTempVersion();

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    img.Mutate(x => x.Rotate(RotateMode.Rotate270));
                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                LoadAndDisplayImage();
                StatusText.Text = "Bild um 90° nach links gedreht";
                System.Diagnostics.Debug.WriteLine("[ROTATE] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Drehen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                System.Diagnostics.Debug.WriteLine("[ROTATE] Rotation nach rechts");
                
                CreateNewTempVersion();

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(currentTempFilePath))
                {
                    img.Mutate(x => x.Rotate(RotateMode.Rotate90));
                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(currentTempFilePath, encoder);
                }

                hasChanges = true;
                LoadAndDisplayImage();
                StatusText.Text = "Bild um 90° nach rechts gedreht";
                System.Diagnostics.Debug.WriteLine("[ROTATE] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Drehen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearMarkers()
        {
            clickPoint = null;
            ClickMarker.Visibility = Visibility.Collapsed;
            SelectionRect.Visibility = Visibility.Collapsed;
        }

        private void ShowExif_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(originalFilePath);
                var exifInfo = string.Join("\n\n", directories.Select(d => 
                    $"[{d.Name}]\n" + string.Join("\n", d.Tags.Select(t => $"{t.Name}: {t.Description}"))));

                var dlg = new Window
                {
                    Title = "EXIF-Daten",
                    Width = 600, Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                dlg.Content = new TextBox
                {
                    Text = exifInfo,
                    IsReadOnly = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Padding = new Thickness(10)
                };

                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EditedImageBytes = File.ReadAllBytes(currentTempFilePath);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (hasChanges)
            {
                var result = MessageBox.Show("Aenderungen verwerfen?", "Bestaetigung",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }
            DialogResult = false;
            Close();
        }
    }
}
