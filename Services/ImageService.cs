using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;

namespace PictureExifclone.Services
{
    /// <summary>
    /// Service für Bildoperationen mit Caching und Performance-Optimierungen
    /// </summary>
    public class ImageService : IDisposable
    {
        private readonly string tempFolder;
        private bool disposed = false;
        
        // Thumbnail-Cache mit WeakReference für automatische Speicherverwaltung
        private readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> thumbnailCache = new();
        
        // Locks für Thread-sichere Thumbnail-Erstellung
        private readonly ConcurrentDictionary<string, SemaphoreSlim> thumbnailLocks = new();

        public ImageService()
        {
            tempFolder = Path.Combine(Path.GetTempPath(), $"PictureExifclone_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);
        }

        /// <summary>
        /// Erstellt ein Thumbnail asynchron mit Caching
        /// </summary>
        public async Task<BitmapImage?> CreateThumbnailAsync(string imagePath, int maxWidth = 200)
        {
            if (disposed || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            // Prüfe Cache zuerst
            if (thumbnailCache.TryGetValue(imagePath, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    return cachedThumbnail;
                }
                // WeakReference existiert, aber Objekt wurde GC'd
                thumbnailCache.TryRemove(imagePath, out _);
            }

            // Hole Lock für diesen spezifischen Pfad (verhindert doppelte Erstellung)
            var semaphore = thumbnailLocks.GetOrAdd(imagePath, _ => new SemaphoreSlim(1, 1));
            
            await semaphore.WaitAsync();
            try
            {
                // Double-check nach dem Lock
                if (thumbnailCache.TryGetValue(imagePath, out weakRef) && 
                    weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    return cachedThumbnail;
                }

                // Erstelle Thumbnail asynchron
                var thumbnail = await Task.Run(() => CreateThumbnailInternal(imagePath, maxWidth));
                
                if (thumbnail != null)
                {
                    thumbnailCache[imagePath] = new WeakReference<BitmapImage>(thumbnail);
                }
                
                return thumbnail;
            }
            finally
            {
                semaphore.Release();
                
                // Cleanup: Entferne Lock wenn nicht mehr benötigt
                if (semaphore.CurrentCount == 1)
                {
                    thumbnailLocks.TryRemove(imagePath, out _);
                }
            }
        }

        /// <summary>
        /// Erstellt ein Thumbnail mit Caching (synchron für Kompatibilität)
        /// </summary>
        public BitmapImage? CreateThumbnail(string imagePath, int maxWidth = 200)
        {
            if (disposed || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            // Prüfe Cache zuerst
            if (thumbnailCache.TryGetValue(imagePath, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    return cachedThumbnail;
                }
                thumbnailCache.TryRemove(imagePath, out _);
            }

            // Erstelle Thumbnail ohne Lock (synchroner Pfad)
            try
            {
                var thumbnail = CreateThumbnailInternal(imagePath, maxWidth);
                
                if (thumbnail != null)
                {
                    thumbnailCache[imagePath] = new WeakReference<BitmapImage>(thumbnail);
                }
                
                return thumbnail;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen des Thumbnails: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Interne Thumbnail-Erstellung (optimiert)
        /// </summary>
        private BitmapImage? CreateThumbnailInternal(string imagePath, int maxWidth)
        {
            string? thumbnailPath = null;
            try
            {
                thumbnailPath = Path.Combine(tempFolder, $"thumb_{Guid.NewGuid():N}.jpg");

                using (var image = SixLabors.ImageSharp.Image.Load(imagePath))
                {
                    if (image.Width > maxWidth)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(maxWidth, 0),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        }));
                    }
                    
                    // Reduzierte JPEG-Qualität für schnellere Thumbnail-Erstellung (75 statt 85)
                    image.SaveAsJpeg(thumbnailPath, new JpegEncoder { Quality = 75 });
                }

                return LoadBitmapImageFromFile(thumbnailPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen des Thumbnails: {ex.Message}");
                
                try
                {
                    if (thumbnailPath != null && File.Exists(thumbnailPath))
                        File.Delete(thumbnailPath);
                }
                catch { }
                
                return null;
            }
        }

        /// <summary>
        /// Invalidiert Cache für einen spezifischen Pfad
        /// </summary>
        public void InvalidateThumbnailCache(string imagePath)
        {
            thumbnailCache.TryRemove(imagePath, out _);
        }

        /// <summary>
        /// Löscht kompletten Thumbnail-Cache
        /// </summary>
        public void ClearThumbnailCache()
        {
            thumbnailCache.Clear();
        }

        /// <summary>
        /// Lädt ein BitmapImage aus einer Datei - VERBESSERTE VERSION
        /// </summary>
        private BitmapImage LoadBitmapImageFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                
                return bitmap;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Fehler beim Laden des Bildes: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lädt ein BitmapImage aus einer Datei ohne File-Locking und Cache
        /// </summary>
        public BitmapImage LoadBitmapImage(string filePath)
        {
            if (disposed || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");

            try
            {
                // Lese Datei komplett in Speicher
                byte[] imageData = File.ReadAllBytes(filePath);
                
                if (imageData == null || imageData.Length == 0)
                    throw new InvalidOperationException("Bilddaten sind leer");
                
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
                
                return bitmap;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Fehler beim Laden des Bildes: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Kopiert ein Bild in eine temporäre Datei für die Bearbeitung
        /// </summary>
        public string CreateTempCopy(string sourcePath)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImageService));
            
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException($"Quelldatei nicht gefunden: {sourcePath}");

            try
            {
                string tempPath = Path.Combine(tempFolder, $"edit_{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
                
                // Verwende FileStream für bessere Fehlerbehandlung
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    sourceStream.CopyTo(destStream);
                }
                
                return tempPath;
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Erstellen der temporären Kopie: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Schreibt GPS-Koordinaten. JPEG: nur das EXIF-Segment wird ersetzt (keine Neukodierung).
        /// PNG/TIFF: verlustfreie Neukodierung. Ergebnis wird vor dem atomaren Ersetzen nachgelesen.
        /// </summary>
        public void WriteGpsToImage(string imagePath, double latitude, double longitude)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!PixelGeometry.ValidGps(latitude, longitude))
                throw new ArgumentOutOfRangeException(nameof(latitude), "Ungültige GPS-Koordinaten.");
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {imagePath}");

            try
            {
                byte[] original = File.ReadAllBytes(imagePath);
                var info = SixLabors.ImageSharp.Image.Identify(original);
                var format = info.Metadata.DecodedImageFormat;
                byte[] output;
                if (format is JpegFormat)
                {
                    var exif = info.Metadata.ExifProfile ?? new ExifProfile();
                    SetGps(exif, latitude, longitude);
                    output = JpegExifWriter.ReplaceExif(original, exif.ToByteArray() ?? throw new InvalidDataException("EXIF konnte nicht serialisiert werden."));
                }
                else if (format is PngFormat or TiffFormat)
                {
                    using var image = SixLabors.ImageSharp.Image.Load(original);
                    var exif = image.Metadata.ExifProfile ?? new ExifProfile();
                    SetGps(exif, latitude, longitude);
                    image.Metadata.ExifProfile = exif;
                    using var stream = new MemoryStream();
                    image.Save(stream, format);
                    output = stream.ToArray();
                }
                else
                {
                    throw new NotSupportedException($"Das Format {format?.Name ?? Path.GetExtension(imagePath)} kann keine GPS-Metadaten speichern.");
                }

                // Verify with an independent reader before touching the target file.
                var gps = MetadataExtractor.ImageMetadataReader.ReadMetadata(new MemoryStream(output))
                    .OfType<MetadataExtractor.Formats.Exif.GpsDirectory>().FirstOrDefault();
                if (gps == null || !gps.TryGetGeoLocation(out var check) ||
                    Math.Abs(check.Latitude - latitude) > 1e-6 || Math.Abs(check.Longitude - longitude) > 1e-6)
                    throw new InvalidDataException("GPS-Nachprüfung fehlgeschlagen; Datei wurde nicht verändert.");

                AtomicFile.Write(imagePath, output);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"Fehler beim Schreiben der GPS-Daten: {ex.Message}", ex);
            }
        }

        private static void SetGps(ExifProfile exif, double latitude, double longitude)
        {
            exif.SetValue(ExifTag.GPSLatitudeRef, latitude >= 0 ? "N" : "S");
            exif.SetValue(ExifTag.GPSLongitudeRef, longitude >= 0 ? "E" : "W");
            exif.SetValue(ExifTag.GPSLatitude, ConvertToRational(Math.Abs(latitude)));
            exif.SetValue(ExifTag.GPSLongitude, ConvertToRational(Math.Abs(longitude)));
        }

        private static SixLabors.ImageSharp.Rational[] ConvertToRational(double value)
        {
            int degrees = (int)value;
            double minutesDecimal = (value - degrees) * 60;
            int minutes = (int)minutesDecimal;
            double seconds = (minutesDecimal - minutes) * 60;

            return new SixLabors.ImageSharp.Rational[]
            {
                new SixLabors.ImageSharp.Rational((uint)degrees, 1),
                new SixLabors.ImageSharp.Rational((uint)minutes, 1),
                new SixLabors.ImageSharp.Rational((uint)Math.Round(seconds * 1000000), 1000000)
            };
        }

        /// <summary>
        /// Speichert ein bearbeitetes Bild unter einem kollisionsfreien Namen im Zielordner.
        /// </summary>
        public string SaveEditedImage(byte[] imageBytes, string originalFileName, string outputFolder)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Bilddaten sind leer", nameof(imageBytes));
            if (string.IsNullOrEmpty(originalFileName) || string.IsNullOrEmpty(Path.GetExtension(originalFileName)))
                throw new ArgumentException("Dateiname ist ungültig", nameof(originalFileName));

            try
            {
                var newPath = AtomicFile.ExportPath(outputFolder, originalFileName);
                AtomicFile.Write(newPath, imageBytes, overwrite: false);
                return newPath;
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Speichern des bearbeiteten Bildes: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Kopiert ein Bild kollisionsfrei in den Zielordner und schreibt optional GPS-Daten.
        /// Das Original bleibt unverändert; bei Fehlern wird keine halbfertige Kopie hinterlassen.
        /// </summary>
        public string SaveSingleImage(string sourcePath, string outputFolder, double? latitude = null, double? longitude = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException($"Quelldatei nicht gefunden: {sourcePath}");

            var newPath = AtomicFile.ExportPath(outputFolder, Path.GetFileName(sourcePath));
            try
            {
                AtomicFile.Write(newPath, File.ReadAllBytes(sourcePath), overwrite: false);
                if (latitude.HasValue && longitude.HasValue)
                    WriteGpsToImage(newPath, latitude.Value, longitude.Value);
                return newPath;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(newPath)) File.Delete(newPath); } catch (IOException) { }
                throw new IOException($"Fehler beim Speichern des Bildes: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
                        // Versuche mehrmals mit Verzögerung
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                Directory.Delete(tempFolder, true);
                                break;
                            }
                            catch
                            {
                                if (i < 2)
                                {
                                    System.Threading.Thread.Sleep(100);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                    System.Diagnostics.Debug.WriteLine($"Warnung: Temporärer Ordner konnte nicht gelöscht werden: {tempFolder}");
                }
                disposed = true;
            }
        }
    }
}
