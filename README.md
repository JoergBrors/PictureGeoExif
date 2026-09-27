# PictureGeoExif

WPF-Desktopanwendung (.NET 10, Windows) zum **Georeferenzieren, Bearbeiten und Verschlagworten von Fotos**. GPS-Koordinaten kommen aus einer OpenStreetMap-Karte oder einem Referenzbild und werden verlustfrei in die EXIF-Daten geschrieben. Ein Editor mit pixelgenauen Werkzeugen und ein optionales KI-Modul für Metadaten ergänzen die Anwendung. Originaldateien werden nie verändert.

Projekt- und Assemblyname: `PictureExifclone` · Lizenz: [MIT](LICENSE) · Drittanbieter: [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md)

## Funktionen

- **Bilder laden** per Dialog oder Drag & Drop (JPG, PNG, BMP, TIF) mit Thumbnails und GPS-Anzeige
- **Karte:** OpenStreetMap über lokal ausgeliefertes Leaflet in WebView2, Marker aller Bilder, Klick setzt Koordinaten, optionales Meter-Raster; der Kachelanbieter ist konfigurierbar
- **GPS schreiben:** JPEG verlustfrei (nur der EXIF-Block wird ersetzt), PNG/TIFF verlustfrei, jeweils nachgeprüft und atomar gespeichert; einzeln, für alle oder aus einem Referenzbild
- **Bildeditor:** Zuschneiden, Unschärfe, Verpixeln, Text, GPS-Stempel; pixelgenau und unabhängig von Zoom und DPI; Undo/Redo, Vorher/Nachher; Export als PNG, JPEG, TIFF oder BMP
- **KI-Metadaten (optional):** Jahreszeit, Titel und Stichwörter per OpenAI (`gpt-5-mini`), Azure OpenAI oder Gemini
  - JSON-Vorlagen, Stapelverarbeitung mit Budgetgrenzen, Ergebnis-Cache und Metadaten-Chat
  - Speichern erst nach Prüfung, als XMP-Sidecar mit Rückgängig
  - Datensparsam: keine exakten GPS-Daten, keine Pfade, Vorschau ohne Metadaten
- **Lizenzfenster** in der App

## Schnellstart

Voraussetzungen: Windows 10 1809+, [.NET SDK 10.0.401](global.json), WebView2 Runtime.

```powershell
git clone https://github.com/JoergBrors/PictureGeoExif.git
cd PictureGeoExif
dotnet build PictureExifclone.sln -c Debug
dotnet run --project PictureExifclone.csproj
dotnet test PictureExifclone.sln
```

Fertige Builds für x64 und ARM64 (self-contained, ohne .NET-Installation) stehen unter GitHub Releases.

## Dokumentation

| Thema | Dokument |
| --- | --- |
| Übersicht aller Dokumente | [docs/README.md](docs/README.md) |
| Wo liegt was im Code? | [docs/Code-Wegweiser.md](docs/Code-Wegweiser.md) |
| Architektur, Datenflüsse, Speicherorte | [docs/Architektur.md](docs/Architektur.md) |
| Build, Tests, CI/Release, Konventionen | [docs/Entwicklung.md](docs/Entwicklung.md) |
| Bedienung | [docs/Benutzerhandbuch.md](docs/Benutzerhandbuch.md) |
| Einsatz im Unternehmen (IT, Datenschutz, Lizenzen) | [docs/Betrieb-und-Unternehmenseinsatz.md](docs/Betrieb-und-Unternehmenseinsatz.md) |
| Modernisierungsplan und Umsetzungsstand | [docs/GUI-AI-Update-Plan.md](docs/GUI-AI-Update-Plan.md) |
| KI-Fachkonzept | [docs/AI-Metadata-Konzept.md](docs/AI-Metadata-Konzept.md) |
| Service-Klassen | [Services/README.md](Services/README.md) |
| Änderungen | [changelog.md](changelog.md) |

## Projektstruktur (Kurzform)

```text
MainWindow / ImageEditorWindow / AiMetadataWindow   WPF-Fenster
Services/        Bild-I/O, Geometrie, EXIF/XMP, KI-Adapter (ohne UI, getestet)
Models/          Datenobjekte
Resources/       Karte (map.html, map.js, Leaflet)
docs/examples/   KI-Vorlage und Antwortschema (werden als Templates/ ausgeliefert)
tests/           xUnit-Tests
licenses/        Lizenztexte aller ausgelieferten Komponenten
```

## Lizenz und Unternehmenseinsatz

Das Projekt steht unter der MIT-Lizenz und darf kommerziell genutzt werden. Die Bildbibliotheken von Six Labors unterliegen einer **Split License**: Für dieses Open-Source-Projekt gilt Apache-2.0. Proprietäre Weiterentwicklungen durch Unternehmen mit mindestens 1 Mio. USD Jahresumsatz benötigen eine kommerzielle Lizenz. Für OpenStreetMap-Kacheln gilt die OSM Tile Usage Policy. Einzelheiten und eine Freigabe-Checkliste stehen in [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md).

## Mitwirken

Issues und Pull Requests gegen `main` sind willkommen. Der PR-Workflow baut ohne Warnungen, führt die Tests aus und prüft die Pakete auf Schwachstellen. Bitte die Konventionen in [docs/Entwicklung.md](docs/Entwicklung.md) beachten.
