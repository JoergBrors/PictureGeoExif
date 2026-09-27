# Code-Wegweiser: Wo ist was?

Schneller Einstieg in den Code. Stand: Branch `GUI-AI-Update`, .NET 10, WPF. Die Architektur erklärt [Architektur.md](Architektur.md).

## Verzeichnisstruktur

```text
PictureGeoExif/
├── App.xaml(.cs)               Anwendungsstart, globale Styles (Buttons, TextBox, Fenster)
├── AppSettings.cs              Persistente Einstellungen (settings.json) + AiPriceEntry
├── AssemblyInfo.cs             Nur ThemeInfo; Versions-/Firmendaten stehen im csproj
├── MainWindow.xaml(.cs)        Hauptfenster: Bildliste, Karte (WebView2), GPS-Aktionen, Speichern
├── ImageEditorWindow.xaml(.cs) Bildeditor: Zoom, Auswahl, Crop/Blur/Verpixeln/Text/GPS-Stempel, Undo/Redo, Export
├── AiMetadataWindow.xaml(.cs)  KI-Metadaten: Anbieter/Preise, lokale Prüfung, Analyse-Queue, Chat, Sidecar speichern
├── LicenseViewer.xaml(.cs)     Lizenzfenster (zeigt LICENSE und THIRD-PARTY-LICENSES.md)
├── Controls/                   SimpleColorPicker (Stempelfarbe)
├── Models/                     Datenobjekte (ImageItem, AiModels …)
├── Services/                   Fachlogik ohne UI (Bild-I/O, Geometrie, Metadaten, KI-Adapter)
├── Resources/                  Karten-Webinhalt: map.html, map.js, leaflet/ (lokal, keine CDN)
├── docs/                       Dokumentation + docs/examples (KI-Vorlage, Schema)
├── tests/PictureGeoExif.Tests/ xUnit-Tests (Logik, Metadaten, Fenster-Instanziierung)
├── licenses/                   Lizenztexte aller ausgelieferten Komponenten (generiert)
├── scripts/                    Update-ThirdPartyLicenses.ps1
├── .github/workflows/          build.yml (PR/Main), release-on-tag.yml (Release-ZIPs)
└── global.json                 SDK-Pin 10.0.401
```

## Aufgabenindex

| Ich will … | Datei / Stelle |
| --- | --- |
| Bilder laden, Dateitypen ändern | `MainWindow.xaml.cs` → `LoadImages`, `LoadImagesButton_Click` (Filter) |
| GPS beim Laden lesen | `MainWindow.xaml.cs` → `ReadExifData` (MetadataExtractor) |
| GPS in eine Datei schreiben | `Services/ImageService.cs` → `WriteGpsToImage` (JPEG über `JpegExifWriter`, PNG/TIFF verlustfrei, BMP abgelehnt) |
| Exportnamen oder Zielordner ändern | `Services/AtomicFile.cs` → `ExportPath`; Ordner aus `AppSettings.OutputFolder` |
| Datei sicher schreiben | `Services/AtomicFile.cs` → `Write` (temporäre Datei + Replace/Move) |
| Karte: Verhalten, Marker, Raster, Layer | `Resources/map.js` (Funktionen `configure`, `addImageMarkers`, `setSelectedMarker`, `setCurrentMarker`, `setRoutes`, `setLayerVisible`, `updateGrid`; Doppelklick = neuer Punkt) |
| Virtuelle Trassen / Reihenfolge der Bilder | `Services/RouteBuilder.cs` (Clustering, Pfad, Richtung); Anwendung in `MainWindow.xaml.cs` → `RebuildRoutes` |
| Trassen an Wege anlegen (Map-Matching) | `Services/RoadMatcher.cs` (Valhalla `trace_attributes`, Drosselung, Polyline-Decoder); `MainWindow.xaml.cs` → `RoadMatch_Click`, `ApplyRoadLayer`, `RoadKey` (Cache); Dialog `RoadMatchSettingsWindow.xaml(.cs)` |
| Rückgängig pro Bild | `Models/ImageItem.cs` → `PushHistory`/`Undo`; `MainWindow.xaml.cs` → `UndoImage_Click` |
| Karte: Einbindung, Sicherheit, Tile-Anbieter | `MainWindow.xaml.cs` → `InitializeWebView` (Virtual Host, Navigation-Filter, UA) und `CoreWebView2_WebMessageReceived`; `Resources/map.html` (CSP) |
| Tile-URL oder Attribution konfigurieren | `AppSettings.cs` (`TileUrl`, `TileAttribution`, `TileAttributionUrl`) |
| Editor-Werkzeug hinzufügen oder ändern | `ImageEditorWindow.xaml.cs` → `GetOperation`; Pixel-Implementierung in `Services/EditorSession.cs` (`Blur`, `Pixelate`, `Stamp`) |
| Auswahl, Zoom, Pixelkoordinaten | `Services/PixelGeometry.cs` (`Selection`, `Fit`, `ValidGps`); Maus-Handling in `ImageEditorWindow.xaml.cs` |
| Undo/Redo, Historie, Speicherlimit | `Services/EditorSession.cs` (`ApplyAsync`, `Undo`, `Redo`, `MaximumPixels`, `HistoryBudget`) |
| Exportformate im Editor | `Services/EditorSession.cs` → `ExportAsync`; Auswahl in `ImageEditorWindow.xaml` (`ExportFormat`) |
| KI-Vorlage validieren oder neue Regel | `Models/AiModels.cs` → `AiTemplate.Parse`/`Validate` |
| KI-Antwort prüfen | `Models/AiModels.cs` → `AiTemplate.ParseResponse`; Schema `docs/examples/ai-metadata.response.schema.json` |
| Neuen KI-Anbieter anbinden | `Services/AiProviders.cs` → `IAiMetadataProvider` implementieren, in `AiProviderFactory.Create` registrieren, Anbietername in `AiTemplate.Validate` erlauben |
| Was an die KI gesendet wird | `Services/AiMetadataService.cs` → `ModelContext` (Text) und `Preview` (Bild) |
| Lokale EXIF→XMP-Abbildung | `Services/AiMetadataService.cs` → `Read`, `LocalMappings`, `XmpCoordinate` |
| Schreibbare XMP-Zielfelder | `Services/AiMetadataService.cs` → Dictionary `Targets` (Allowlist) und `WriteSidecar` |
| Nutzungspräferenzen (LearningOptOutIn, IPTC Data Mining) | `Services/AiMetadataService.cs` → `Read` (setzt `AiImageRow.Preference`) |
| Budget, Kosten, Queue, Chat | `AiMetadataWindow.xaml.cs` → `AnalyzeAsync`, `AnalyzeOneAsync`, `ChatSend_Click`, `HandleChatReply` |
| API-Schlüssel speichern | `Services/CredentialStore.cs` (Windows Credential Manager) |
| Einstellungen hinzufügen | `AppSettings.cs` (Property ergänzen; `FilePath` nur für Tests setzen) |
| Globale Optik (Buttons, Schrift) | `App.xaml` |
| Lizenzliste aktualisieren | `scripts/Update-ThirdPartyLicenses.ps1`, danach `THIRD-PARTY-LICENSES.md` |
| Build- oder Release-Pipeline | `.github/workflows/build.yml`, `.github/workflows/release-on-tag.yml` |

## Wichtige Typen

| Typ | Datei | Zweck |
| --- | --- | --- |
| `ImageItem` | `Models/ImageItem.cs` | Eintrag der Bildliste im Hauptfenster (Pfad, Thumbnail, Lat/Lon, Auswahl) |
| `EditorSession` | `Services/EditorSession.cs` | Transaktionale, verlustfreie Bearbeitungshistorie eines Bildes (PNG-Checkpoints im Temp-Ordner) |
| `PixelGeometry` | `Services/PixelGeometry.cs` | Rechteck mit exklusiven Kanten, Fit-Zoom, GPS-Validierung |
| `JpegExifWriter` | `Services/JpegExifWriter.cs` | Ersetzt nur das EXIF-APP1-Segment eines JPEG |
| `ImageService` | `Services/ImageService.cs` | Thumbnails (mit Cache), GPS schreiben, Kopien/Exporte speichern |
| `AiTemplate` | `Models/AiModels.cs` | Geladene, validierte KI-Vorlage (JSON) |
| `AiImageRow`, `AiFieldChange` | `Models/AiModels.cs` | Bildzeile im KI-Fenster bzw. ein prüfbarer Änderungsvorschlag |
| `AiPrices`, `AiUsage`, `AiReply` | `Models/AiModels.cs` | Preise pro 1 Mio. Tokens, Token-Verbrauch, Antwort |
| `IAiMetadataProvider` | `Services/AiProviders.cs` | Anbieter-Schnittstelle (OpenAI/Azure: `OpenAiResponsesProvider`, Gemini: `GeminiProvider`) |
| `AiProviderException` | `Services/AiProviders.cs` | Fehler mit bereits verbrauchten Tokens; `Ambiguous` = Anfrage evtl. verarbeitet |
| `AiMetadataService` | `Services/AiMetadataService.cs` | Lokales Lesen, Vorschau, Vorschläge, Sidecar, Cache |

## Legacy (nicht verwendet)

Diese Dateien sind im Projekt, werden aber vom aktiven Code **nicht** benutzt. Bitte nicht erweitern; sie sind Kandidaten zum Entfernen:

| Datei | Hinweis |
| --- | --- |
| `Services/OptimizedImageService.cs` | Ältere Variante von `ImageService`; schreibt GPS noch per Neukodierung |
| `Services/ImageProcessingService.cs`, `Models/ImageDocument.cs`, `Models/ToolContext.cs`, `Models/ToolMode.cs` | Früherer Editor-Entwurf, ersetzt durch `EditorSession` |
| `Services/UndoService.cs` | Ersetzt durch die Historie in `EditorSession` |
| `Services/CoordinateMapper.cs` | Ersetzt durch `PixelGeometry` und WPF-Koordinatentransformation |
| `Assets/map.html` | Alte Karte; aktiv ist `Resources/map.html` |
| `ImageEditorWindow_BACKUP.xaml.cs`, `MainWindow.xaml.cs.backup`, `apply-updates.ps1`, `old/` | Sicherungen bzw. Hilfsskripte früherer Stände |

## Kommunikation Karte ↔ App

```text
map.js  ──postMessage──▶  MainWindow.CoreWebView2_WebMessageReceived
  {type:"ready"}                  → configure(...) senden, Marker + Raster setzen
  {type:"status", text}           → MapStatusText
  {type:"select", id}             → Bild mit Index id auswählen
  {type:"hover", id}              → Name/Trasse/Koordinaten im grauen Bereich (id −1 = Ende)
  {type:"routeHover", number}     → Trasseninfo (Bilder, Länge, Richtung)
  {type:"coordinates", lat, lng}  → aktuelle GPS-Auswahl (validiert)

MainWindow ──ExecuteScriptAsync──▶ map.js
  configure({url, attribution, attributionUrl})
  addImageMarkers([{id, lat, lng}], fit) · setSelectedMarker(lat,lng,center) · clearSelectedMarker()
  setRoutes([{number, lengthMeters, points:[[lat,lng],…]}]) · setRoadRoutes([{number, segments:[{onRoad, points}]}])
  setLayerVisible("routes"|"road"|"images", bool)
  setCurrentMarker(lat,lng) · clearCurrentMarker() · updateGrid(enabled, meter)
```

`id` ist der aktuelle Listenindex. Deshalb ruft jede Änderung an Liste oder GPS `RebuildRoutes` auf, das Trassen, Reihenfolge und Marker gemeinsam neu setzt.

Nachrichten werden nur vom Ursprung `https://picturegeoexif.local/` angenommen. Zahlen gehen kulturunabhängig (`InvariantCulture`) an JavaScript, Texte als JSON-serialisierte Strings.
