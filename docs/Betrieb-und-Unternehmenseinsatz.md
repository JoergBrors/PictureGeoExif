# Betrieb und Unternehmenseinsatz

Leitfaden für IT, Datenschutz und Verantwortliche, die PictureGeoExif in einer Organisation einsetzen wollen. Die lizenzrechtliche Einordnung und eine Freigabe-Checkliste stehen in [THIRD-PARTY-LICENSES.md, Abschnitt 4](../THIRD-PARTY-LICENSES.md). Dieses Dokument ist keine Rechtsberatung.

## 1. Einsatzbild

| Eigenschaft | Wert |
| --- | --- |
| Art | Einzelplatz-Desktopanwendung, keine Serverkomponente, keine Telemetrie |
| Plattform | Windows 10 1809+ / Windows 11, x64 oder ARM64 |
| Rechte | Keine Administratorrechte nötig (portables ZIP) |
| Netzwerk | Karte: HTTPS zum Kachelanbieter. KI (optional): HTTPS zu OpenAI, Azure oder Google, nur nach ausdrücklichem Start |
| Datenhaltung | Nur lokal bzw. im gewählten Ausgabeordner; Originale bleiben unverändert |
| Lizenz | MIT; Fremdkomponenten siehe THIRD-PARTY-LICENSES.md |
| Gewährleistung/Support | Keine (Open Source, „AS IS“) |

## 2. Installation und Verteilung

1. Das Release-ZIP (`PictureExifclone-<tag>-win-x64.zip` bzw. `-win-arm64.zip`) von GitHub Releases laden und die Prüfsumme bzw. Quelle nach Unternehmensrichtlinie prüfen.
2. In einen Programmordner entpacken, z. B. `C:\Program Files\PictureGeoExif\` (per Softwareverteilung) oder benutzerbezogen.
3. **Nicht trennen:** `PictureExifclone.exe`, `Resources\`, `Templates\`, `licenses\`, `LICENSE`, `THIRD-PARTY-LICENSES.md`.
4. **WebView2 Runtime** sicherstellen. Unter Windows 11 ist sie vorhanden; sonst den Evergreen-Installer von Microsoft verteilen.
5. Die Anwendung ist self-contained und enthält die .NET-Runtime. Updates erfolgen durch Austausch des Ordners; Benutzereinstellungen bleiben unter `%APPDATA%` erhalten.

Die EXE ist nicht code-signiert. Bei AppLocker oder WDAC den Hash oder Pfad freigeben oder intern signieren.

## 3. Konfiguration

`%APPDATA%\PictureExifclone\settings.json` (wird beim ersten Beenden angelegt):

```json
{
  "TileUrl": "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
  "TileAttribution": "OpenStreetMap contributors",
  "TileAttributionUrl": "https://www.openstreetmap.org/copyright",
  "OutputFolder": "D:\\Bilder\\Export",
  "AiPrices": { "openai-economy": { "Input": 0, "Cached": 0, "Output": 0, "VerifiedDate": "2026-09-27T00:00:00" } },
  "AiTemplatePath": null,
  "RouteMaxGapMeters": 200,
  "SortImagesByRoute": true,
  "RoadMatchUrl": "https://valhalla1.openstreetmap.de",
  "RoadMatchProfile": "pedestrian",
  "RoadMatchMaxDeviationMeters": 25
}
```

- **Kachelanbieter:** `TileUrl` muss HTTPS sein. Die Attribution des Anbieters ist Pflicht und wird in der Karte angezeigt. Für den Unternehmenseinsatz mit vielen Nutzern einen kommerziellen Anbieter oder eigenen Tile-Server verwenden (OSM Tile Usage Policy).
- **Trassen und Routing-Server** (auch in der App über ⚙ einstellbar):
  - `RouteMaxGapMeters`: Abstand, ab dem eine neue Trasse beginnt.
  - `RoadMatchUrl`: Valhalla-kompatibler Server (HTTPS, HTTP nur für `localhost`). Für Firmen empfohlen: eigener Valhalla-Server.
  - `RoadMatchProfile`: `pedestrian`, `bicycle` oder `auto`.
  - `RoadMatchMaxDeviationMeters`: Grenze, ab der ein Foto nicht mehr an einen Weg gezogen wird.
- **Ausgabeordner:** Er kann auf einem Netzlaufwerk liegen. Geschrieben wird atomar, mit Temp-Datei im Zielordner.
- **KI-Vorlage:** Unternehmensweite Vorgaben (Felder, Budget, Anbieter) als JSON verteilen und im KI-Fenster laden. Die Vorlage wird beim Laden validiert; unsichere Einstellungen werden abgelehnt, etwa Rechte aus Bildern ableiten, Original senden, exakte GPS-Daten senden oder automatischer Anbieterwechsel.
- **Umgebungsvariablen** (optional, z. B. per GPO):

| Variable | Zweck |
| --- | --- |
| `OPENAI_API_KEY` | OpenAI-Schlüssel, falls nicht im Credential Manager |
| `GEMINI_API_KEY` | Gemini-Schlüssel |
| `AZURE_OPENAI_API_KEY` | Azure-Schlüssel, nur bei Profilen ohne Entra ID |
| `PICTUREGEO_AZURE_OPENAI_ENDPOINT` | Azure-OpenAI-Endpoint (`https://<ressource>.openai.azure.com`) |
| `PICTUREGEO_AZURE_GPT5_MINI_DEPLOYMENT` | Name des Azure-Deployments |

Für Azure wird **Entra ID** empfohlen (`DefaultAzureCredential`: Windows-/Visual-Studio-/Azure-CLI-Anmeldung oder Browser). Das Benutzerkonto braucht die Rolle „Cognitive Services OpenAI User“ auf der Ressource.

## 4. Datenablage und Datenschutz

| Ort | Inhalt | Personenbezug | Löschen / Aufbewahrung |
| --- | --- | --- | --- |
| Ausgabeordner | Bildkopien inkl. GPS | ja (Bild, Standort) | nach Unternehmensrichtlinie |
| `<bild>.xmp` neben dem Original | Titel, Stichwörter, Jahreszeit, übernommene EXIF-Werte | möglich | mit dem Bild verwalten |
| `%LOCALAPPDATA%\PictureGeoExif\ai-cache\` | KI-Antworten (Text, keine Bilder) | gering | Einträge werden nach 30 Tagen nicht mehr verwendet, aber nicht automatisch gelöscht; Ordner jederzeit löschbar |
| `%LOCALAPPDATA%\PictureGeoExif\ai-audit\audit.jsonl` | Wer/was/wann bei Metadatenänderungen: Pfad, Feld, alt/neu, Quelle | ja (Dateipfade) | nach Richtlinie rotieren |
| `%LOCALAPPDATA%\PictureGeoExif\WebView2\` | Kartencache, Browserprofil | gering | löschbar |
| `%TEMP%\PictureGeoExif\…` | Editor-Zwischenstände | ja | automatisch beim Schließen |
| Windows-Anmeldeinformationen | API-Schlüssel | – | über „Löschen“ im KI-Fenster oder Credential Manager |

**KI-Nutzung datenschutzkonform einrichten:**

1. **Anbieter wählen und Vertrag schließen:** Auftragsverarbeitungsvertrag (DPA), Region bzw. Datenresidenz, Aufbewahrung, keine Nutzung für Training. Bei Gemini die bezahlte Stufe verwenden.
2. **Verzeichnis von Verarbeitungstätigkeiten** ergänzen. Bei systematischer Verarbeitung von Personenbildern eine Datenschutz-Folgenabschätzung prüfen.
3. **Nutzer informieren**, was übertragen wird (siehe [Benutzerhandbuch](Benutzerhandbuch.md#was-wird-gesendet)).
4. **Budgetgrenzen im Anbieterkonto** setzen. Die App-Grenze ist keine Abrechnungssperre.
5. **Schlüssel pro Person oder Team** vergeben und rotieren. Keine Schlüssel in geteilten Vorlagen.

Soll die KI im Unternehmen nicht genutzt werden: keinen Schlüssel ausgeben und die Anbieter-Endpunkte per Proxy oder Firewall sperren. Die übrigen Funktionen bleiben vollständig nutzbar.

## 5. Netzwerk-Freigaben

| Ziel | Wofür | Pflicht |
| --- | --- | --- |
| `tile.openstreetmap.org` oder eigener Kachelanbieter | Kartenkacheln | für die Karte |
| `www.openstreetmap.org` | Attribution/Copyright-Link (öffnet Systembrowser) | optional |
| `api.openai.com` | OpenAI | nur KI |
| `<ressource>.openai.azure.com`, `login.microsoftonline.com` | Azure OpenAI, Entra ID | nur KI (Azure) |
| `generativelanguage.googleapis.com` | Gemini | nur KI (Gemini) |
| `valhalla1.openstreetmap.de` oder eigener Valhalla-Server | „Trassen an Wege anlegen“ (nur Koordinaten, nur nach Klick) | optional |

Die Karte lädt Leaflet lokal, es gibt keine CDN-Aufrufe.

## 6. Lizenzfreigabe im Unternehmen: Kurzfassung

- **Nutzung des unveränderten Tools:** unter MIT erlaubt. Die SixLabors-Bildbibliotheken fallen dabei unter Apache-2.0.
- **Eigene, nicht offen lizenzierte Weiterentwicklung** durch ein Unternehmen mit mindestens 1 Mio. USD Jahresumsatz: **kommerzielle Six-Labors-Lizenz erforderlich**.
- **OSM:** Attribution sichtbar lassen; bei intensiver Nutzung einen eigenen oder kommerziellen Kachelanbieter einsetzen.
- **KI-Anbieter:** Es gelten die eigenen Verträge des Unternehmens.

Details und Checkliste: [THIRD-PARTY-LICENSES.md](../THIRD-PARTY-LICENSES.md), Abschnitt 4.
