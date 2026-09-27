# GUI & AI Update – Analyse und Umsetzungsplan

Stand: 27.09.2026. Arbeitsbranch: `GUI-AI-Update` (Leerzeichen sind in Git-Branch-Namen nicht zulässig). Übersicht aller Dokumente: [docs/README.md](README.md); Umsetzungsstand: Abschnitt 10.

## Auftrag und Umfang

Zunächst Analyse und Plan, noch keine funktionalen Änderungen. Ziele: 46 Build-Warnungen an der Ursache beheben, die OpenStreetMap-Karte browser- und richtlinienkonform einbinden und die Bildbearbeitung modernisieren. „Openstreetview browser confirm“ wird als konforme OpenStreetMap-Einbindung interpretiert; eine Street-View-Funktion ist damit nicht eingeplant. KI ist im Branchnamen genannt, konkrete KI-Funktionen sind noch nicht spezifiziert.

## Reproduzierbarer Ausgangszustand

- Eine WPF-Anwendung in `PictureExifclone.sln`, Ziel `net8.0-windows`, Nullable aktiviert.
- Installiertes und beim Build verwendetes SDK: 10.0.401. Keine `global.json`.
- Release-Rebuild: `dotnet build PictureExifclone.sln -c Release --no-restore -t:Rebuild -v:minimal`.
- Ergebnis: **46 Warnungen, 0 Fehler**. Lokales vollständiges Protokoll: `build-analysis-release.log` (durch *.log ignoriert).
- Es wurde mit vorhandenen Restore-Artefakten gebaut. Ein frischer Restore, Paket-Sicherheitsaudit, Debug-Build und GUI-Laufzeittest sind noch nicht erfolgt.
- Kein Testprojekt in der Solution. README erwähnt ein hier nicht vorhandenes Benchmark-Projekt.
- Hauptlogik steckt in `MainWindow.xaml.cs` und `ImageEditorWindow.xaml.cs`. Vorhandene Services für Verarbeitung, Undo und Koordinaten sind im aktiven Editor nicht integriert. `OptimizedImageService` ist nicht der im Hauptfenster instanziierte Service.

## 1. Warnungen: Ursachen und konkrete Reparaturen

Die WPF-Zwischenkompilierung und das Hauptprojekt melden dieselben 23 Warnstellen jeweils einmal.

| Code | Warnstellen | Meldungen im Build | Ursache und Maßnahme |
| --- | ---: | ---: | --- |
| CA1416 | 17 | 34 | `GenerateAssemblyInfo=false`, kein `SupportedOSPlatform`-Attribut, manuelles `TargetPlatform("Windows7.0")`. Windows-Mindestversion konsistent deklarieren; WebView2 verlangt laut Build mindestens 10.0.17763.0. |
| CS0108 | 1 | 2 | `ImageEditorWindow.xaml:75`: Feld `TextInput` verdeckt `UIElement.TextInput`. In `WatermarkTextBox` umbenennen und Referenzen aktualisieren. |
| CS8618 | 1 | 2 | `ImageEditorWindow.xaml.cs:46`: `currentTempFilePath` wird nur indirekt in einer Hilfsmethode initialisiert. Pfad aus Initialisierung zurückgeben und im Konstruktor definitiv zuweisen; Fehlerpfad aufräumen. |
| CS8073 | 4 | 8 | `MainWindow.xaml.cs:431,677`: überflüssige Nullprüfung des GeoLocation-Werttyps entfernen. `ImageEditorWindow.xaml.cs:611,675`: Fonts-Werttyp nicht auf null prüfen; Schrift explizit über TryGet/erfolgreiche Enumeration auswählen und leere Sammlung behandeln. |

**Umsetzung:** Zunächst unter .NET 8 bereinigen, damit Warnungsbehebung und Framework-Migration unabhängig prüfbar bleiben. Assembly-Metadaten vorzugsweise ins Projekt verlagern, Generierung aktivieren, doppelte manuelle Attribute entfernen und `ThemeInfo` erhalten. Alternativ bei manueller Verwaltung ein korrektes `SupportedOSPlatform` setzen. Zielplattform und unterstützte Mindestversion müssen zusammenpassen. Das bisher hartkodierte `AssemblyConfiguration("Debug")` ebenfalls korrigieren.

**Abnahme:** Frischer Restore und Debug-/Release-Rebuild jeweils mit 0 Warnungen und 0 Fehlern; keine pauschalen `NoWarn`, `#pragma` oder Null-forgiving-Operatoren zur Symptombekämpfung. Danach `TreatWarningsAsErrors` in CI aktivieren. Microsoft dokumentiert genau den Zusammenhang zwischen deaktivierter AssemblyInfo-Generierung und CA1416: [CA1416](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1416).

## 2. OpenStreetMap: Sperre beheben und Browserintegration absichern

**Befund:** `MainWindow.xaml.cs:66–72` lädt per `NavigateToString(GetLeafletHtml())`. Tatsächlich verwendet wird das inline erzeugte HTML; die ebenfalls vorhandene `Resources/map.html` ist kein alleiniger Quellstand. Beide verwenden Subdomain-Tiles. Der Screenshot zeigt eine explizite OSM-Zugriffssperre.

**Ursachenhypothese:** `NavigateToString` verwendet `about:blank` ohne regulären Seitenursprung. Ein fehlender Referer ist deshalb plausibel. Der tatsächliche Sperrgrund ist ohne Netzwerkprotokoll noch nicht bewiesen; Netzwerk-/IP-Sperren sind ebenfalls möglich. Eine reine Änderung des Browser-User-Agents ist kein belastbarer Nachweis der Behebung.

**Arbeitsschritte:**

1. Kleine interaktive Sitzung protokollieren: Tile-URL, Status, User-Agent, Referer und Cache-Verhalten; keine automatisierten Massenabrufe.
2. `Resources/map.html` zur einzigen Kartenimplementierung machen. Leaflet-JS/CSS lokal versioniert ausliefern, einschließlich Lizenz.
3. Lokale Kartenressourcen mittels WebView2 Virtual Host Mapping über einen stabilen HTTPS-Ursprung bereitstellen. Nur das dedizierte Webressourcen-Verzeichnis freigeben. Im Single-File-Publish die Bereitstellung/Extraktion der Ressourcen ausdrücklich testen.
4. Tile-URL auf `https://tile.openstreetmap.org/{z}/{x}/{y}.png` umstellen. Die Anwendung mit stabiler App-Kennung und Kontakt-/Projekt-URL identifizieren; Referer aus dem tatsächlichen Seitenursprung senden. Keine fremde Browseridentität vortäuschen und keinen fremden Referer erfinden.
5. Persistentes WebView2-Profil unter LocalAppData verwenden, normales HTTP-Caching erhalten. Keine Cache-Bypass-Header und keine Offline-/Bulk-Downloads.
6. Sichtbare, anklickbare Attribution „© OpenStreetMap contributors“ mit Copyright-Link. Provider-URL und Attribution gemeinsam konfigurierbar machen.
7. Ladezustand sowie verständliche Fehleranzeige bei Netzwerkproblemen/403/429; keine unendlichen Wiederholungen. Ein 403 kann auch als Bildinhalt erscheinen, deshalb Netzantworten und Sichtprüfung berücksichtigen.
8. Initialisierung über `Task`, NavigationCompleted bzw. explizite Ready-Nachricht koordinieren, Script-Aufrufe awaiten, Fehler erfassen und WebView2 beim Schließen entsorgen.
9. Webnachrichten als typisierte JSON-Nachrichten validieren: erlaubter Ursprung, Nachrichtentyp und endliche GPS-Werte innerhalb der Grenzen. Dateinamen als Text ins DOM einsetzen, nicht als Popup-HTML. Navigation auf erlaubte Ziele begrenzen.
10. Raster begrenzen: Die aktuelle verschachtelte Rechteckschleife kann bei großer Kartenausdehnung und 100-m-Raster extrem viele Objekte erzeugen. Zoomschwelle, maximale Objektzahl und geeignete metrische Projektion vorsehen.

**Abnahme:** Karte in WebView2 und Edge/Chrome manuell prüfen; Standortwahl, Marker, Größenänderung und Attribution funktionieren. Tatsächliche Header und Cache-Wiederverwendung nachweisen. Offline-/403-/429-Szenarien mit kontrollierten Testantworten prüfen. Keine Zusage dauerhafter OSM-Verfügbarkeit.

Grundlagen: [OSM Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/) und [WebView2: lokale Inhalte und Virtual Host Mapping](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content).

## 3. Bildbearbeitung: Datenintegrität zuerst

### Konkrete Probleme im aktuellen Code

- Sechs Editor-Operationen verwenden explizit `JpegEncoder { Quality = 95 }`, behalten aber den aus dem Original abgeleiteten Dateipfad. Auch `ImageProcessingService.SaveImage` erzwingt JPEG. PNG-Dateien können so JPEG-Inhalt erhalten; Transparenz geht verloren. Wiederholte JPEG-Kodierung verursacht zusätzliche Qualitätsverluste.
- `CreateNewTempVersion` bildet Namen aus `tempFileHistory.Count`. Nach dem Limit von zehn Einträgen wird derselbe Name erneut erzeugt; Kopieren auf sich selbst kann fehlschlagen. Die Ausnahme wird abgefangen und die anschließende Bearbeitung kann einen bestehenden Zustand verändern. Aus Code abgeleitet, noch nicht per GUI reproduziert.
- Undo existiert, Redo fehlt. Die Historie wird vor dem erfolgreichen Abschluss einer Operation geändert.
- Bildoperationen laufen synchron in UI-Handlern. Vollständiges Laden und Speichern sowie explizites GC belasten die Interaktion.
- `SaveEditedImage` übernimmt die ursprüngliche Dateiendung; Dateinamen mit Sekundengenauigkeit können kollidieren. Löschen und anschließendes Verschieben ist kein ausfallsicherer atomarer Ersatz.
- Kein durchgängiges AutoOrient-Konzept erkennbar; Rotation, Crop und Metadaten müssen auf denselben normalisierten Pixelkoordinaten beruhen.

### Ziel und Umsetzung

1. Einheitliches Editor-Dokument mit Originalreferenz, normalisierter Orientierung, Metadaten und Bearbeitungszustand einführen. Originale standardmäßig unverändert lassen.
2. Verlustfreie interne Bearbeitung: Arbeitsbild/Operationen im Speicher mit begrenztem Budget und bei Bedarf verlustfreien Checkpoints. JPEG erst beim Export kodieren.
3. Zentralen Exportservice einführen: Format, Encoder und Dateiendung stimmen überein; JPEG-Qualität wählbar, PNG-Alpha erhalten. Unterstützte Formate explizit definieren, unbekannte/mehrseitige/animierte Formate kontrolliert behandeln.
4. EXIF/GPS und ICC-Profile gezielt übernehmen; Orientation nach Pixel-Normalisierung korrigieren. Eine explizite Option zum Entfernen privater Metadaten anbieten. GPS-Änderungen und reine Metadaten-Exporte separat auf unnötige Neukodierung prüfen.
5. Undo/Redo mit eindeutigen IDs und Speicher-/Plattenbudget umsetzen. Zustand erst nach erfolgreicher Operation veröffentlichen; Fehler und Abbruch verändern den vorherigen Zustand nicht.
6. Verarbeitung aus dem Code-behind in einen gemeinsam genutzten Service verschieben. Vorhandene Services erst prüfen und zusammenführen, nicht unverändert anschließen. Abbruch, Fortschritt und Sperre konkurrierender Mutationen vorsehen.
7. Eine zentrale Transformation für Zoom, Pan, DPI, Letterboxing und Pixelkoordinaten verwenden. Crop auf Bildgrenzen beschränken; Vorschau und exportiertes Resultat müssen deckungsgleich sein.
8. Kollisionsfreie Exportnamen, temporäre Datei im Zielverzeichnis, vollständige Validierung und anschließendes sicheres Veröffentlichen. Fehlgeschlagene Exporte dürfen bestehende Dateien nicht löschen. Ungespeicherte Änderungen auch beim Schließen per X behandeln.

**Gezielte Tests:** JPEG/PNG mit Alpha, EXIF-Orientierungen 1–8, GPS in allen Hemisphären und Nullkoordinaten, ICC-Erhalt, Crop bei Zoom/DPI, mindestens 25 Bearbeitungsschritte mit Undo/Redo, verzweigter Verlauf, Abbruch, beschädigte Datei und nicht beschreibbares Ziel. Dateisignatur gegen Endung prüfen; Original-Hash muss unverändert bleiben. Die Formatbehandlung richtet sich nach der [ImageSharp-Dokumentation](https://docs.sixlabors.com/articles/imagesharp/imageformats.html).

## 4. GUI modernisieren

- WPF beibehalten und ViewModels/Commands schrittweise einführen; keine komplette Framework-Neuentwicklung für diese Reparaturen.
- Größenveränderbare Bildliste, große Karten-/Bildansicht und klar gegliederte Werkzeuge. Auswahl, Änderungen und laufende Vorgänge sichtbar darstellen.
- Gemeinsame Styles für Typografie, Abstände, Farben und Buttons; hoher Kontrast, Tastaturbedienung, Fokuszustände und DPI-Skalierung.
- Editor: Crop, Rotation, Text/GPS-Wasserzeichen und Verpixelung erhalten; Undo/Redo, Vorher/Nachher, Zoom auf 100 % und Einpassen anbieten. Weitere Werkzeuge separat priorisieren.
- Bildliste virtualisieren, Vorschaudaten begrenzen und große Stapel mit Fortschritt/Abbruch laden.
- Deutsche Texte und Icons konsistent pflegen; mögliche Encoding-Probleme anhand der echten UTF-8-Dateien prüfen, nicht aus PowerShell-Darstellung ableiten.

**Abnahme:** Bedienung bei 100/150/200 % Skalierung, kleinem Fenster und per Tastatur; keine verdeckten Aktionen/Attribution, keine eingefrorene Oberfläche bei längeren Operationen.

## 5. Aktueller technischer Stand und optionale KI

- Nach der separaten Warnungsbereinigung Migration auf .NET 10 LTS einplanen. Microsoft führt .NET 8 bis 10.11.2026 und .NET 10 bis 14.11.2028 als unterstützt: [Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). Konkrete SDK-Patchversion unmittelbar bei Umsetzung verifizieren und mit `global.json` festhalten.
- NuGet-Pakete einschließlich transitiver Abhängigkeiten auf verfügbare stabile Versionen, Schwachstellen, Breaking Changes und Lizenzverträglichkeit prüfen. Die installierten Versionen sind noch nicht als „neueste“ oder sicher zertifiziert. Keine pauschale Aktualisierung sämtlicher Pakete ohne Kompatibilitätstest.
- WebView2 SDK und Evergreen Runtime getrennt betrachten; fehlende Runtime verständlich melden.
- Konkretisiertes KI-Paket: Bildanalyse für Metadaten mit Azure GPT-5 mini, OpenAI und Google Gemini, JSON-Vorlagen, Batch und eigenem Chatfenster. Siehe Abschnitt 9 und das separate KI-Konzept. Generative Pixelbearbeitung bleibt ein möglicher späterer Ausbau.
- KI-Ergebnis stets als überprüfbare, rückgängig machbare Variante; keine automatische Änderung von GPS-Koordinaten aufgrund einer KI-Vermutung. Schlüssel nicht in Quellcode oder WebView-Inhalte einbauen.

## 6. Reihenfolge und Lieferpakete

| Reihenfolge | Paket | Fertig, wenn … |
| --- | --- | --- |
| 0 | Branch und Analyse | Branch erstellt, 46 Warnungen reproduziert, dieser Plan vorhanden. Erledigt. |
| 1 | Warnungsursachen | Debug und Release ohne Warnungen; CI verhindert Rückfälle. |
| 2 | OSM/WebView2 | Richtlinienkonforme Requests nachgewiesen, Karte und Fehlerzustände geprüft. |
| 3 | Editor-Datenintegrität | Formate/Metadaten korrekt, keine Historienkollisionen, sicherer Export. |
| 4 | Editor und GUI | Services integriert, Undo/Redo, asynchrone Verarbeitung, DPI-/Bedientests bestanden. |
| 5 | Laufzeit/Pakete/Release | .NET 10 und geprüfte Abhängigkeiten, reproduzierbare Builds und Publish-Smoke-Tests. |
| 6 | KI-Metadaten, Batch und Chat | Anbieteradapter, JSON-Vorlagen, eigenes Fenster, Kostenkontrolle und überprüfbare Metadatenänderungen gemäß Abschnitt 9 umgesetzt. |

Release-Workflow ergänzend korrigieren: Er reagiert aktuell auf Tags, checkt aber ausdrücklich `main` aus. Künftig den tatsächlichen Tag-Commit bauen. Für Pull Requests einen Build-/Test-Workflow ohne Veröffentlichung ergänzen. Win-x64 und Win-arm64 veröffentlichen und auf passender Hardware bzw. Testumgebung prüfen; Compile-Erfolg allein belegt keine ARM64-Laufzeitfunktion.

Empfohlene getrennte Commits: `fix/build-warnings`, `fix/osm-webview`, `fix/editor-integrity`, `refactor/editor`, `feat/gui-update`, `chore/dotnet10-ci`. Keine automatische Veröffentlichung während der Analyse.

## 7. Ergänzung: AI/ML-Nutzungspräferenzen mit LearningOptOutIn

Geprüft am 27.09.2026. **Empfehlung: als konkretes Metadaten-Feature aufnehmen**, unabhängig davon, ob später KI-Bildbearbeitung eingebaut wird. Die Funktion benötigt weder ein Modell noch einen Cloudanbieter. Dies erweitert den bisherigen Umfang von Paket 3 (Metadatenintegrität) und Paket 4 (GUI).

### Verifizierter Stand und Grenzen der Prüfung

CIPA führt Exif 3.1 als DC-008-Translation-2026 und die aktuelle XMP-Abbildung als DC-010-2026. ExifTool implementiert `LearningOptOutIn` in `Exif.pm` unter Tag-ID `0x9287`. Seine Kategorien umfassen eine allgemeine Angabe, nichtgeneratives Training, generatives Training, Data Mining sowie Eingabe in ein bereits trainiertes Foundation Model. Die Zustände sind Opt-out, Opt-in und Unspecified. Technisch enthält der Tag eine Anzahl und Kategorie/Zustand-Paare; ExifTool schreibt UNDEFINED und interpretiert den Inhalt als vorzeichenlose 16-Bit-Werte. Das ist kein einzelnes boolesches Feld. Quelle: [ExifTool-Implementierung](https://github.com/exiftool/exiftool/blob/master/lib/Image/ExifTool/Exif.pm).

Die CIPA-Veröffentlichungen sind bestätigt, die vollständigen Norm-PDFs waren über den verwendeten Webzugriff nicht abrufbar. Vor produktivem Schreiben sind insbesondere Byteordnung, erlaubte Anzahl/Reihenfolge, allgemeine Vorgabe versus Einzelkategorien, reservierte Werte, ExifVersion sowie die genaue XMP-Struktur anhand der Norm zu bestätigen. ExifTool ist eine Implementierungsreferenz, kein Ersatz für diesen Abgleich. In der geprüften zentralen `XMP.pm` wurde der Name nicht gefunden; daraus folgt keine Aussage über sämtliche ExifTool-Module.

### Empfohlene Architektur

| Baustein | Aufgabe | Grenze |
| --- | --- | --- |
| MetadataExtractor | Bestehendes Lesen von EXIF/GPS/IPTC/XMP beibehalten | Kein allgemeiner Metadaten-Writer; neue Tag-Namen nicht voraussetzen. |
| XmpCore | XMP parsen, gezielt ändern und serialisieren | Kein vollständiger JPEG-/PNG-/TIFF-Container-Writer. |
| Eigener Exif31Codec | Gezielt `LearningOptOutIn` decodieren/validieren/encodieren | Keine komplette EXIF-3.1-Implementierung versprechen. |
| MetadataContainerWriter | EXIF-/XMP-Blöcke ersetzen oder ergänzen, Bilddaten erhalten | Separater, anspruchsvoller Teil mit Formatgrenzen und Offset-Erhaltung. |
| ExifTool in Tests | Unabhängig lesen, Referenzdateien schreiben und Warnungen prüfen | Version/Commit pinnen; keine Laufzeitabhängigkeit der Anwendung. |

`obj/project.assets.json` belegt bereits `MetadataExtractor/2.9.0 -> XmpCore/6.1.10.1`. Bei direkter Verwendung der XmpCore-API eine explizite kompatible PackageReference aufnehmen. Neue XMP-Namensräume können über die generischen APIs modelliert werden; dazu ist nicht zwingend ein neuer Bibliotheksrelease nötig. [XmpCore-Projekt](https://github.com/drewnoakes/xmp-core-dotnet), [MetadataExtractor-Projekt](https://github.com/drewnoakes/metadata-extractor-dotnet).

### Datenmodell und Bedienung

- `LearningPreferences` mit allgemeinen und kategorisierten Angaben modellieren. Rohe unbekannte Codes, Herkunft und Originaldaten zusätzlich erhalten.
- `Missing`, `Unspecified`, `Unknown`, `Invalid` und widersprüchliche Quellen unterscheiden. Ein fehlender Tag bedeutet keine ausdrückliche Zustimmung. „Nicht angegeben“ und „Tag entfernen“ sind verschiedene Aktionen.
- Im GUI einen Bereich „KI-Nutzung & Data Mining“ mit allgemeiner Vorgabe und vier Einzelkategorien anbieten. Werte: „Nicht erlaubt“, „Erlaubt“, „Nicht festgelegt“. Die genaue Vererbungsdarstellung folgt dem Normabgleich.
- Einzelbild und Mehrfachauswahl unterstützen. Bei gemischten Werten „Unterschiedlich“ anzeigen; Stapeländerungen zunächst als Vorschau, anschließend explizit speichern.
- Bestehende Präferenzen beim Laden nicht verändern. Bei Crop, Rotation, GPS-Änderung und Export mitführen. Metadatenbereinigung in „Standort entfernen“, „persönliche Daten entfernen“ und „alle Metadaten entfernen“ unterscheiden; letzteres entfernt auch diese Angaben und muss dies anzeigen.
- Kurzer UI-Hinweis: Diese Angaben beschreiben Nutzungspräferenzen und verhindern technisch weder Kopieren noch KI-Verarbeitung. Keine Aussage über garantierte rechtliche Durchsetzung.

### EXIF, XMP und IPTC richtig zusammenführen

EXIF und XMP sind unterschiedliche Repräsentationen. IPTC ist zugleich ein Metadatenschema, dessen moderne Eigenschaften häufig in XMP gespeichert werden; es sind deshalb nicht einfach drei austauschbare Kopien desselben Tags.

1. EXIF `LearningOptOutIn` unterstützen. Für seine XMP-Repräsentation die verbindliche Abbildung in CIPA DC-010-2026 verwenden; bis diese geprüft ist, keinen eigenen Namen als angeblichen Standard exportieren.
2. Zusätzlich die IPTC/PLUS-Eigenschaft „Data Mining“ berücksichtigen. Sie ist bereits im IPTC-Standard enthalten, aber ihre Semantik ist nicht pauschal identisch mit den feineren EXIF-Kategorien. Keine verlustbehaftete automatische Gleichsetzung.
3. Konflikte zwischen eingebettetem EXIF, XMP und Sidecar sichtbar machen. Beim reinen Lesen keine Quelle überschreiben. Synchronisierung nur für eindeutig abbildbare Werte und mit sichtbarer Änderungsübersicht.
4. Unbekannte XMP-Eigenschaften und fremde EXIF-Tags erhalten. Ein Standardschema kann durch XmpCore generisch verarbeitet werden, ohne IPTC-IIM-Felder zu erfinden.

Quellen: [CIPA-Standards und Downloads](https://www.cipa.jp/e/std/std-sec.html), [IPTC Photo Metadata 2025.1](https://iptc.org/std/photometadata/specification/IPTC-PhotoMetadata-2025.1.html), [IPTC Opt-out-Empfehlungen](https://iptc.org/news/iptc-publishes-best-practice-guidance-on-generative-ai-opt-out-for-publishers/).

### Sicheres Schreiben als eigenes Lieferpaket

Die heutige Methode `ImageService.WriteGpsToImage` lädt und speichert das gesamte Bild über ImageSharp. Für reine Rechte-/GPS-Metadaten ist das kein geeignetes Endziel, weil dabei erneut kodiert wird und der Erhalt unbekannter Tags nicht nachgewiesen ist.

- Zuerst Lesen und Verlustfreiheit bei bestehenden Bearbeitungs-/Exportwegen absichern, danach Schreiben freischalten.
- Erster Schreibumfang: klar begrenzte JPEG-Dateien. APP1-Segmente, TIFF-IFDs, Größenlimits, Offsets, MakerNotes und eventuell mehrere EXIF/XMP-Blöcke beachten. Bilddaten und unberührte Blöcke unverändert übernehmen.
- Einen kleinen Tag-Codec von einem komplexeren Container-Writer trennen. Unbekannte Strukturen nicht blind neu serialisieren; bei nicht sicher unterstützten Layouts Schreiben verweigern und Original erhalten.
- XMP-Sidecars als explizite Option für nicht unterstützte Container; sie sind keine in das Bild eingebetteten EXIF-Tags und werden nicht überall automatisch berücksichtigt.
- PNG mit eXIf/XMP sowie TIFF separat als spätere Formatpakete prüfen. RAW, HEIC, AVIF, BigTIFF und Multi-Picture-Dateien zunächst nicht schreibend versprechen.
- Erst in temporäre Datei schreiben, erneut lesen und prüfen, dann sicher veröffentlichen. Der gesamte Metadatenänderungssatz muss erfolgreich sein oder das Original bleibt unverändert.

### Validierungs- und Testplan

1. CIPA-Regeln und Taglayout als kleine Spezifikationsnotiz mit Version dokumentieren; passende ExifTool-Version/Commit und Prüfsumme festhalten.
2. Referenzdateien für allgemeine und individuelle Regeln, alle drei Zustände sowie fehlende/unbekannte/ungültige Werte anlegen. Little-/Big-Endian, doppelte Kategorien, verkürzte Daten und falsche Längen prüfen.
3. ExifTool erzeugt Dateien, die der .NET-Reader versteht; der .NET-Writer erzeugt Dateien, die ExifTool mit denselben Werten liest. Zusätzlich unabhängige erwartete Bytefolgen prüfen, damit beide Implementierungen nicht denselben Fehler übersehen.
4. Read-only-Prüfung beispielsweise mit `exiftool -j -G1 -a -s -LearningOptOutIn -ExifVersion bild.jpg`; ergänzend numerische Ausgabe und `-validate -warning -error` erfassen. Bereits vorhandene Warnungen von neu verursachten unterscheiden.
5. Bei reinen Metadatenänderungen unveränderte JPEG-Bilddaten nachweisen. GPS, ICC, Thumbnail, MakerNotes und nicht bearbeitete XMP-Eigenschaften vergleichen; wiederholtes Schreiben muss idempotent sein.
6. Crop/Rotation/Export/Undo/Redo dürfen die Präferenzen nicht stillschweigend verlieren. Konflikte und nicht unterstützte Formate müssen zuverlässig angezeigt werden.

### Open-Source-Einordnung

Für den vorgeschlagenen Metadatenkern ist der Ansatz gut geeignet: MetadataExtractor nennt Apache-2.0, XmpCore BSD. ExifTool kann als separat verwaltetes Testwerkzeug dienen. Daraus folgt aber nicht automatisch „100 % Open Source“ für die gesamte Desktop-Anwendung: Sie verwendet auch WebView2 und SixLabors-Pakete. SixLabors beschreibt für die eingesetzten Hauptversionen eine nutzungsabhängige Split-Lizenz; die konkreten Paketlizenzen und die beabsichtigte Distribution müssen zusammenpassen. Ein pauschales Lizenzversprechen wird deshalb nicht in den Plan übernommen. [SixLabors-Lizenzmodell](https://sixlabors.com/posts/license-changes/).

**Neue Reihenfolge innerhalb der Metadatenarbeit:** Normabgleich und Reader → Erhalt beim Editieren → begrenzter verlustfreier JPEG-Writer → GUI und Stapeländerung → XMP-Synchronisierung/IPTC-Abbildung → weitere Container. Die normative XMP-Abbildung bleibt bis zum vollständigen Normabgleich ausdrücklich offen. In diesem Schritt wurden nur Recherche und Planung ergänzt, noch keine Pakete installiert oder Bilddateien verändert.

## 8. Verbindliche Editor-Anforderung: bildgrößenunabhängige Werkzeuge

Ergänzt nach gezielter Codeprüfung am 27.09.2026. Crop, Blur/Verpixelung, GPS-Stempel und Text müssen für jede unterstützte Bildgröße und Orientierung dieselbe Auswahl in Vorschau und Export verwenden. „Jede Bildgröße“ bedeutet innerhalb eines expliziten Speicher-/Decoderlimits; überschrittene Limits müssen vor Bearbeitungsbeginn verständlich gemeldet werden. Kein stilles Herunterskalieren des Exports.

### Konkrete Befunde

| Stelle | Befund | Auswirkung |
| --- | --- | --- |
| `ImageEditorWindow.xaml`, ImageScrollViewer.LayoutTransform | Zoom transformiert den ScrollViewer einschließlich seiner Oberfläche. | Viewport, Scrollen und Einpassen sind unnötig miteinander gekoppelt. |
| `LoadAndDisplayImage`, `ZoomFit_Click` | Einpassen wird auf mindestens 0,1 begrenzt. | Sehr große Bilder passen nicht vollständig: 20000 px bei 800 px verfügbarer Breite benötigen 4 %, nicht 10 %. |
| XAML PreviewMouseWheel und Konstruktor Zeile 84 | Derselbe Mausradhandler wird zweimal registriert. | Doppelregistrierung entfernen; genau eine Zoomänderung pro Ereignis als Laufzeitkriterium prüfen. |
| `Image_MouseDown`, `Canvas_MouseMove` | Umrechnung mehrfach über ActualWidth/Height; Auswahl lebt überwiegend als Canvas-Rechteck. Keine Mausaufnahme. | Nicht zentral abgesicherte Geometrie; Ziehen über Bildgrenzen und Loslassen außerhalb können einen inkonsistenten Auswahlzustand hinterlassen. |
| `ApplyCrop_Click`, `ApplyPixelate_Click` | Nur Breite wird auf mindestens 10 Anzeigeeinheiten geprüft; Höhe fehlt. Ganzzahlwerte werden separat abgeschnitten. | Dünne gültige Pixelbereiche werden abgelehnt, leere Bereiche können als Erfolg erscheinen, Rundungsfehler am Rand. |
| Dieselben Handler | X/Y werden auf null gesetzt, Breite/Höhe aber nicht um den abgeschnittenen negativen Anteil verkürzt. | Keine echte Rechteckschnittmenge: Auswahl x=-20, Breite=50 ergibt aktuell x=0, Breite=50 statt Breite=30. Rechnerisch anhand der vorhandenen Formel bestätigt. |
| `CoordinateMapper.TryViewToPixel` | Punktbegrenzung auf W-1/H-1. | Für Pixelpositionen brauchbar; darf nicht unverändert für exklusive Crop-Rechts-/Unterkanten W/H verwendet werden. |
| Rotation, Undo, Crop | Rotation und Undo setzen Marker/Auswahl nicht zurück; Crop versteckt nur das Rechteck, nicht den gespeicherten Klickpunkt. | Alte Positionen können nach Größen-/Orientierungswechsel auf andere oder außerhalb liegende Pixel zeigen. |
| `ApplyGeo_Click`, `ApplyText_Click` | Nur Klickpunkt, keine Textmessung/Bounding-Box; feste Schriftbereiche, 2-px-Schatten und erste Systemschrift. | Lange Texte/GPS-Zeile werden rechts/unten abgeschnitten; auf kleinen Bildern übergroß, auf großen Bildern kaum sichtbar. |
| Aktiver Editor gegenüber `ImageProcessingService` | GPS-Zeile im Editor einzeilig, im Service zweizeilig; unterschiedliche Schriftwahl. | Ein späterer Service-Wechsel würde das Aussehen ändern. |
| `ApplyPixelate_Click` | Verpixelung ist vorhanden, echter Blur nicht. | Blur als eigenes Werkzeug ergänzen, nicht nur das bestehende Werkzeug umbenennen. |

Dies sind statische Befunde und zwei rechnerische Gegenbeispiele; ein interaktiver GUI-Test wurde in diesem Analyseschritt nicht durchgeführt.

### Gemeinsame Geometrie als Voraussetzung

1. Eine Bildoberfläche mit explizitem Bezug zwischen normalisierten Bildpixeln und WPF-DIPs festlegen. Bild-DPI und Monitor-DPI unterscheiden. EXIF-Orientierung einmal normalisieren; alle Werkzeuge arbeiten danach im selben Koordinatenraum.
2. Nur Bildinhalt und Overlay gemeinsam transformieren, den ScrollViewer als festen Viewport belassen. Aktuelle ViewportWidth/Height nach Layout verwenden. Einpassen dynamisch auch unter 10 % erlauben; Fensteränderungen im Fit-Modus nachführen. Zoom am Mauszeiger verankern und Scrollposition stabil halten.
3. Auswahl als kanonisches Rechteck in Bildpixeln speichern, Overlay daraus zeichnen. WPF-Eingaben zunächst in den lokalen Bildraum transformieren; Zoom/Scroll nicht doppelt einrechnen. Keine Pixelwerte aus einem möglicherweise veralteten Overlay rekonstruieren.
4. Für Crop exklusive Kanten verwenden: links/oben abrunden, rechts/unten aufrunden; anschließend alle Kanten auf [0,W] bzw. [0,H] begrenzen. Breite=Rechts-Links, Höhe=Unten-Oben. Alle Ziehrichtungen normalisieren. Mindestens 1×1 Pixel; leere/nicht endliche Bereiche ablehnen, ohne Historie oder Bild zu ändern.
5. Pointer beim Ziehen aufnehmen; MouseUp, Escape, Capture-Verlust und Fenster-Deaktivierung sauber behandeln. Start außerhalb des Bildes ignorieren; Endpunkte außerhalb begrenzen. Anzeigeeinheiten nicht als fachliche Mindestgröße verwenden.
6. Bei Crop/Rotation/Undo/Redo flüchtige Auswahl und Klickmarker im ersten Ausbau löschen. Spätere persistente Textobjekte explizit mittransformieren; keine stillschweigende Wiederverwendung alter Koordinaten.

### Crop, Blur und Verpixelung

- Crop zeigt Ausgabemaße in Pixeln und optional feste Seitenverhältnisse. Vollbildauswahl muss exakt W×H liefern, einschließlich rechter/unterer Randpixel.
- Blur erhält einen eigenen Radiusparameter; Verpixelung einen eigenen Blockgrößenparameter. Relative Stärke auf Basis der ausgewählten Bildregion anbieten, die resultierenden Pixelwerte anzeigen. Zoom verändert die Stärke niemals.
- Parameter aus tatsächlicher Auswahl-/Bildgröße ableiten und gegen die jeweils zulässigen Algorithmusgrenzen prüfen. Sehr kleine oder schmale Bereiche kontrolliert verarbeiten; ein Block größer als die Region darf keine unbehandelte Ausnahme auslösen.
- Für Blur eine definierte Randbehandlung festlegen: Kontext um die Auswahl für die Filterberechnung lesen, ausschließlich innerhalb der Auswahl zurückschreiben. Kein ungeplanter Effekt außerhalb der markierten Region. Alpha korrekt verarbeiten; halbtransparente Pixel beim Zurückschreiben nicht versehentlich erneut über das Original mischen.
- Vorschau mit demselben Effekt-/Koordinatenmodell berechnen. Bei reduzierter Vorschauauflösung den Filterradius/Blockmaßstab anpassen, Export auf voller Auflösung ausführen. Vorschaujobs abbrechen oder verwerfen, wenn ihre Dokumentversion veraltet ist.
- Außerhalb der Region muss das verlustfreie Arbeitsbild pixelidentisch bleiben. Bei späterem JPEG-Export können Kompressionsunterschiede auftreten; deshalb diese Invariante vor JPEG-Kodierung bzw. anhand eines PNG-Exports prüfen.

### GPS-Stempel und Text

- Relative Standardgröße, z. B. konfigurierbarer Anteil der kurzen Bildkante, mit manuellem Pixelmodus. Abstände, Hintergrund und Schatten ebenfalls skalieren. Messung bestimmt zusätzlich, ob der komplette Text ins Bild passt.
- Platzierung über Bildanker (vier Ecken, Mitte) oder freien Punkt. Gesamten Text einschließlich Schatten/Innenabstand messen; in der Vorschau Begrenzungsrahmen und tatsächliches Erscheinungsbild zeigen. Bei Platzmangel umbrechen/verkleinern oder klar ablehnen; keine still abgeschnittenen Inhalte, insbesondere bei winzigen Bildern.
- Dieselbe Schriftdatei, Metrik und Layoutberechnung für Vorschau und Export; deterministische lizenzierte Fallback-Schrift statt zufälliger erster Systemschrift. Schriftgrößeneinheiten und Render-DPI explizit umrechnen. Mehrzeiligen Text, Umlaute, Gradzeichen und nicht verfügbare Glyphen prüfen.
- GPS aus dem aktuellen Bild-Dokument beziehen. Endliche Werte und Grenzen [-90,90]/[-180,180] prüfen; 0/0 ist gültig, fehlende Werte sind kein 0/0. Kein stilles Übernehmen einer unzugewiesenen Kartenposition.
- Dezimalgrad zunächst eindeutig als Lat/Lon mit definierter Kultur/Formatierung ausgeben, optional N/S/E/W. West-/Südwerte, Grenzwerte und Rundung bis hin zu negativem Null prüfen. Aktuell verwendet die Interpolation die jeweilige Systemkultur.
- Sichtbarer Stempel und EXIF-GPS sind getrennte Daten: Stempeln allein ändert GPS-Metadaten nicht. Bei GPS-Änderung darf eine noch editierbare Stempelvorschau aktualisiert werden; bereits eingebrannter Text bleibt Pixelinhalt. Das Entfernen von EXIF-GPS entfernt keinen eingebrannten Stempel.
- Einstellungen pro Werkzeug getrennt halten. Ein GPS-Stempel darf nicht versehentlich Textwerkzeug-Einstellungen übernehmen. Erhalt von LearningOptOutIn, ICC und anderen vereinbarten Metadaten bei allen Exportwegen prüfen.

### Verbindliche Abnahmematrix

| Dimension | Fälle |
| --- | --- |
| Bildgrößen | 1×1, 2×3, 16×16, 320×240, 4032×3024, 3024×4032, 6000×4000, 20000×1000 und 1000×20000; zusätzlich ungerade Maße |
| Orientierung/DPI | EXIF 1–8, Bild-DPI 72/96/300, Monitor-Skalierung 100/150/200 %, Monitorwechsel |
| Ansicht | Fit unter 10 %, 25/100/400 %, gescrollt, Fenster nach Auswahl verkleinern/vergrößern |
| Auswahl | Alle vier Ziehrichtungen, jede Ecke, Vollbild, 1-Pixel-Streifen, teilweise außerhalb, Nullhöhe/-breite, Abbruch und Capture-Verlust |
| Reihenfolge | Auswahl → Zoom → Anwenden; Crop → GPS-Stempel; Rotation → Crop → Blur; Undo/Redo → erneute Bearbeitung; Werkzeugwechsel |
| GPS/Text | Fehlend, 0/0, Süd/West, ±90/±180, NaN/Infinity ablehnen, de-DE/en-US, lange Zeilen, mehrere Zeilen, Bildrand, zu kleines Bild |
| Formate | JPEG, PNG mit Alpha, vereinbarte weitere Formate; gesonderte große-Datei-/Speicherlimitprüfung |

Automatisierte Geometrietests mit synthetischen, eindeutig gefärbten Randpixeln: Crop-Abmessungen und Inhalt exakt vergleichen; Bild→Anzeige→Bild für innere Punkte mit numerischer Toleranz prüfen. Geometrie- und Filtertests von Exportkompression trennen. Für große Dateien eine begrenzte repräsentative Kombination statt des gesamten kartesischen Produkts verwenden. Ergänzend echte WPF-Interaktion für Mausaufnahme, Zoom, Scrollen, Monitor-DPI und Vorschau testen.

**Abnahme:** Kein Versatz zwischen Auswahl und bearbeiteten Pixeln; kein stilles Abschneiden von Text/GPS; keine erfolglosen Operationen mit Erfolgsmeldung; keine veralteten Marker nach Geometrieänderungen. Diese Anforderungen werden vor der rein visuellen GUI-Modernisierung umgesetzt und sind Teil der Editor-Reparatur, nicht optionale Nacharbeit.

## 9. Konkretisiertes KI-Modul: Azure, OpenAI und Gemini

Der Nutzer hat den KI-Umfang festgelegt: Bilder anhand einer JSON-Vorlage analysieren, vorhandene EXIF-Daten berücksichtigen und XMP/Nutzungspräferenzen kontrolliert ergänzen. Verarbeitung einzeln und im Batch, eigenes WPF-Fenster sowie Chat zum Festlegen von Metadaten. Kosten- und Tokensparen sind ausdrückliche Anforderungen.

Der vollständige Entwurf steht in [AI-Metadata-Konzept.md](AI-Metadata-Konzept.md). Die [Beispiel-JSON](examples/ai-metadata.template.json) enthält Anbieterprofile, Prompt/Felddefinitionen, Jahreszeiten-Enum, lokale EXIF→XMP-Konvertierungen, LearningOptOutIn-Regeln, Batch-, Budget- und Chat-Einstellungen. Dazu gehören ein [Antwortschema](examples/ai-metadata.response.schema.json) und eine [fiktive Beispielantwort](examples/ai-metadata.response.example.json).

Die OpenAI API mit `gpt-5-mini` (Profil `openai-economy`) ist der gewünschte Standard. Azure `gpt-5-mini` und Gemini bleiben auswählbare Alternativen. Moderne Responses-/Structured-Output-Techniken werden verwendet; Gemini erhält einen nativen Adapter. Rabattierter Provider-Batch und eine lokale Stapel-Queue sind getrennte Modi. Die native Batch-Verfügbarkeit für Azure GPT-5 mini ist noch nicht bestätigt und wird nicht vorausgesetzt.

Normale EXIF-Aufnahmedaten unterstützen die Beschreibung und lokale XMP-Abbildung. Trainingserlaubnisse werden ausschließlich aus bestehenden expliziten Präferenzen oder einer vom Nutzer gewählten JSON-/Chat-Vorgabe übernommen, niemals aus Motiv, Datum oder Kameramodell geraten. Das KI-Antwortschema enthält keine frei generierbaren Rechtefelder. Die genaue normative XMP-Abbildung von LearningOptOutIn bleibt bis zum Normabgleich aus Abschnitt 7 gesperrt.

Status: umgesetzt, siehe Abschnitt 10. Keine Anbieteranmeldung, Bildübertragung oder kostenpflichtige Inferenz durchgeführt; die Anbieteradapter sind nur offline (Schema, Parsing, Validierung) getestet.

## 10. Umsetzungsstand (27.09.2026)

| Paket | Stand | Nachweis |
| --- | --- | --- |
| 1 Warnungen | Erledigt. .NET 10 (`global.json` 10.0.401), `TreatWarningsAsErrors`, generierte AssemblyInfo mit korrekten UTF-8-Metadaten. | Debug- und Release-Rebuild: 0 Warnungen, 0 Fehler. |
| 2 OSM/WebView2 | Erledigt bis auf Netzwerknachweis. Lokales Leaflet über Virtual Host, CSP, Tile-URL ohne Subdomains, eigener User-Agent-Zusatz, Attribution, 403/429-Anzeige, Rasterbegrenzung, validierte Webnachrichten. | Header-/Cache-Nachweis im Netzwerkprotokoll steht aus. |
| 3 Editor-Datenintegrität | Erledigt. Verlustfreie Historie (PNG), Undo/Redo, formatgetreuer Export, kollisionsfreie atomare Exportnamen. GPS-Schreiben bei JPEG ersetzt nur das EXIF-APP1-Segment (Scan-Daten bytegleich), PNG/TIFF verlustfrei, BMP abgelehnt; Ergebnis wird vor dem Ersetzen nachgelesen. | Tests: Scan-Daten identisch, Idempotenz, ungültige Koordinaten ändern nichts, Original-Hash unverändert. |
| 4 Editor/GUI | Weitgehend erledigt. Zentrale Pixelgeometrie, Blur/Verpixelung nur in der Auswahl, Stempel mit Messung und Randabstand an allen Ankern. | Geometrie-/Filtertests; interaktiver DPI-/Monitortest steht aus. |
| 5 Laufzeit/CI | Erledigt. Release baut den Tag-Commit, PR-Workflow mit Build, Tests und Paketaudit. Single-File-Publish liefert `Resources/` und `Templates/` neben der EXE. | Publish-Smoke-Test win-x64; ARM64-Laufzeittest steht aus. |
| 6 KI-Metadaten | Umgesetzt: `AiMetadataWindow`, Adapter OpenAI/Azure (Responses, Structured Outputs, Entra ID oder Schlüssel) und Gemini (`generateContent`, JSON-Schema), lokale Queue mit begrenzter Parallelität, Budgetreservierung vor Versand, Ergebnis-Cache, Chat mit typisierten Aktionen, XMP-Sidecar mit Nachprüfung, Audit und Rückgängig. | 46 Unit-Tests inkl. Fenster-Instanziierung. |

Bewusst noch nicht umgesetzt:

- Nativer Provider-Batch (50 % Rabatt, bis 24 h): Die lokale Queue ist der erste Pfad. `AiPrices.Cost` rechnet keinen pauschalen Batch-Rabatt.
- Decodieren und Schreiben von `LearningOptOutIn`: Ein vorhandener Tag gilt als „Entscheidung nötig“, nie als Zustimmung. Schreiben bleibt bis zum CIPA-Normabgleich gesperrt. IPTC-PLUS-Data-Mining-Einschränkungen führen zum Überspringen.
- Eingebettetes XMP-Schreiben: nur Sidecar (`bild.jpg.xmp`).
- Preise sind nicht hinterlegt. Sie werden pro Profil mit Prüfdatum eingegeben; ohne sie startet kein kostenpflichtiger Lauf.
- SixLabors-Hauptversionen 4.x/3.x wegen des Lizenzmodells nicht aktualisiert; keine bekannten Schwachstellen in den aktuellen Paketen.
- Bekannte Grenze: Beim EXIF-Neuserialisieren durch ImageSharp können Offsets in herstellerspezifischen MakerNotes ungültig werden. Das galt schon vor der Änderung und betrifft keine Bilddaten.
