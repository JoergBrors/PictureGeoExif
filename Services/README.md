# Services

Fachlogik ohne UI-Abhängigkeit (außer `BitmapImage` in `ImageService`). Die Klassen sind über `tests/PictureGeoExif.Tests` abgedeckt. Die Einordnung ins Gesamtsystem beschreibt [docs/Architektur.md](../docs/Architektur.md).

## Aktive Klassen

### `ImageService`: Thumbnails, GPS, Exporte

| Methode | Verhalten |
| --- | --- |
| `Task<BitmapImage?> CreateThumbnailAsync(path, maxWidth = 200)` / `CreateThumbnail(...)` | Thumbnail im Temp-Ordner, Cache über `WeakReference`, Sperre pro Datei |
| `void InvalidateThumbnailCache(path)`, `ClearThumbnailCache()` | Cache leeren, z. B. nach einem neuen Pfad |
| `void WriteGpsToImage(path, lat, lon)` | JPEG: nur das EXIF-APP1-Segment wird ersetzt (`JpegExifWriter`). PNG/TIFF: verlustfreie Neukodierung. Andere Formate: `InvalidOperationException`. Vor dem Ersetzen wird mit MetadataExtractor nachgelesen, dann atomar geschrieben. Ungültige Koordinaten: `ArgumentOutOfRangeException`, die Datei bleibt unverändert. |
| `string SaveSingleImage(source, outputFolder, lat?, lon?)` | Kollisionsfreie Kopie im Ausgabeordner, optional mit GPS. Bei einem Fehler wird die Kopie entfernt; das Original bleibt immer unverändert. |
| `string SaveEditedImage(bytes, fileName, outputFolder)` | Speichert Editor-Exporte kollisionsfrei (`overwrite: false`). |

```csharp
using var images = new ImageService();
string copy = images.SaveSingleImage(@"C:\Fotos\a.jpg", @"D:\Export", 47.3769, 8.5417);
```

### `JpegExifWriter`

`byte[] ReplaceExif(ReadOnlySpan<byte> jpeg, ReadOnlySpan<byte> exif)` ersetzt bzw. ergänzt den EXIF-Block (nach einem eventuellen JFIF-APP0) und kopiert alle anderen Bytes unverändert. Es gibt drei Abbruchfälle, das Original bleibt dann erhalten:

- mehrere EXIF-Blöcke
- EXIF größer als 64 KB
- beschädigte Segmentstruktur

### `AtomicFile`

- `Write(path, bytes, overwrite = true)`: schreibt eine temporäre Datei im Zielordner mit Flush und ersetzt dann per `File.Replace` bzw. `File.Move`.
- `ExportPath(folder, name)`: erzeugt `folder\JJJJMMTT\name_JJJJMMTT_HHmmss_fff_<GUID>.ext`.

### `PixelGeometry`

- `Selection(x1, y1, x2, y2, w, h)`: Rechteck in Bildpixeln. Links und oben wird abgerundet, rechts und unten aufgerundet; die Kanten werden auf [0, W] bzw. [0, H] begrenzt. Leere oder ungültige Eingaben ergeben `Rectangle.Empty`.
- `Fit(...)`: Zoom zum Einpassen, auch unter 10 %.
- `ValidGps(lat, lon)`: prüft endliche Werte in den Grenzen ±90/±180.

### `EditorSession`

Transaktionale, verlustfreie Bearbeitungshistorie eines Bildes.

- `OpenAsync`: normalisiert die Orientierung und lehnt Bilder über 80 MP sowie mehrseitige Dateien ab.
- `ApplyAsync(operation)`: übernimmt eine Operation erst nach Erfolg in die Historie.
- `Undo`, `Redo`: Historie mit maximal 31 Ständen bzw. 1 GB.
- `ExportAsync(ext, quality)`: kodiert erst beim Export.

Statische Werkzeuge:

- `Blur(image, rect, sigma)`: liest Kontext um die Auswahl, schreibt nur innerhalb der Auswahl zurück.
- `Pixelate(image, rect, block)`: begrenzt die Blockgröße auf die Region.
- `Stamp(image, text, size, color, x, y, alignRight, alignBottom)`: misst den Text, verkleinert ihn bei Bedarf und lehnt unlesbar kleine Stempel ab.

### `AiMetadataService`: lokale Seite der KI-Funktion

| Methode | Zweck |
| --- | --- |
| `Read(row)` | EXIF, eingebettetes XMP und Sidecar lesen; Nutzungspräferenzen (`LearningOptOutIn` vorhanden, IPTC PLUS DataMining) ermitteln |
| `LocalMappings(local)` | EXIF → XMP (Aufnahmezeit mit bekanntem Offset, GPS als XMP-Koordinate, Urheber, Copyright), nur wenn im XMP noch nicht vorhanden |
| `ModelContext(row, local)` | Minimaler Textkontext: Datum ohne Uhrzeit, Hemisphäre, vorhandene Beschreibungen |
| `Preview(path, template)` | Orientierte, verkleinerte JPEG-Vorschau ohne Metadaten; wird nie hochskaliert |
| `Proposals(row, proposal, template, source)` | Vorschläge gemäß `fillMissing`/`mergeUnique`/`replace`; Jahreszeiten unter der Konfidenzschwelle sind nicht vorausgewählt |
| `WriteSidecar(row, changes)` | Schreibt nur Allowlist-Ziele ins `.xmp`. Verweigert, wenn sich das Bild seit der Prüfung geändert hat; liest vor dem atomaren Schreiben nach. |
| `CacheKey/CacheRead/CacheWrite` | Ergebnis-Cache unter `%LOCALAPPDATA%\PictureGeoExif\ai-cache` |

### `AiProviders`

- `IAiMetadataProvider.CompleteAsync(system, user, jpeg?, schemaName, schema, maxOutput, token)` führt genau eine strukturierte Anfrage aus.
- `AiProviderFactory.Create(profile, maxAttempts)` erstellt einen der Adapter:
  - `OpenAiResponsesProvider` für OpenAI und Azure (Responses API, `store=false`)
  - `GeminiProvider` (`generateContent`)
- Wiederholt wird nur bei 429 und 5xx. Bei Timeout oder Abbruch nach dem Senden wirft der Adapter `AiProviderException` mit `Ambiguous = true`.
- `Usage` enthält die bereits verbrauchten Tokens, damit die Kosten auch im Fehlerfall gezählt werden.

### `CredentialStore`

`Read`, `Write` und `Delete` für generische Einträge im Windows Credential Manager (aktueller Benutzer). Verwendet für API-Schlüssel.

## Legacy (nicht verwendet)

`OptimizedImageService`, `ImageProcessingService`, `UndoService` und `CoordinateMapper` stammen aus früheren Ständen und werden vom aktiven Code nicht aufgerufen. `OptimizedImageService` schreibt GPS noch per Neukodierung mit `.bak`-Datei. **Nicht verwenden oder erweitern**; sie sind Kandidaten zum Entfernen.
