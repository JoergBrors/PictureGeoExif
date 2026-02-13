# PictureExifclone - Bild Georeferenzierungs-Anwendung

Eine WPF-Anwendung für .NET 8, die es ermöglicht, GPS-Koordinaten zu Bildern hinzuzufügen.

## Funktionen

### 1. Bilder laden
- **Dateiauswahl**: Klicken Sie auf "Bilder laden" um einen oder mehrere Bilder auszuwählen
- **Drag & Drop**: Ziehen Sie Bilder direkt in die Anwendung
- Unterstützte Formate: JPG, JPEG, PNG, BMP, TIF, TIFF

### 2. GPS-Koordinaten auswählen
Es gibt drei Möglichkeiten, GPS-Koordinaten festzulegen:

#### a) OpenStreetMap-Karte
- Klicken Sie auf die Karte, um einen Punkt auszuwählen
- Die Koordinaten werden automatisch übernommen

#### b) Referenzbild
- Klicken Sie auf "Referenzbild verwenden"
- Wählen Sie ein Bild mit bestehenden GPS-EXIF-Daten
- Die GPS-Koordinaten werden aus dem Referenzbild extrahiert

#### c) Vorhandenes Bild bearbeiten
- Wählen Sie ein Bild aus der Liste, das bereits GPS-Daten hat
- Klicken Sie auf "Bild bearbeiten"
- Die GPS-Koordinaten des Bildes werden auf der Karte angezeigt

### 3. GPS-Daten anwenden
- Wählen Sie ein Bild aus der Liste
- Stellen Sie sicher, dass GPS-Koordinaten ausgewählt sind
- Klicken Sie auf "GPS auf ausgewähltes Bild anwenden"
- Die GPS-Daten werden in die EXIF-Metadaten des Bildes geschrieben

### 4. Alle Bilder speichern
- Klicken Sie auf "Alle speichern"
- Die Anwendung fragt, ob die aktuellen GPS-Koordinaten auf alle Bilder ohne GPS-Daten angewendet werden sollen
- Bestätigen Sie, um alle Änderungen zu speichern

## Technische Details

### Verwendete NuGet-Pakete
- **Microsoft.Web.WebView2**: Für die Integration der OpenStreetMap/Leaflet-Karte
- **MetadataExtractor**: Zum Lesen von EXIF-Metadaten
- **SixLabors.ImageSharp**: Zum Schreiben von EXIF-GPS-Daten

### GPS-Datenformat
Die Anwendung schreibt folgende EXIF-Tags:
- `GPSLatitude`: Breitengrad im Grad/Minuten/Sekunden-Format
- `GPSLongitude`: Längengrad im Grad/Minuten/Sekunden-Format
- `GPSLatitudeRef`: N (Nord) oder S (Süd)
- `GPSLongitudeRef`: E (Ost) oder W (West)

## Bedienung

1. Starten Sie die Anwendung
2. Laden Sie Bilder per Drag & Drop oder über den Button "Bilder laden"
3. Wählen Sie GPS-Koordinaten auf einer der drei Arten:
   - Klicken auf die Karte
   - Referenzbild verwenden
   - Aus vorhandenem Bild
4. Wenden Sie die Koordinaten auf einzelne oder alle Bilder an
5. Speichern Sie die Änderungen

## Hinweise

- Die Anwendung überschreibt die Originaldateien. Erstellen Sie vorher eine Sicherungskopie!
- GPS-Koordinaten werden direkt in die EXIF-Metadaten geschrieben
- Die Karte zeigt standardmäßig Deutschland (Zentrum bei 51.1657°N, 10.4515°E)
- Bereits vorhandene GPS-Daten in Bildern werden in der Liste angezeigt

## Systemanforderungen

- Windows 10 oder höher
- .NET 8 Runtime
- WebView2 Runtime (wird normalerweise mit Windows 10/11 mitgeliefert)
