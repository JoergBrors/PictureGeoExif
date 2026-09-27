# Benutzerhandbuch

## Grundprinzip

PictureGeoExif ergänzt Fotos um GPS-Koordinaten, bearbeitet Bilder und schlägt Beschreibungen per KI vor. **Die Originaldateien werden nie verändert:**

- Gespeicherte Bilder landen als neue Datei im **Ausgabeordner** (Standard: `Dokumente\PictureExifclone_Output\<Datum>\`).
- KI- und Metadatenänderungen werden als **XMP-Sidecar** neben dem Bild gespeichert (`foto.jpg.xmp`). Programme wie Lightroom, darktable oder digiKam lesen diese Dateien.

## Hauptfenster

| Bereich | Bedienung |
| --- | --- |
| **Bilder laden** | Schaltfläche „📁 Bilder laden“ oder Dateien ins Fenster ziehen. Unterstützt: JPG, PNG, BMP, TIF. |
| **Bildkacheln** | Klick wählt das Bild aus. Hat es GPS, zeigt die Karte seine Position. Unter dem Bild stehen Koordinaten und Trassenposition („Trasse 2 · Nr. 5“). Buttons: ✏ = Editor, 💾 = speichern, ↶ = letzte Änderung an diesem Bild rückgängig, ✕ = aus der Liste entfernen (die Datei bleibt erhalten). |
| **Karte** | **Doppelklick** setzt neue Koordinaten (oranger Marker); ein einfacher Klick verändert nichts. Grüne Kreise sind Bilder mit GPS; ein Klick darauf wählt das Bild, beim Überfahren erscheint der Name im grauen Bereich über der Karte (keine Popups, die andere Punkte verdecken). Optional lässt sich ein metrisches Raster ab Zoomstufe 13 einblenden. |
| **Trassen** | Aus allen GPS-Punkten entstehen virtuelle Trassen: Punkte, die höchstens den eingestellten Abstand (Standard 200 m) voneinander entfernt liegen, gehören zu einer Trasse. Jede Trasse hat einen **Hauptstrang**, der dem Verlauf der Punkte folgt und Süd → Nord bzw. West → Ost läuft (Startpunkt gefüllt, Endpunkt hohl). Mit „Bildliste nach Trasse ordnen“ folgt die Kachelreihenfolge dem Trassenverlauf. Überfahren einer Trasse zeigt Bildanzahl, Abzweige, Länge und Richtung. |
| **Abzweige / Hausanschlüsse** | Fotos, die weiter als „Abzweig ab“ (Standard 10 m) neben dem Hauptstrang liegen, werden als **Abzweig** senkrecht an den Hauptstrang angebunden (gestrichelte Linie, kleiner Punkt an der Abzweigstelle) statt den Hauptstrang umzuleiten. Näher liegende Fotos gelten als GPS-Ungenauigkeit und bleiben im Hauptstrang. In der Bildliste steht ein Abzweig-Foto direkt hinter der Stelle, an der der Abzweig ansetzt („Trasse 4 · Nr. 13 · Abzweig“). Eine echte Ecke der Trasse wird nicht zum Abzweig, weil der Hauptstrang danach die Richtung wechselt. |
| **Trassen an Wege anlegen** | „🛣 Trassen an Wege anlegen“ legt die Trassen über einen Routing-Server an Straßen, Geh- und Feldwege an (Ebene „Wegverlauf“, breite Linie in Trassenfarbe). Fotos, die weiter als der eingestellte Abstand (Standard 25 m) vom nächsten Weg liegen, werden gerade und grau gestrichelt verbunden. Hauptstrang und Abzweige werden getrennt angelegt: Ein Abzweig folgt einem Weg, wenn einer in Reichweite liegt, sonst bleibt er die direkte Verbindung zum Hauptstrang; das letzte Stück bis zum Foto (z. B. ins Grundstück) wird immer gerade ergänzt. Die Infozeile zeigt je Foto den Abstand zum Weg. **Die GPS-Daten der Fotos werden nicht verändert.** Beim ersten Senden pro Sitzung fragt die App nach; bereits angelegte Trassen werden nicht erneut angefragt. |
| **⚙ Einstellungen** | Routing-Server (Standard: öffentlicher FOSSGIS-Demo-Server, nur gelegentliche Nutzung), Wegeprofil (Fußwege empfohlen, Rad, Auto) und maximaler Abstand Foto → Weg. |
| **Layer** | „Trassen (Luftlinie)“, „Wegverlauf anzeigen“ und „Bilder anzeigen“ blenden die jeweilige Ebene ein oder aus. |
| **📌 Referenzbild verwenden** | Übernimmt die GPS-Daten eines anderen Fotos als aktuelle Koordinaten. |
| **GPS auf ausgewähltes Bild anwenden** | Speichert eine Kopie des ausgewählten Bildes mit den aktuellen Koordinaten. Vorhandene Koordinaten werden erst nach Rückfrage ersetzt. |
| **GPS auf alle Bilder anwenden und speichern** | Wie oben, für alle geladenen Bilder. |
| **💾 Alle speichern** | Speichert Kopien aller Bilder mit ihren jeweils eigenen Koordinaten. |
| **Speicherort ändern** | Wählt den Ausgabeordner. |
| **KI-Metadaten · Batch & Chat** | Öffnet das KI-Fenster (siehe unten). |
| **📜 Lizenzen anzeigen** | Projekt- und Drittanbieterlizenzen. |

**↶ Rückgängig an der Kachel** setzt das Bild auf den Stand vor der letzten Speicherung bzw. GPS-Zuweisung zurück (Pfad und Koordinaten). Die dabei erzeugte Kopie im Ausgabeordner wird nicht gelöscht.

GPS lässt sich in JPEG, PNG und TIFF schreiben. **BMP kann keine GPS-Daten speichern** und wird mit einer Meldung abgelehnt. Bei JPEG bleibt die Bildqualität vollständig erhalten, denn es wird nur der Metadatenblock ersetzt.

## Bildeditor

Öffnen über ✏️ an einer Bildkachel.

1. **Werkzeug wählen:** Zuschneiden, Unschärfe (Blur), Verpixeln, Text oder GPS-Stempel.
2. **Bereich aufziehen** (Crop, Blur, Verpixeln) bzw. **ins Bild klicken** oder einen Eckanker wählen (Text, Stempel). Die Auswahl ist pixelgenau; auch einzelne Pixel und Bildränder sind möglich.
3. **Vorschau** zeigt das Ergebnis, **Anwenden** übernimmt es, **Auswahl/Vorschau löschen** verwirft.
4. Die Effektstärke richtet sich nach der Größe der Auswahl; die Stempelgröße nach der kurzen Bildkante. **Der Zoom beeinflusst das Ergebnis nicht.**
5. **Rückgängig/Wiederholen** (Strg+Z / Strg+Y), **Original/Bearbeitet** zum Vergleichen, **EXIF** zeigt die Originalmetadaten.
6. **Speichern** exportiert im gewählten Format (PNG verlustfrei, JPEG mit Qualitätsregler, TIFF, BMP) in den Ausgabeordner.

Weitere Bedienung:

- **Zoom:** Strg+Mausrad, „Einpassen“ oder „100 %“.
- **Escape** verwirft die Auswahl.
- Längere Vorgänge lassen sich abbrechen. Beim Schließen mit ungespeicherten Änderungen fragt der Editor nach.
- Bilder über 80 Megapixel sowie mehrseitige oder animierte Dateien werden aus Speichergründen abgelehnt.

## KI-Metadaten

Das Fenster schlägt **Jahreszeit, Titel und Stichwörter** vor und übernimmt vorhandene EXIF-Angaben (Aufnahmezeit, GPS, Urheber, Copyright) ins XMP.

### Einmalig einrichten

1. **Anbieterprofil** wählen. Standard ist `openai-economy` (OpenAI `gpt-5-mini`); alternativ Azure oder Gemini.
2. **API-Schlüssel** eingeben und „Speichern“ klicken. Er landet in der Windows-Anmeldeinformationsverwaltung. Azure meldet sich über Entra ID an; dafür ist kein Schlüssel nötig.
3. **Preise** (USD pro 1 Mio. Tokens: Eingabe, Cache, Ausgabe) von der Preisseite des Anbieters übernehmen und das **Prüfdatum** setzen. Ohne Preise startet keine kostenpflichtige Analyse.

### Ablauf

1. **Lokal prüfen** (kostenlos): Die App liest die vorhandenen Metadaten, bereitet EXIF→XMP-Übernahmen vor und zeigt, ob eine KI-Analyse nötig ist.
2. **Testlauf (3 Bilder)** oder **Analyse starten**: Vorher erscheint eine Zusammenfassung mit Anbieter, Bildanzahl, übertragenen Daten und maximalen Kosten.
3. **Vorschläge prüfen:** Ein Bild links wählen; in der Mitte stehen Feld, bisheriger Wert, Vorschlag, Quelle und Beleg. Mit „Übernehmen“ an- oder abwählen. Jahreszeiten mit niedriger Modellsicherheit sind nicht vorausgewählt.
4. **Ausgewählte Änderungen speichern:** Schreibt die Sidecar-Dateien. Direkt danach lässt sich die Speicherung rückgängig machen.

Weitere Funktionen:

- **Regeln:** Tab mit den Datenschutz- und Rechteregeln. Dort lässt sich die Jahreszeit auch **ohne KI** manuell für alle markierten Bilder setzen.
- **Chat:** Zum Beispiel „Setze die Jahreszeit auf Winter“ oder „Ergänze das Stichwort Alpen“. Der Chat erzeugt nur Vorschläge für die markierten Bilder; gespeichert wird erst über die Schaltfläche. Bilder werden dabei nicht erneut gesendet.
- **Vorlage & JSON:** Die Regeln für die Analyse lassen sich als JSON anpassen, validieren und als neue Version speichern.

### Was wird gesendet?

Übertragen werden nur:

- eine verkleinerte Vorschau (max. 1024 px) **ohne eingebettete Metadaten**,
- das Aufnahmedatum **ohne Uhrzeit**,
- die **Hemisphäre statt der genauen Position**,
- bereits vorhandene Titel und Stichwörter.

Keine Dateinamen oder Pfade, keine Seriennummern. Bilder mit einem Data-Mining-Verbot in den IPTC-Metadaten werden übersprungen. Bei einer vorhandenen EXIF-KI-Nutzungspräferenz fragt die App vor dem Senden nach.

### Hinweise

- Die Budgetgrenzen der Vorlage (Standard 5 USD pro Lauf, 0,03 USD pro Bild) gelten nur in der App. Für eine harte Grenze zusätzlich im Anbieterkonto ein Budget setzen.
- „Unklar ob verarbeitet“ heißt: Die Verbindung brach nach dem Senden ab. Die Anfrage wird bewusst nicht automatisch wiederholt, um doppelte Kosten zu vermeiden.
- Identische Bilder mit gleicher Vorlage kommen 30 Tage lang aus dem lokalen Cache und kosten nichts.
