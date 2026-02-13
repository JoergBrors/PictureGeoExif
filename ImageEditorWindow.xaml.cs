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
        private string workingFilePath;
        private double? latitude;
        private double? longitude;
        private bool hasChanges = false;
        
        private double currentZoom = 1.0;
        private bool isSelecting = false;
        private System.Windows.Point startPoint;
        private System.Windows.Point? clickPoint = null;
        
        private double imageOriginalWidth = 0;
        private double imageOriginalHeight = 0;

        // Undo-System: Stack von Backup-Dateien
        private Stack<string> undoStack = new Stack<string>();
        private const int MAX_UNDO_STEPS = 10;

        public byte[]? EditedImageBytes { get; private set; }
        
        // Für Scroll-Position-Wiederherstellung
        private double savedScrollOffsetX = 0;
        private double savedScrollOffsetY = 0;
        private bool restoreScrollPosition = false;

        public ImageEditorWindow(string filePath, double? lat = null, double? lon = null)
        {
            InitializeComponent();
            
            originalFilePath = filePath;
            latitude = lat;
            longitude = lon;

            System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            System.Diagnostics.Debug.WriteLine("BILDEDITOR START");
            System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");
            }

            try
            {
                workingFilePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), 
                    $"edit_{Guid.NewGuid()}{System.IO.Path.GetExtension(filePath)}");
                
                System.Diagnostics.Debug.WriteLine($"[INIT] Original: {filePath}");
                System.Diagnostics.Debug.WriteLine($"[INIT] Working:  {workingFilePath}");
                
                File.Copy(filePath, workingFilePath, true);
                
                if (!File.Exists(workingFilePath))
                {
                    throw new IOException("Temp-Datei konnte nicht erstellt werden");
                }
                
                var fileInfo = new FileInfo(workingFilePath);
                System.Diagnostics.Debug.WriteLine($"[INIT] Temp-Datei OK: {fileInfo.Length} bytes");
                
                // Prüfe Bildgröße VOR dem Laden
                using (var testImg = SixLabors.ImageSharp.Image.Load(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[INIT] ImageSharp-Größe: {testImg.Width}x{testImg.Height}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Erstellen der Temp-Datei: {ex.Message}");
                throw new InvalidOperationException($"Fehler beim Erstellen der Arbeitsdatei: {ex.Message}", ex);
            }

            SaveUndoState();
            LoadAndDisplayImage();
            
            ImageScrollViewer.PreviewMouseWheel += ImageScrollViewer_PreviewMouseWheel;

            Loaded += (s, e) =>
            {
                UpdateDimensions();
                CenterImage();
            };
            
            Closed += (s, e) =>
            {
                try 
                { 
                    System.Diagnostics.Debug.WriteLine("[CLEANUP] Beginne Aufräumen...");
                    
                    if (File.Exists(workingFilePath)) 
                    {
                        File.Delete(workingFilePath);
                        System.Diagnostics.Debug.WriteLine($"[CLEANUP] Temp-Datei gelöscht: {workingFilePath}");
                    }
                    
                    foreach (var undoFile in undoStack)
                    {
                        if (File.Exists(undoFile)) 
                        {
                            File.Delete(undoFile);
                            System.Diagnostics.Debug.WriteLine($"[CLEANUP] Undo-Datei gelöscht: {undoFile}");
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[CLEANUP] Fertig");
                    System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Cleanup: {ex.Message}");
                }
            };
            
            UpdateUndoButton();
        }

        private void SaveUndoState()
        {
            try
            {
                string undoFile = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), 
                    $"undo_{Guid.NewGuid()}{System.IO.Path.GetExtension(workingFilePath)}");
                
                File.Copy(workingFilePath, undoFile, true);
                undoStack.Push(undoFile);

                while (undoStack.Count > MAX_UNDO_STEPS)
                {
                    var oldFile = undoStack.First();
                    var tempStack = new Stack<string>(undoStack.Reverse().Skip(1).Reverse());
                    undoStack = tempStack;
                    
                    if (File.Exists(oldFile)) File.Delete(oldFile);
                }

                System.Diagnostics.Debug.WriteLine($"[UNDO] Backup erstellt, Stack-Größe: {undoStack.Count}");
                UpdateUndoButton();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler beim Speichern des Undo-Status: {ex.Message}");
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                try
                {
                    var undoFile = undoStack.Pop();
                    
                    System.Diagnostics.Debug.WriteLine($"[UNDO] Stelle wieder her von: {undoFile}");
                    
                    if (File.Exists(undoFile))
                    {
                        File.Copy(undoFile, workingFilePath, true);
                        File.Delete(undoFile);
                    }

                    LoadAndDisplayImage();
                    hasChanges = true;
                    StatusText.Text = "Letzte Änderung rückgängig gemacht";
                    UpdateUndoButton();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Rückgängig machen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateUndoButton()
        {
            if (UndoButton != null)
            {
                UndoButton.IsEnabled = undoStack.Count > 0;
                UndoButton.Content = undoStack.Count > 0 ? $"? UNDO ({undoStack.Count})" : "? UNDO";
            }
        }

        private void LoadAndDisplayImage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("-".PadRight(80, '-'));
                System.Diagnostics.Debug.WriteLine("[LOAD] Lade Bild...");
                
                double previousZoom = currentZoom;
                System.Diagnostics.Debug.WriteLine($"[LOAD] Vorheriger Zoom: {previousZoom:F2}");
                
                DisplayImage.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                if (!File.Exists(workingFilePath))
                {
                    throw new FileNotFoundException($"Arbeitsdatei nicht gefunden: {workingFilePath}");
                }

                byte[] imageData = File.ReadAllBytes(workingFilePath);
                System.Diagnostics.Debug.WriteLine($"[LOAD] Datei gelesen: {imageData.Length} bytes");
                
                if (imageData == null || imageData.Length == 0)
                {
                    throw new InvalidOperationException("Bilddaten sind leer");
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.None;
                
                using (var memoryStream = new MemoryStream(imageData))
                {
                    memoryStream.Position = 0;
                    bitmap.StreamSource = memoryStream;
                    bitmap.EndInit();
                }
                
                bitmap.Freeze();
                
                DisplayImage.Source = bitmap;
                
                var newWidth = bitmap.PixelWidth;
                var newHeight = bitmap.PixelHeight;
                
                System.Diagnostics.Debug.WriteLine($"[LOAD] BitmapImage: {newWidth}x{newHeight}");
                
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

                ImageInfoText.Text = $"{System.IO.Path.GetFileName(originalFilePath)}\n{imageOriginalWidth} x {imageOriginalHeight} px";
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
                    
                    // WICHTIG: Stelle ABSOLUTE Scroll-Position wieder her
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
                else if (!restoreScrollPosition)  // ? NUR Auto-Zoom wenn NICHT Scroll wiederhergestellt wird!
                {
                    System.Diagnostics.Debug.WriteLine("[LOAD] Initialer Load - berechne Auto-Zoom");
                    Dispatcher.InvokeAsync(() => 
                    {
                        if (ImageScrollViewer.ActualWidth > 0 && imageOriginalWidth > 0)
                        {
                            var scaleX = (ImageScrollViewer.ActualWidth - 40) / imageOriginalWidth;
                            var scaleY = (ImageScrollViewer.ActualHeight - 40) / imageOriginalHeight;
                            currentZoom = Math.Max(0.1, Math.Min(Math.Min(scaleX, scaleY), 1.0));
                            System.Diagnostics.Debug.WriteLine($"[LOAD] Auto-Zoom berechnet: {currentZoom:F2} (scaleX={scaleX:F2}, scaleY={scaleY:F2})");
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
                
                StatusText.Text = "Bild geladen - Bereit zum Bearbeiten";
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
                DisplayImage.Width = imageOriginalWidth;
                DisplayImage.Height = imageOriginalHeight;
                
                PreviewCanvas.Width = imageOriginalWidth;
                PreviewCanvas.Height = imageOriginalHeight;
                
                ImageContainer.Width = imageOriginalWidth;
                ImageContainer.Height = imageOriginalHeight;
                
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Bild={imageOriginalWidth}x{imageOriginalHeight}");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Preview={PreviewCanvas.Width}x{PreviewCanvas.Height}");
                System.Diagnostics.Debug.WriteLine($"[DIMENSIONS] Container={ImageContainer.Width}x{ImageContainer.Height}");
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
                var scaleX = (ImageScrollViewer.ActualWidth - 40) / imageOriginalWidth;
                var scaleY = (ImageScrollViewer.ActualHeight - 40) / imageOriginalHeight;
                currentZoom = Math.Max(0.1, Math.Min(Math.Min(scaleX, scaleY), 10.0));
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

            // GetPosition gibt Position relativ zum Image-Element
            var displayPosition = e.GetPosition(DisplayImage);
            
            // WICHTIG: DisplayImage.Width ist die LOGISCHE Größe (= imageOriginalWidth)
            // GetPosition gibt Koordinaten relativ zu dieser logischen Größe
            // Daher brauchen wir KEINE Transformation - die Koordinaten sind bereits korrekt!
            
            var imagePosition = new System.Windows.Point(
                displayPosition.X,
                displayPosition.Y
            );
            
            // Begrenze auf Bildgrenzen
            imagePosition.X = Math.Max(0, Math.Min(imagePosition.X, imageOriginalWidth - 1));
            imagePosition.Y = Math.Max(0, Math.Min(imagePosition.Y, imageOriginalHeight - 1));
            
            startPoint = imagePosition;
            clickPoint = imagePosition;
            
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Display-Position (roh): ({displayPosition.X:F1}, {displayPosition.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Image.Width: {DisplayImage.Width}, Image.Height: {DisplayImage.Height}");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Image.ActualWidth: {DisplayImage.ActualWidth:F1}, Image.ActualHeight: {DisplayImage.ActualHeight:F1}");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] ? Bild-Position: ({imagePosition.X:F1}, {imagePosition.Y:F1})");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Original-Größe: {imageOriginalWidth}x{imageOriginalHeight}");
            System.Diagnostics.Debug.WriteLine($"[MOUSE] Zoom: {currentZoom:F2}");

            if (ModePixelate.IsChecked == true || ModeCrop.IsChecked == true)
            {
                isSelecting = true;
                SelectionRect.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRect, startPoint.X);
                Canvas.SetTop(SelectionRect, startPoint.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
                
                StatusText.Text = ModeCrop.IsChecked == true 
                    ? "Ziehen Sie Rechteck auf - was behalten werden soll" 
                    : "Ziehen Sie Rechteck auf - Bereich zum Verpixeln";
            }
            else
            {
                // Zeige Marker an geklickter Position
                ClickMarker.Visibility = Visibility.Visible;
                Canvas.SetLeft(ClickMarker, imagePosition.X - 10);
                Canvas.SetTop(ClickMarker, imagePosition.Y - 10);
                
                System.Diagnostics.Debug.WriteLine($"[MOUSE] Marker platziert bei Canvas: ({imagePosition.X - 10:F1}, {imagePosition.Y - 10:F1})");
                
                StatusText.Text = ModeText.IsChecked == true 
                    ? "Position gewaehlt - Klicken Sie 'TEXT EINFUEGEN'" 
                    : "Position gewaehlt - Klicken Sie 'GEO-DATEN EINFUEGEN'";
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Nur für Rechteck-Auswahl bei Verpixeln/Crop
            if (!isSelecting || e.LeftButton != MouseButtonState.Pressed) return;
            
            var displayPosition = e.GetPosition(DisplayImage);
            
            // Keine Transformation nötig - GetPosition gibt bereits Bild-Koordinaten
            var imagePosition = new System.Windows.Point(
                displayPosition.X,
                displayPosition.Y
            );
            
            var x = Math.Min(startPoint.X, imagePosition.X);
            var y = Math.Min(startPoint.Y, imagePosition.Y);
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = Math.Abs(imagePosition.X - startPoint.X);
            SelectionRect.Height = Math.Abs(imagePosition.Y - startPoint.Y);
            
            PositionText.Text = $"Position: {(int)imagePosition.X}, {(int)imagePosition.Y}";
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            if (SelectionRect.Visibility == Visibility.Visible && SelectionRect.Width > 10)
            {
                System.Diagnostics.Debug.WriteLine($"[MOUSE] Rechteck fertig: ({Canvas.GetLeft(SelectionRect):F0}, {Canvas.GetTop(SelectionRect):F0}, {SelectionRect.Width:F0}, {SelectionRect.Height:F0})");
                
                StatusText.Text = ModeCrop.IsChecked == true 
                    ? "Rechteck fertig - Klicken Sie 'BILD ZUSCHNEIDEN'" 
                    : "Rechteck fertig - Klicken Sie 'BEREICH VERPIXELN'";
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
                System.Diagnostics.Debug.WriteLine($"[TEXT] Canvas-Click: ({clickPoint.Value.X:F1}, {clickPoint.Value.Y:F1})");
                System.Diagnostics.Debug.WriteLine($"[TEXT] Bild vor Bearbeitung: {imageOriginalWidth}x{imageOriginalHeight}");
                
                // Speichere die ABSOLUTE Scroll-Position RELATIV zum geklickten Punkt
                // So dass der geklickte Bereich nach Reload sichtbar bleibt
                savedScrollOffsetX = ImageScrollViewer.HorizontalOffset;
                savedScrollOffsetY = ImageScrollViewer.VerticalOffset;
                restoreScrollPosition = true;
                
                System.Diagnostics.Debug.WriteLine($"[TEXT] Speichere Scroll-Position: X={savedScrollOffsetX:F1}, Y={savedScrollOffsetY:F1}");
                
                SaveUndoState();

                var imageX = clickPoint.Value.X;
                var imageY = clickPoint.Value.Y;

                System.Diagnostics.Debug.WriteLine($"[TEXT] Ziel-Position im Bild: ({imageX:F1}, {imageY:F1})");
                System.Diagnostics.Debug.WriteLine($"[TEXT] Text: '{TextInput.Text}'");
                System.Diagnostics.Debug.WriteLine($"[TEXT] Schriftgröße: {FontSizeSlider.Value}");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[TEXT] ImageSharp geladen: {img.Width}x{img.Height}");
                    
                    var family = SixLabors.Fonts.SystemFonts.Collection.Families.FirstOrDefault();
                    if (family == null)
                    {
                        MessageBox.Show("Keine Systemschrift gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var font = family.CreateFont((float)FontSizeSlider.Value);
                    var color = TextColorPicker.SelectedColor;
                    var imgColor = SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);

                    System.Diagnostics.Debug.WriteLine($"[TEXT] Font: {family.Name}, Größe: {FontSizeSlider.Value}");
                    System.Diagnostics.Debug.WriteLine($"[TEXT] Farbe: R={color.R}, G={color.G}, B={color.B}, A={color.A}");

                    img.Mutate(x =>
                    {
                        x.DrawText(TextInput.Text, font, SixLabors.ImageSharp.Color.Black, 
                            new SixLabors.ImageSharp.PointF((float)imageX + 2, (float)imageY + 2));
                        x.DrawText(TextInput.Text, font, imgColor, 
                            new SixLabors.ImageSharp.PointF((float)imageX, (float)imageY));
                    });

                    System.Diagnostics.Debug.WriteLine($"[TEXT] Nach Mutation: {img.Width}x{img.Height}");

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(workingFilePath, encoder);
                    
                    System.Diagnostics.Debug.WriteLine($"[TEXT] Gespeichert mit Quality=95");
                }

                // Prüfe gespeicherte Datei
                var savedFileInfo = new FileInfo(workingFilePath);
                System.Diagnostics.Debug.WriteLine($"[TEXT] Gespeicherte Datei: {savedFileInfo.Length} bytes");
                
                using (var testImg = SixLabors.ImageSharp.Image.Load(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[TEXT] Gespeicherte Bildgröße: {testImg.Width}x{testImg.Height}");
                }

                hasChanges = true;
                ClearMarkers();
                
                System.Diagnostics.Debug.WriteLine("[TEXT] Lade Bild neu...");
                LoadAndDisplayImage();
                
                StatusText.Text = "Text eingefuegt!";
                System.Diagnostics.Debug.WriteLine("[TEXT] Fertig!");
                System.Diagnostics.Debug.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler in ApplyText: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
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
                System.Diagnostics.Debug.WriteLine($"[GEO] Canvas-Click: ({clickPoint.Value.X:F1}, {clickPoint.Value.Y:F1})");
                
                SaveUndoState();

                var imageX = clickPoint.Value.X;
                var imageY = clickPoint.Value.Y;

                System.Diagnostics.Debug.WriteLine($"[GEO] Ziel-Position: ({imageX:F1}, {imageY:F1})");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[GEO] Bild geladen: {img.Width}x{img.Height}");
                    
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
                    img.Save(workingFilePath, encoder);
                    
                    System.Diagnostics.Debug.WriteLine($"[GEO] Nach Speichern: {img.Width}x{img.Height}");
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler in ApplyGeo: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
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
                
                SaveUndoState();

                var canvasX = Canvas.GetLeft(SelectionRect);
                var canvasY = Canvas.GetTop(SelectionRect);
                var canvasWidth = SelectionRect.Width;
                var canvasHeight = SelectionRect.Height;

                System.Diagnostics.Debug.WriteLine($"[PIXELATE] Canvas-Rechteck: ({canvasX:F0}, {canvasY:F0}, {canvasWidth:F0}, {canvasHeight:F0})");
                System.Diagnostics.Debug.WriteLine($"[PIXELATE] Pixel-Größe: {PixelSizeSlider.Value}");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[PIXELATE] Bild geladen: {img.Width}x{img.Height}");
                    
                    var rect = new SixLabors.ImageSharp.Rectangle(
                        (int)canvasX, (int)canvasY,
                        (int)canvasWidth, (int)canvasHeight);

                    rect.X = Math.Max(0, Math.Min(rect.X, img.Width));
                    rect.Y = Math.Max(0, Math.Min(rect.Y, img.Height));
                    rect.Width = Math.Min(rect.Width, img.Width - rect.X);
                    rect.Height = Math.Min(rect.Height, img.Height - rect.Y);

                    System.Diagnostics.Debug.WriteLine($"[PIXELATE] Korrigiertes Rechteck: ({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})");

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        using (var sub = img.Clone(ctx => ctx.Crop(rect)))
                        {
                            sub.Mutate(x => x.Pixelate((int)PixelSizeSlider.Value));
                            img.Mutate(x => x.DrawImage(sub, new SixLabors.ImageSharp.Point(rect.X, rect.Y), 1f));
                        }
                    }

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(workingFilePath, encoder);
                    
                    System.Diagnostics.Debug.WriteLine($"[PIXELATE] Nach Speichern: {img.Width}x{img.Height}");
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler in ApplyPixelate: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
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
                
                SaveUndoState();

                var canvasX = Canvas.GetLeft(SelectionRect);
                var canvasY = Canvas.GetTop(SelectionRect);
                var canvasWidth = SelectionRect.Width;
                var canvasHeight = SelectionRect.Height;

                System.Diagnostics.Debug.WriteLine($"[CROP] Canvas-Rechteck: ({canvasX:F0}, {canvasY:F0}, {canvasWidth:F0}, {canvasHeight:F0})");

                using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(workingFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[CROP] Bild VOR Crop: {img.Width}x{img.Height}");
                    
                    var rect = new SixLabors.ImageSharp.Rectangle(
                        (int)canvasX, (int)canvasY,
                        (int)canvasWidth, (int)canvasHeight);

                    rect.X = Math.Max(0, Math.Min(rect.X, img.Width));
                    rect.Y = Math.Max(0, Math.Min(rect.Y, img.Height));
                    rect.Width = Math.Min(rect.Width, img.Width - rect.X);
                    rect.Height = Math.Min(rect.Height, img.Height - rect.Y);

                    System.Diagnostics.Debug.WriteLine($"[CROP] Korrigiertes Rechteck: ({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})");

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        img.Mutate(x => x.Crop(rect));
                        System.Diagnostics.Debug.WriteLine($"[CROP] Bild NACH Crop: {img.Width}x{img.Height}");
                    }

                    var encoder = new JpegEncoder { Quality = 95 };
                    img.Save(workingFilePath, encoder);
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] Fehler in ApplyCrop: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Fehler beim Zuschneiden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
                EditedImageBytes = File.ReadAllBytes(workingFilePath);
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
