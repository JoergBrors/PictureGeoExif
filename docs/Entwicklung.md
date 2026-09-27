# Entwicklung

## Voraussetzungen

- Windows 10 1809 oder neuer
- .NET SDK **10.0.401** (per `global.json` gepinnt, `rollForward: latestPatch`)
- WebView2 Runtime (für die Karte)
- Optional: Visual Studio 2022/2026 mit „.NET-Desktopentwicklung“, PowerShell 7 für das Lizenzskript

## Bauen, Starten, Testen

```powershell
dotnet build PictureExifclone.sln -c Debug          # 0 Warnungen Pflicht (TreatWarningsAsErrors)
dotnet run --project PictureExifclone.csproj
dotnet test PictureExifclone.sln -c Release          # xUnit, ca. 1–2 s
```

Release wie in der CI (self-contained, Single-File):

```powershell
dotnet publish PictureExifclone.csproj -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:SelfContained=true -o out
```

`Resources/` (Karte) und `Templates/` (KI-Vorlage aus `docs/examples`) liegen danach **neben** der EXE und müssen mit verteilt werden. Das Release-ZIP enthält sie.

## Tests

Projekt: `tests/PictureGeoExif.Tests` (referenziert die App direkt).

| Datei | Prüft |
| --- | --- |
| `MetadataTests.cs` | GPS-Schreiben: JPEG-Scan-Daten bytegleich, Idempotenz, ungültige Werte ändern nichts, PNG-Alpha, BMP-Ablehnung, kollisionsfreie Exporte; Sidecar-Merge, Revisionsschutz, Ziel-Allowlist; Vorschau ohne Metadaten; XMP-GPS-Format |
| `LogicTests.cs` | Auswahlgeometrie (Ränder, Richtungen, 1 px, leer/ungültig), Fit unter 10 %, Blur nur in der Auswahl, Verpixeln mit zu großem Block, Stempel-Anker; Vorlagen- und Antwortvalidierung, Schreibregeln, Schema-Übersetzung, Kostenformel |
| `WindowTests.cs` | `AiMetadataWindow` und `ImageEditorWindow` lassen sich auf einem STA-Thread erzeugen (XAML und Handler gültig) |

Regeln:

- **Tests dürfen keine Benutzerdaten anfassen.** Für `AppSettings` in Tests immer `new AppSettings { FilePath = <temp> }` verwenden. Das Standard-`Save()` schreibt nach `%APPDATA%`.
- Testbilder synthetisch erzeugen (ImageSharp); keine echten Fotos einchecken.
- Keine echten Netzaufrufe in Tests. `RoadMatcherTests` nutzen einen Fake-`HttpMessageHandler`; die Drosselung lässt sich per `minInterval: TimeSpan.Zero` abschalten. Der öffentliche FOSSGIS-Server darf nicht aus CI angesprochen werden.
- Keine echten KI-Aufrufe in Tests. Anbieterlogik wird offline über Schema, Parsing und Validierung geprüft.

## Konventionen

- **Encoding:** Alle Text-Quelldateien sind UTF-8. Keine Windows-1252-Dateien einchecken, sonst erscheinen Umlaute als „�“.
- **Warnungen:** `TreatWarningsAsErrors` ist aktiv. Ursachen beheben, kein `#pragma`, `NoWarn` oder `!` zur Symptomunterdrückung.
- **Dateischreiben:** immer über `AtomicFile.Write`, neue Exportnamen über `AtomicFile.ExportPath`. Originale nie überschreiben.
- **Koordinaten an JavaScript:** mit `InvariantCulture` formatieren (`MainWindow.Js`), Texte per `JsonSerializer.Serialize`.
- **Langlaufende Arbeit:** `Task.Run` plus `CancellationToken`; UI-Zustand erst nach Erfolg ändern.
- **Sprache:** Oberflächentexte Deutsch, Code-Kommentare Englisch oder Deutsch; den Stil der Umgebung übernehmen.
- **KI:** Neue Vorlagenfelder oder Zieltags nur mit Validierung in `AiTemplate.Validate` bzw. Eintrag in `AiMetadataService.Targets` und Tests.

## CI/CD

| Workflow | Auslöser | Schritte |
| --- | --- | --- |
| `.github/workflows/build.yml` | Pull Request, Push auf `main` | Build Debug, Tests Release, Paketaudit (bricht bei anfälligen Paketen ab) |
| `.github/workflows/release-on-tag.yml` | Tag-Push | Checkout **des Tag-Commits**, Tests, Single-File-Publish `win-x64` und `win-arm64`, ZIP inklusive Lizenzen, GitHub Release |

Ein Release entsteht durch Tag und Push:

```powershell
git tag v0.96; git push origin v0.96   # Version vorher im csproj (<Version>) anheben
```

Der ARM64-Build wird in der CI nur kompiliert. Ob er zur Laufzeit funktioniert, muss auf ARM64-Hardware geprüft werden.

## Abhängigkeiten und Lizenzen

- Paketänderungen im `PictureExifclone.csproj` vornehmen, dann:

  ```powershell
  dotnet list PictureExifclone.csproj package --vulnerable --include-transitive
  pwsh scripts/Update-ThirdPartyLicenses.ps1
  ```

- Anschließend [THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md) prüfen und ergänzen.
- **SixLabors-Pakete nicht ohne Lizenzprüfung auf eine neue Hauptversion heben** (Split License, siehe THIRD-PARTY-LICENSES, Abschnitt 3.1).
- `docs/examples/*.json` werden als `Templates\` ins Ausgabeverzeichnis kopiert; Änderungen dort betreffen die Standardvorlage der App.

## Fehlersuche

| Symptom | Ursache / Prüfung |
| --- | --- |
| Karte leer, Meldung „WebView2 Runtime prüfen“ | Runtime fehlt oder Profilordner `%LOCALAPPDATA%\PictureGeoExif\WebView2` nicht beschreibbar |
| „Kartenserver: HTTP 403/429“ | OSM blockiert oder drosselt; Tile-Anbieter in `settings.json` wechseln (siehe Betrieb) |
| KI: „Start blockiert: Preise fehlen“ | Preise und Prüfdatum im KI-Fenster eintragen |
| KI: „Kein API-Schlüssel“ | Schlüssel im Fenster speichern oder `OPENAI_API_KEY` / `GEMINI_API_KEY` / `AZURE_OPENAI_API_KEY` setzen |
| KI Azure: Umgebungsvariable fehlt | `PICTUREGEO_AZURE_OPENAI_ENDPOINT` und `PICTUREGEO_AZURE_GPT5_MINI_DEPLOYMENT` setzen (Namen aus der Vorlage) |
| „Unklar ob verarbeitet“ | Timeout oder Verbindungsabbruch nach dem Senden; bewusst ohne automatische Wiederholung, im Anbieterportal prüfen |
