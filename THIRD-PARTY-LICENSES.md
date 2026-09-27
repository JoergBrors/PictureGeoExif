# Drittanbieter-Lizenzen und Einsatz im Unternehmen

Stand: 27.09.2026 · PictureGeoExif (PictureExifclone) 0.97 · Zielplattform .NET 10 / Windows 10 1809+

Diese Datei listet alle Komponenten Dritter, die mit der Anwendung ausgeliefert oder zur Laufzeit genutzt werden, einschließlich transitiver Abhängigkeiten. Die vollständigen Lizenztexte liegen im Ordner `licenses/`. Die maschinenlesbare Liste steht in `licenses/dependency-licenses-summary.json`. Beide werden mit `scripts/Update-ThirdPartyLicenses.ps1` direkt aus den NuGet-Paketen erzeugt, nicht aus Webseiten.

> **Hinweis:** Diese Übersicht ist eine technische Einordnung und keine Rechtsberatung. Für den produktiven Einsatz in einem Unternehmen sollten die Punkte in Abschnitt 4 von der Rechts- bzw. Lizenzabteilung bestätigt werden.

---

## 1. Projektlizenz

| Komponente | Lizenz | Datei |
| --- | --- | --- |
| PictureGeoExif / PictureExifclone | MIT, Copyright (c) 2026 Jörg Brors | `LICENSE` |

Die MIT-Lizenz erlaubt kommerzielle und interne Nutzung, Änderung und Weitergabe. Bedingung: Der Copyright- und Lizenzhinweis bleibt erhalten. Die Software wird ohne Gewährleistung bereitgestellt.

---

## 2. Ausgelieferte Komponenten (Laufzeit)

„Ausgeliefert“ heißt: Die Komponente ist im Build bzw. im Release-ZIP enthalten.

### 2.1 Direkt verwendete Bibliotheken

| Komponente | Version | Lizenz (SPDX) | Zweck in der App | Lizenzdateien |
| --- | --- | --- | --- | --- |
| SixLabors.ImageSharp | 3.1.12 | **Six Labors Split License 1.0** (Apache-2.0 **oder** kommerziell, siehe 3.1) | Laden, Bearbeiten und Speichern von Bildern | `licenses/SixLabors.ImageSharp/LICENSE` |
| SixLabors.ImageSharp.Drawing | 2.1.7 | **Six Labors Split License 1.0** | Text/GPS-Stempel zeichnen | `licenses/SixLabors.ImageSharp.Drawing/LICENSE` |
| SixLabors.Fonts | 2.1.3 | **Six Labors Split License 1.0** | Schriftmessung und -darstellung | `licenses/SixLabors.Fonts/LICENSE` |
| MetadataExtractor | 2.9.3 | Apache-2.0 | EXIF/GPS/XMP lesen | `licenses/MetadataExtractor/` |
| XmpCore | 6.1.10.1 | BSD-3-Clause (Adobe) | XMP-Sidecar lesen/schreiben | `licenses/XmpCore/LICENSE.txt` |
| Microsoft.Web.WebView2 (SDK) | 1.0.4191.47 | BSD-3-Clause-artig (Microsoft) + NOTICE | Kartenansicht | `licenses/Microsoft.Web.WebView2/` |
| Ookii.Dialogs.Wpf | 5.0.1 | BSD-3-Clause | Ordnerauswahl-Dialog | `licenses/Ookii.Dialogs.Wpf/` |
| Azure.Identity | 1.21.0 | MIT | Entra-ID-Anmeldung für Azure OpenAI (optional) | `licenses/Azure.Identity/` |
| Leaflet | 1.9.4 | BSD-2-Clause | Kartenbibliothek (JavaScript), lokal in `Resources/leaflet/` | `licenses/Leaflet/LICENSE`, `Resources/leaflet/LICENSE` |

### 2.2 Transitive Abhängigkeiten (über Azure.Identity)

Alle folgenden Pakete stehen unter MIT, Copyright Microsoft Corporation:

| Komponente | Version |
| --- | --- |
| Azure.Core | 1.53.0 |
| System.ClientModel | 1.10.0 |
| System.Memory.Data | 10.0.3 |
| Microsoft.Identity.Client | 4.83.1 |
| Microsoft.Identity.Client.Extensions.Msal | 4.83.1 |
| Microsoft.IdentityModel.Abstractions | 8.14.0 |
| Microsoft.Bcl.AsyncInterfaces | 10.0.3 |
| Microsoft.Extensions.Configuration.Abstractions, .DependencyInjection.Abstractions, .Diagnostics.Abstractions, .FileProviders.Abstractions, .Hosting.Abstractions, .Logging.Abstractions, .Options, .Primitives | jeweils 10.0.3 |

Lizenztexte und `THIRD-PARTY-NOTICES.TXT` stehen jeweils unter `licenses/<Paketname>/`.

### 2.3 .NET-Laufzeit (nur im Self-Contained-Release enthalten)

Das Release-ZIP enthält die .NET-Runtime und WPF, damit auf dem Zielrechner kein .NET installiert sein muss.

| Komponente | Version | Lizenz | Dateien |
| --- | --- | --- | --- |
| Microsoft.NETCore.App (Runtime win-x64) | 10.0.12 | MIT + Third-Party-Notices | `licenses/Microsoft.NETCore.App.Runtime.win-x64/` |
| Microsoft.WindowsDesktop.App (WPF, win-x64) | 10.0.12 | MIT | `licenses/Microsoft.WindowsDesktop.App.Runtime.win-x64/` |

Beim ARM64-Release gelten dieselben Lizenzen für die `win-arm64`-Pakete. Die genaue Patchversion hängt vom SDK zum Build-Zeitpunkt ab; das Skript erfasst die tatsächlich verwendete.

---

## 3. Besondere Bedingungen

### 3.1 SixLabors (ImageSharp, ImageSharp.Drawing, Fonts): Split License

Wortlaut in `licenses/SixLabors.ImageSharp/LICENSE`. Die drei Pakete stehen **unter Apache-2.0**, wenn einer dieser Fälle zutrifft:

1. Nutzung in Software unter einer Open-Source- oder Source-Available-Lizenz. PictureGeoExif steht unter MIT, dieser Fall trifft also zu.
2. Nutzung als transitive Abhängigkeit eines Drittpakets.
3. Direkte Nutzung durch ein gewinnorientiertes Unternehmen bzw. eine Person mit **weniger als 1 Mio. USD Jahresbruttoumsatz**.
4. Direkte Nutzung durch eine gemeinnützige Organisation.

**In allen anderen Fällen** ist eine kommerzielle Six-Labors-Lizenz erforderlich (<https://sixlabors.com/pricing/>). Siehe Abschnitt 4.2 für typische Unternehmensszenarien.

Die neueren Hauptversionen (ImageSharp 4.x, Fonts 3.x, Drawing 3.x) wurden bewusst **nicht** übernommen. Vor einem Update ist deren Lizenz erneut zu prüfen.

### 3.2 WebView2: SDK und Runtime

- Das **SDK** (NuGet-Paket, ausgeliefert) steht unter der Microsoft-Lizenz in `licenses/Microsoft.Web.WebView2/LICENSE.txt`. Die enthaltenen Fremdkomponenten sind in `NOTICE.txt` aufgeführt.
- Die **WebView2 Runtime (Evergreen)** wird **nicht** mitgeliefert. Sie ist unter Windows 10/11 meist vorinstalliert oder wird separat von Microsoft installiert. Für sie gelten die Microsoft-Bedingungen der Runtime.

### 3.3 OpenStreetMap: Kartendaten und Kacheldienst

- **Kartendaten** © OpenStreetMap contributors, lizenziert unter der **Open Database License (ODbL 1.0)**. Einzelne Inhalte stehen unter der **Database Contents License (DbCL 1.0)**. Texte und Attributionsrichtlinie: `licenses/OpenStreetMap/`.
- Die App zeigt die Attribution sichtbar und anklickbar in der Karte an. Diese Anzeige darf nicht entfernt oder verdeckt werden.
- Die Kacheln kommen standardmäßig vom Community-Server `tile.openstreetmap.org`. Dafür gilt die **OSM Tile Usage Policy** (<https://operations.osmfoundation.org/policies/tiles/>): keine starke oder automatisierte Nutzung, keine Massen-Downloads, klare Identifikation der App und **keine Verfügbarkeitszusage**. Die OSMF kann den Zugriff jederzeit sperren. Zum Unternehmenseinsatz siehe Abschnitt 4.3.

### 3.4 Routing-Dienst für „Trassen an Wege anlegen“ (optional)

- Die Funktion sendet die **Koordinaten der Trassenpunkte** an einen Valhalla-kompatiblen Routing-Server (`POST /trace_attributes`). Voreingestellt ist der **öffentliche Demo-Server des FOSSGIS e.V.** (`valhalla1.openstreetmap.de`); über ⚙ ist er austauschbar.
- Es gelten die **Nutzungsbedingungen des FOSSGIS e.V.** (<https://www.fossgis.de/arbeitsgruppen/osm-server/nutzungsbedingungen/>):
  - gültiger User-Agent, der die Anwendung identifiziert, und höchstens **1 Anfrage pro Sekunde**. Beides setzt die App technisch um: eine Verbindung, Drosselung ≥ 1,1 s, keine automatischen Wiederholungen.
  - **Gewerbliche Nutzung nur, wenn der Dienst keinen wesentlichen Teil eines Onlineangebots darstellt**; massenhafte Abrufe sind verboten.
  - keine Verfügbarkeitszusage.
- Die gelieferte Geometrie stammt aus OpenStreetMap-Daten (ODbL, © OpenStreetMap contributors); die Attribution der Karte deckt das ab.
- Valhalla selbst (MIT-Lizenz) wird **nicht** mitgeliefert. Zum eigenen Server siehe Abschnitt 4.3.

### 3.5 KI-Anbieter (optional, nur nach ausdrücklichem Start)

Die KI-Funktion nutzt Cloud-Dienste. Sie liefert **keine** Software dieser Anbieter aus; es gelten die jeweiligen Vertrags- und Nutzungsbedingungen des Kontos, dessen Schlüssel eingetragen wird:

| Anbieter | Dienst | Bedingungen (vom Betreiber zu prüfen) |
| --- | --- | --- |
| OpenAI | OpenAI API (Responses API) | OpenAI Business-/API-Bedingungen, Data Processing Addendum |
| Microsoft | Azure OpenAI in Azure AI Foundry | Microsoft Product Terms, Data Protection Addendum (DPA) |
| Google | Gemini API | Gemini API Terms und Zusatzbedingungen; kostenlose und bezahlte Stufen unterscheiden sich bei der Datennutzung |

Die Bildanalyse ist **standardmäßig nicht aktiv**. Ohne Schlüssel und eingetragene Preise sendet die App keine Daten. Einzelheiten stehen in Abschnitt 4.4.

### 3.6 Schriften, Symbole und Normen

- **Schriften:** Es werden keine Schriftdateien mitgeliefert. Stempel verwenden die Systemschrift „Segoe UI“ (ersatzweise „Arial“) des jeweiligen Windows-Systems. Symbole in der Oberfläche sind Unicode-Emoji der Systemschrift.
- **Normen:** CIPA DC-008/DC-010 (EXIF 3.1, XMP) und die IPTC Photo Metadata sind nur referenziert, nicht enthalten.

---

## 4. Einsatz im Unternehmen

### 4.1 Kurzfassung

| Frage | Antwort |
| --- | --- |
| Darf ein Unternehmen das Tool intern nutzen? | Ja. MIT erlaubt kommerzielle Nutzung. SixLabors fällt unter Apache-2.0, solange das unveränderte Open-Source-Tool genutzt wird (siehe 4.2). |
| Kostet die Nutzung Lizenzgebühren? | Für das unveränderte Tool nein. Möglich sind Kosten für KI-Anbieter, einen kommerziellen Kartendienst und eine kommerzielle SixLabors-Lizenz im Fall B aus 4.2. |
| Was muss bei der Weitergabe beiliegen? | `LICENSE`, `THIRD-PARTY-LICENSES.md` und der Ordner `licenses/`. Das Release-ZIP enthält sie bereits. |
| Gibt es Gewährleistung oder Support? | Nein (MIT, „AS IS“). |
| Werden Originalbilder verändert? | Nein. Exporte und GPS-Änderungen landen als Kopie im Ausgabeordner; KI-Metadaten als XMP-Sidecar neben dem Bild. |

### 4.2 SixLabors: Entscheidungshilfe

| Szenario | Einordnung |
| --- | --- |
| **A:** Unternehmen nutzt das veröffentlichte, unveränderte Tool bzw. einen öffentlichen Open-Source-Fork. | Software unter Open-Source-Lizenz: Apache-2.0, keine kommerzielle SixLabors-Lizenz nötig. |
| **B:** Unternehmen mit **≥ 1 Mio. USD Umsatz** ändert den Code und nutzt oder vertreibt ihn **proprietär** (nicht unter Open-Source-Lizenz), z. B. als Teil eines internen oder kommerziellen Closed-Source-Produkts. | Direkte Abhängigkeit in proprietärer Software: **kommerzielle Six-Labors-Lizenz erforderlich**. |
| **C:** Wie B, aber < 1 Mio. USD Umsatz oder gemeinnützig. | Apache-2.0. |

Im Zweifel gilt der Lizenzwortlaut. Wer eine Lizenzpflicht vermeiden will, veröffentlicht Änderungen unter einer Open-Source-Lizenz (Szenario A) oder erwirbt eine kommerzielle Lizenz.

### 4.3 Karte im Unternehmen

- Gelegentliche interaktive Nutzung durch einzelne Mitarbeitende ist mit der OSM Tile Usage Policy vereinbar. Die App cached normal, lädt nicht massenhaft und identifiziert sich mit eigenem User-Agent.
- Für **viele Arbeitsplätze, dauerhaften Betrieb oder Verfügbarkeitsanforderungen** sollte ein kommerzieller Kachelanbieter oder ein eigener Tile-Server genutzt werden. Konfiguration in `%APPDATA%\PictureExifclone\settings.json`:
  - `TileUrl` (HTTPS, Platzhalter `{z}/{x}/{y}`)
  - `TileAttribution` und `TileAttributionUrl`: die vom Anbieter verlangte Attribution
- Bei einem Proxy mit TLS-Inspektion muss die WebView2 Runtime dem Unternehmenszertifikat vertrauen.
- **Trassen an Wege anlegen:** Der FOSSGIS-Demo-Server eignet sich zum Ausprobieren und für gelegentliche Einzelnutzung. Für den regelmäßigen Firmeneinsatz einen **eigenen Valhalla-Server** betreiben (z. B. Docker-Image mit einem Deutschland- oder Bundesland-Extrakt aus OpenStreetMap) und über ⚙ eintragen. HTTP ist nur für `localhost` zulässig, sonst HTTPS. Dann verlassen keine Baustellenkoordinaten das Unternehmen.

### 4.4 KI-Funktion und Datenschutz (DSGVO)

- **Personenbezug:** Bilder können Personen zeigen; GPS-Koordinaten, Aufnahmezeit und Kameradaten können personenbeziehbar sein. Wer die KI-Analyse nutzt, übermittelt Daten an einen Auftragsverarbeiter.
- **Vor der Freigabe klären:**
  1. Auftragsverarbeitungsvertrag (AVV/DPA) mit dem Anbieter.
  2. Region und Datenresidenz, etwa Azure-Region oder EU-Datengrenze.
  3. Aufbewahrung beim Anbieter und Nutzung für Training. Bei Gemini keine kostenlose Stufe für Unternehmensdaten verwenden.
  4. Eintrag im Verzeichnis von Verarbeitungstätigkeiten; ggf. eine Datenschutz-Folgenabschätzung.
  5. Information bzw. Rechtsgrundlage für abgebildete Personen.
- **Was die App technisch begrenzt:**
  - Gesendet werden nur eine verkleinerte Vorschau (max. 1024 px) **ohne eingebettete Metadaten**, das Aufnahmedatum **ohne Uhrzeit**, die **Hemisphäre statt GPS** sowie vorhandene Titel und Stichwörter.
  - Keine Dateipfade, keine Seriennummern.
  - OpenAI- und Azure-Anfragen werden mit `store=false` gesendet.
  - Jeder Lauf muss ausdrücklich gestartet werden und zeigt vorher Anbieter, Umfang und Kostenobergrenze.
- **Nutzungspräferenzen:** Bilder mit IPTC-Data-Mining-Einschränkung werden übersprungen. Bei vorhandenem EXIF-`LearningOptOutIn` fragt die App nach. Die Präferenzen beschreiben den Willen des Rechteinhabers; sie verhindern technisch keine Verarbeitung. Verantwortlich bleibt der Betreiber.
- **Schlüssel:** Sie liegen in der Windows-Anmeldeinformationsverwaltung des Benutzers (`PictureGeoExif/OpenAI`, `PictureGeoExif/Gemini`) oder werden per Umgebungsvariable gesetzt. Azure nutzt bevorzugt Entra ID. Schlüssel gehören nie in Vorlagen oder Repositories.
- **Kosten:** Preise werden pro Anbieterprofil mit Prüfdatum eingetragen; ohne sie startet kein kostenpflichtiger Lauf. Das Budget in der Vorlage ist eine App-Grenze, **keine Abrechnungssperre beim Anbieter**. Für harte Limits zusätzlich Budgetgrenzen im Anbieterkonto setzen.

### 4.5 Checkliste für die Freigabe

- [ ] MIT-Hinweis und `licenses/` werden mit verteilt (im Release-ZIP enthalten).
- [ ] SixLabors-Szenario nach 4.2 bestimmt; bei Szenario B kommerzielle Lizenz vorhanden.
- [ ] Kachelanbieter festgelegt: OSM nur bei geringer Nutzung, sonst kommerziell oder eigener Server; Attribution konfiguriert.
- [ ] WebView2 Runtime auf den Zielsystemen vorhanden.
- [ ] Routing-Server für „Trassen an Wege anlegen“ festgelegt: eigener Valhalla-Server oder bewusst nur gelegentliche Nutzung des FOSSGIS-Demo-Servers.
- [ ] KI-Funktion: Anbieter, AVV, Region, Budgetgrenzen im Anbieterkonto und Zuständigkeit für Schlüssel geklärt, oder KI bewusst nicht genutzt.
- [ ] Verarbeitungsverzeichnis und Information der Betroffenen, falls personenbezogene Bilder verarbeitet werden.
- [ ] Ausgabeordner und Sidecar-Ablage in das Backup-Konzept aufgenommen.

Betriebsdetails, Installation und Speicherorte stehen in `docs/Betrieb-und-Unternehmenseinsatz.md`.

---

## 5. Nur für Entwicklung und Tests (nicht ausgeliefert)

| Komponente | Version | Lizenz |
| --- | --- | --- |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| coverlet.collector | 6.0.4 | MIT |
| .NET SDK 10.0.401 | – | MIT |
| GitHub Actions (`actions/checkout`, `actions/setup-dotnet`, `actions/cache`, `ncipollo/release-action`) | v4 bzw. v1 | MIT |

---

## 6. Pflege

1. Nach jeder Paketänderung `pwsh scripts/Update-ThirdPartyLicenses.ps1` ausführen. Für die Runtime-Lizenzen vorher einmal self-contained publishen.
2. Neue Pakete oder geänderte Lizenzen in dieser Datei ergänzen, insbesondere Split-, Dual- oder kommerzielle Lizenzen.
3. Pakete ohne Lizenzangabe im NuGet-Paket brauchen eine geprüfte Datei unter `licenses/_manual/<Paket>.txt`, sonst bricht das Skript ab.
4. Manuell gepflegt werden `licenses/Leaflet/` (bei einem Leaflet-Update aus `Resources/leaflet/LICENSE` übernehmen) und `licenses/OpenStreetMap/`.
