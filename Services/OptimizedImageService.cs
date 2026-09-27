using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace PictureExifclone.Services
{
    /// <summary>
    /// Optimized ImageService mit Caching und Async-Operationen
    /// </summary>
    public class OptimizedImageService : IDisposable
    {
        private readonly string tempFolder;
        private bool disposed = false;
        
        // Cache für Thumbnails (WeakReference ermöglicht GC bei Speicherdruck)
        private readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> thumbnailCache = new();
        
        // SemaphoreSlim für Thread-sichere Thumbnail-Erstellung
        private readonly ConcurrentDictionary<string, SemaphoreSlim> thumbnailLocks = new();

        public OptimizedImageService()
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

            // Hole Lock für diesen spezifischen Pfad
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
                    // Speichere im Cache
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
        /// Synchrone Version für Kompatibilität
        /// </summary>
        public BitmapImage? CreateThumbnail(string imagePath, int maxWidth = 200)
        {
            return CreateThumbnailAsync(imagePath, maxWidth).GetAwaiter().GetResult();
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
                    
                    // Reduziere JPEG-Qualität für Thumbnails (von 85 auf 75)
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
        /// Lädt mehrere Thumbnails parallel
        /// </summary>
        public async Task<BitmapImage?[]> CreateThumbnailsBatchAsync(string[] imagePaths, int maxWidth = 200)
        {
            var tasks = imagePaths.Select(path => CreateThumbnailAsync(path, maxWidth)).ToArray();
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Löscht Cache für einen spezifischen Pfad
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

        public BitmapImage LoadBitmapImage(string filePath)
        {
            if (disposed || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");

            try
            {
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

        public string CreateTempCopy(string sourcePath)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(OptimizedImageService));
            
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException($"Quelldatei nicht gefunden: {sourcePath}");

            try
            {
                string tempPath = Path.Combine(tempFolder, $"edit_{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
                
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

        public void WriteGpsToImage(string imagePath, double latitude, double longitude)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(OptimizedImageService));
            
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {imagePath}");

            string? backupPath = null;
            try
            {
                backupPath = imagePath + ".bak";
                File.Copy(imagePath, backupPath, true);

                byte[] imageData = File.ReadAllBytes(imagePath);

                using (var ms = new MemoryStream(imageData))
                using (var image = SixLabors.ImageSharp.Image.Load(ms))
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

                    using (var outMs = new MemoryStream())
                    {
                        var format = image.Metadata.DecodedImageFormat;
                        if (format != null)
                            image.Save(outMs, format);
                        else
                            image.SaveAsJpeg(outMs, new JpegEncoder { Quality = 95 });
                        
                        File.WriteAllBytes(imagePath, outMs.ToArray());
                    }
                }

                // Cache für diesen Pfad invalidieren
                InvalidateThumbnailCache(imagePath);

                if (backupPath != null && File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch (Exception ex)
            {
                if (backupPath != null && File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, imagePath, true);
                        File.Delete(backupPath);
                    }
                    catch { }
                }
                
                throw new InvalidOperationException($"Fehler beim Schreiben der GPS-Daten: {ex.Message}", ex);
            }
        }

        private SixLabors.ImageSharp.Rational[] ConvertToRational(double value)
        {
            int degrees = (int)value;
            double minutesDecimal = (value - degrees) * 60;
            int minutes = (int)minutesDecimal;
            double seconds = (minutesDecimal - minutes) * 60;

            return new SixLabors.ImageSharp.Rational[]
            {
                new SixLabors.ImageSharp.Rational((uint)degrees, 1),
                new SixLabors.ImageSharp.Rational((uint)minutes, 1),
                new SixLabors.ImageSharp.Rational((uint)(seconds * 1000000), 1000000)
            };
        }

        public string SaveEditedImage(byte[] imageBytes, string originalFileName, string outputFolder)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(OptimizedImageService));
            
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Bilddaten sind leer", nameof(imageBytes));
            
            if (string.IsNullOrEmpty(originalFileName))
                throw new ArgumentException("Dateiname ist ungültig", nameof(originalFileName));

            try
            {
                var outDir = Path.Combine(outputFolder, DateTime.Now.ToString("yyyyMMdd"));
                Directory.CreateDirectory(outDir);

                var baseName = Path.GetFileNameWithoutExtension(originalFileName);
                var ext = Path.GetExtension(originalFileName);
                if (string.IsNullOrEmpty(ext))
                    ext = ".jpg";
                
                var newName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                var newPath = Path.Combine(outDir, newName);

                var tempPath = newPath + ".tmp";
                File.WriteAllBytes(tempPath, imageBytes);
                
                if (File.Exists(newPath))
                    File.Delete(newPath);
                    
                File.Move(tempPath, newPath);

                return newPath;
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Speichern des bearbeiteten Bildes: {ex.Message}", ex);
            }
        }

        public string SaveSingleImage(string sourcePath, string outputFolder, double? latitude = null, double? longitude = null)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(OptimizedImageService));
            
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException($"Quelldatei nicht gefunden: {sourcePath}");

            try
            {
                var outDir = Path.Combine(outputFolder, DateTime.Now.ToString("yyyyMMdd"));
                Directory.CreateDirectory(outDir);

                var baseName = Path.GetFileNameWithoutExtension(sourcePath);
                var ext = Path.GetExtension(sourcePath);
                var newName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                var newPath = Path.Combine(outDir, newName);

                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destStream = new FileStream(newPath, FileMode.Create, FileAccess.Write))
                {
                    sourceStream.CopyTo(destStream);
                }

                if (latitude.HasValue && longitude.HasValue)
                {
                    WriteGpsToImage(newPath, latitude.Value, longitude.Value);
                }

                return newPath;
            }
            catch (Exception ex)
            {
                throw new IOException($"Fehler beim Speichern des Bildes: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                ClearThumbnailCache();
                
                try
                {
                    if (Directory.Exists(tempFolder))
                    {
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
                    System.Diagnostics.Debug.WriteLine($"Warnung: Temporärer Ordner konnte nicht gelöscht werden: {tempFolder}");
                }
                disposed = true;
            }
        }
    }
}
