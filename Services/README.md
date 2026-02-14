# Services

Dieses Verzeichnis enthält Service-Klassen, die Bildoperationen (Laden, Thumbnail-Erzeugung, EXIF-Schreiben) und verwandte Funktionalitäten kapseln.

Wichtigste Dateien:

- `ImageService.cs` - Original-Version mit Caching und robusten Dateioperationen
- `OptimizedImageService.cs` - Optimierte Version mit Batch-Thumbnail-API und zusätzlichen Invalidate-Mechanismen

Dieses README beschreibt die öffentliche API, typisches Verhalten und Beispiele zur Verwendung.

Übersicht der Klassen und wichtigste Methoden

1) `ImageService`
- Zweck: Basis-Service für Thumbnails, temporäre Kopien, Laden von BitmapImages und Schreiben von GPS-EXIF.
- Wichtige Methoden (Signaturen):
  - `Task<BitmapImage?> CreateThumbnailAsync(string imagePath, int maxWidth = 200)`
  - `BitmapImage? CreateThumbnail(string imagePath, int maxWidth = 200)`
  - `BitmapImage LoadBitmapImage(string filePath)`
  - `string CreateTempCopy(string sourcePath)`
  - `void WriteGpsToImage(string imagePath, double latitude, double longitude)`
  - `string SaveSingleImage(string sourcePath, string outputFolder, double? latitude = null, double? longitude = null)`
  - `string SaveEditedImage(byte[] imageBytes, string originalFileName, string outputFolder)`
  - `void InvalidateThumbnailCache(string imagePath)`
  - `void ClearThumbnailCache()`
  - `void Dispose()`

- Verhalten / Hinweise:
  - Thumbnails werden in einem temporären Ordner erzeugt und per `BitmapImage` geladen.
  - Caching verwendet `WeakReference<BitmapImage>` — Thumbnails können bei Speicherbedarf durch den GC freigegeben werden.
  - Beim Schreiben von GPS-Daten wird eine Backup-Datei (`.bak`) angelegt und bei Fehlern wiederhergestellt.
  - Methoden werfen spezifische Exceptions (`FileNotFoundException`, `InvalidOperationException`, `IOException`) bei Fehlern.

- Beispiel (synchrones Thumbnail):

  ```csharp
  var svc = new ImageService();
  var thumb = svc.CreateThumbnail(@"C:\bilder\foto.jpg", 160);
  if (thumb != null) imageControl.Source = thumb;
  ```

- Beispiel (GPS schreiben):

  ```csharp
  svc.WriteGpsToImage(@"C:\bilder\foto.jpg", 51.0, 7.0);
  ```

2) `OptimizedImageService`
- Zweck: Verbesserte/optimierte Variante mit Batch-APIs und aggressive Cache-Invalidierung nach Änderungen.
- Wichtige Methoden (Signaturen):
  - `Task<BitmapImage?> CreateThumbnailAsync(string imagePath, int maxWidth = 200)`
  - `BitmapImage? CreateThumbnail(string imagePath, int maxWidth = 200)`
  - `Task<BitmapImage?[]> CreateThumbnailsBatchAsync(string[] imagePaths, int maxWidth = 200)`
  - `BitmapImage LoadBitmapImage(string filePath)`
  - `string CreateTempCopy(string sourcePath)`
  - `void WriteGpsToImage(string imagePath, double latitude, double longitude)`
  - `string SaveSingleImage(string sourcePath, string outputFolder, double? latitude = null, double? longitude = null)`
  - `string SaveEditedImage(byte[] imageBytes, string originalFileName, string outputFolder)`
  - `void InvalidateThumbnailCache(string imagePath)`
  - `void ClearThumbnailCache()`
  - `void Dispose()`

- Verhalten / Hinweise:
  - Bietet `CreateThumbnailsBatchAsync` für paralleles Erzeugen von Thumbnails.
  - Invalidiert Thumbnails im Cache, wenn GPS-Daten geschrieben werden (wichtig beim Live-Reload von Thumbnails in der UI).
  - Nutzt atomare Schreibvorgänge (tmp -> move) beim Speichern bearbeiteter Bilder.

- Beispiel (Batch-Thumbnails):

  ```csharp
  var svc = new OptimizedImageService();
  var thumbs = await svc.CreateThumbnailsBatchAsync(new[] { "a.jpg", "b.jpg" }, 180);
  // thumbs[0] und thumbs[1] sind BitmapImage-Instanzen oder null
  ```

Fehlerbehandlung und Threading

- Die Services geben Exceptions weiter. In der UI sollten die Aufrufer Fehler abfangen und dem Nutzer sinnvolle Meldungen anzeigen.
- Async-Methoden (`CreateThumbnailAsync`, `CreateThumbnailsBatchAsync`) sind thread-safe bezüglich der internen Locks; UI-Aufrufer sollten sicherstellen, dass BitmapImage-Objekte auf dem UI-Thread verwendet/gesetzt werden.

Changelog-Verknüpfung

- Änderungen an den Services werden im Root-`changelog.md` protokolliert. Für Verzeichnis-spezifische Änderungen kann ebenfalls eine `changelog.md` in diesem Verzeichnis angelegt und mit dem Root-Changelog verlinkt werden.

Sicherheit & Performance

- Verwende `Dispose()` für Service-Instanzen, die temporäre Ordner anlegen, insbesondere in langen Sessions.
- Bei sehr vielen Thumbnails empfehlen wir, die Batch-API zu nutzen und die Anzahl paralleler Tasks auf die CPU-Kerne zu begrenzen.

Weitere Details

- Für tiefergehende Implementierungsdetails und Diskussionshistorie siehe `../changelog.md` im Root-Verzeichnis und die Kommentare in den `.cs`-Dateien.
