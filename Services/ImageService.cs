using System;
using System.IO;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace PictureExifclone.Services
{
    /// <summary>
    /// Service für Bildoperationen mit temporären Dateien zur Speicheroptimierung
    /// Verbesserte Version mit robuster Fehlerbehandlung
    /// </summary>
    public class ImageService : IDisposable
    {
        private readonly string tempFolder;
        private bool disposed = false;

        public ImageService()
        {
            tempFolder = Path.Combine(Path.GetTempPath(), $"PictureExifclone_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);
        }

        /// <summary>
        /// Erstellt ein Thumbnail aus einem Bild mit robuster Fehlerbehandlung
        /// </summary>
        public BitmapImage? CreateThumbnail(string imagePath, int maxWidth = 200)
        {
            if (disposed || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            string? thumbnailPath = null;
            try
            {
                thumbnailPath = Path.Combine(tempFolder, $"thumb_{Guid.NewGuid():N}.jpg");

                using (var image = SixLabors.ImageSharp.Image.Load(imagePath))
                {
                    var originalWidth = image.Width;
                    var originalHeight = image.Height;
                    
                    if (originalWidth > maxWidth)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(maxWidth, 0),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        }));
                    }
                    
                    image.SaveAsJpeg(thumbnailPath, new JpegEncoder { Quality = 85 });
                }

                return LoadBitmapImage(thumbnailPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen des Thumbnails: {ex.Message}");
                
                // Cleanup bei Fehler
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
        /// Lädt ein BitmapImage aus einer Datei ohne File-Locking
        /// </summary>
        public BitmapImage LoadBitmapImage(string filePath)
        {
            if (disposed || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {filePath}");

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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
        /// Speichert GPS-Koordinaten in ein Bild mit verbesserter Fehlerbehandlung
        /// </summary>
        public void WriteGpsToImage(string imagePath, double latitude, double longitude)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImageService));
            
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {imagePath}");

            string? backupPath = null;
            try
            {
                // Erstelle Backup
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

                // Lösche Backup bei Erfolg
                if (backupPath != null && File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch (Exception ex)
            {
                // Stelle Backup wieder her bei Fehler
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

        /// <summary>
        /// Speichert ein bearbeitetes Bild in den Zielordner
        /// </summary>
        public string SaveEditedImage(byte[] imageBytes, string originalFileName, string outputFolder)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImageService));
            
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

                // Sichere atomare Operation
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

        /// <summary>
        /// Speichert ein einzelnes Bild mit optionalen GPS-Daten
        /// </summary>
        public string SaveSingleImage(string sourcePath, string outputFolder, double? latitude = null, double? longitude = null)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ImageService));
            
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

                // Kopiere Datei mit FileStream für bessere Fehlerbehandlung
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destStream = new FileStream(newPath, FileMode.Create, FileAccess.Write))
                {
                    sourceStream.CopyTo(destStream);
                }

                // Schreibe GPS wenn vorhanden
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
