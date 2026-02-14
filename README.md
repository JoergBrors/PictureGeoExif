# PictureExifclone — Bild-Georeferenzierungs-Anwendung (Deutsch)

## Kurzbeschreibung

PictureExifclone ist eine WPF-Anwendung (.NET 8) zum Hinzufügen und Bearbeiten von GPS-EXIF-Daten in Bilddateien. Die Anwendung bietet Funktionen zum Laden, Anzeigen, Bearbeiten und Exportieren von Bildern sowie verschiedene Möglichkeiten, GPS-Koordinaten auszuwählen und in die EXIF-Metadaten zu schreiben.

In diesem README befindet sich die vollständige Nutzungs- und Build-Dokumentation. Per-Verzeichnis-READMEs beschreiben den Inhalt der Unterordner; diese sind mit diesem Root-README und dem Root-`changelog.md` verknüpft.

## Projekte in der Solution

- `PictureExifclone` — Hauptanwendung (WPF, .NET 8)
- `BenchmarkSuite1` — Benchmark-Projekt (falls vorhanden)

## Verzeichnis-READMEs

Zur besseren Struktur befinden sich in relevanten Unterverzeichnissen eigene `README.md`-Dateien. Diese beschreiben die Dateien im jeweiligen Verzeichnis und wie sie in das Gesamtprojekt passen.

Aktuelle Verzeichnis-READMEs:

- `Services/README.md` — Beschreibung der Service-Klassen (Thumbnails, EXIF, Speicher-APIs)
- `BenchmarkSuite1/README.md` — Benchmark-Project Anleitung

Verwenden Sie diese Verzeichnis-READMEs für kontextbezogene Details. Das Root-README bleibt die zentrale Einstiegspage.

## Changelog-Verhalten (Root & Verzeichnisse)

- Root-`changelog.md` im Repository-Stamm ist das zentrale Änderungsprotokoll und Release-Log.
- Falls ein Verzeichnis eigene Änderungen protokollieren möchte, erstellt dort eine lokale `changelog.md`.
  - Diese lokale Datei sollte eine kurze Überschrift und die Relevanz zur Root-Changelog enthalten.
  - Beispielkopf für `Services/changelog.md`:

    # Services Changelog
    Dies ist ein Verzeichnis-spezifisches Änderungsprotokoll. Wichtige Änderungen werden auch im Root-`changelog.md` referenziert.

- Empfehlung: Immer einen Eintrag im Root-`changelog.md` anlegen (oder referenzieren), wenn Änderungen die Benutzer-Funktionen betreffen oder Release-Notes erzeugt werden.

## Kurzübersicht der Funktionen

- Bilder per Datei-Dialog oder Drag & Drop laden
- Vorschau / Thumbnails (performance- und speicheroptimiert)
- Auswahl von GPS-Koordinaten über:
  - Interaktive Karte (WebView2 / OpenStreetMap / Leaflet)
  - Referenzbild mit vorhandenen GPS-EXIF-Daten
  - vorhandenes Bild bearbeiten (über Liste)
- GPS-Daten in EXIF schreiben (inkl. `GPSLatitude`, `GPSLongitude`, `GPSLatitudeRef`, `GPSLongitudeRef`)
- Einzelbild- oder Stapelspeicherung

## Kernkomponenten und Verhalten (Kurzdoku)

1. `ImageService` (Siehe `Services/ImageService.cs` / `Services/README.md`)
2. `OptimizedImageService` (Siehe `Services/OptimizedImageService.cs` / `Services/README.md`)

(Die ausführlichen API-Details und Beispiele befinden sich in `Services/README.md`.)

## Anleitungen für Entwickler

### Voraussetzungen

- .NET 8 SDK installiert
- (optional) Visual Studio 2022/2023 mit .NET Desktop-Entwicklung
- WebView2 Runtime installiert, falls Karte genutzt wird

### Projekt öffnen

- Lokales Klonen: `git clone https://github.com/JoergBrors/PictureGeoExif.git`
- Solution öffnen mit Visual Studio oder über CLI

### Build & Run (lokal)

- CLI (Debug):
  - `dotnet build PictureExifclone.csproj -c Debug`
  - `dotnet run --project PictureExifclone.csproj`
- Debug in Visual Studio: Solution öffnen, `PictureExifclone` als Startprojekt wählen und starten.

### Builden aus einem Git-Tag ("tag build")

Siehe Root-`changelog.md` für eine Schritt-für-Schritt-Anleitung, wie man Builds aus einem Tag erzeugt. Kurze Zusammenfassung:

1. `git fetch --all --tags`
2. `git checkout tags/<tag> -b build-<tag>`
3. `dotnet build PictureExifclone.csproj -c Release`
4. `dotnet publish PictureExifclone.csproj -c Release -o ./publish`

## Tests & Benchmarks

- `BenchmarkSuite1` kann mit `dotnet run --project BenchmarkSuite1/BenchmarkSuite1.csproj -c Release` gestartet werden.

## Mitwirken (Contributing)

- Bitte Issues im GitHub-Repository eröffnen und Pull Requests gegen `main` einreichen.
- Für grössere Änderungen bitte vorher ein Issue mit Design/Architekturvorschlag öffnen.

## Lizenz

- Prüfen Sie die enthaltene Lizenz-Datei im Repository (sofern vorhanden) für Nutzungs- und Verbreitungsbedingungen.

## Kontakt

- Projekt im lokalen Repo: `E:\Code\PictureGeoExif` (Beispielpfad)

```text
Hinweis: Verzeichnis-READMEs und lokale `changelog.md` sollten immer mit dem Root-`changelog.md` verknüpft werden, damit Release-Notes zentral gepflegt sind.
