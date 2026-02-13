# ?? Erweiterte Bildbearbeitung - Feature-Spec

## ?? Anforderungen

### 1. **Zoom & Cropping**
- ? Zoom In/Out (Mausrad + Buttons)
- ? Pan/Scroll durch Bild
- ? Crop-Tool mit Bereich-Auswahl
- ? Fit to Window

### 2. **Layer-System für Text/Wasserzeichen**
- ? Text-Layer hinzufügen (editierbar)
- ? Geo-Wasserzeichen-Layer
- ? Layer verschiebbar (Drag & Drop)
- ? Layer-Eigenschaften änderbar:
  - Schriftart
  - Schriftgröße (Slider)
  - Farbe (ColorPicker)
  - Position
  - Rotation (optional)
- ? Layer-Liste (anzeigen/verstecken/löschen)
- ? Erst beim "Anwenden" wird ins Bild gerendert

### 3. **Verpixelung mit Bereich-Auswahl**
- ? Rechteck aufziehen
- ? Pixelgröße einstellbar
- ? Vorschau vor Anwenden

### 4. **EXIF-Editor Dialog**
- ? Alle EXIF-Daten anzeigen
- ? GPS-Koordinaten editieren
- ? Datum/Zeit editieren
- ? Kamera-Infos anzeigen
- ? Copyright/Author editieren
- ? Speichern-Button

## ??? Architektur

### UI-Struktur:
```
???????????????????????????????????????????????????????
? Toolbar: [Zoom] [Text] [Geo] [Crop] [Pixelate]     ?
???????????????????????????????????????????????????????
?   Layer     ?      Bild-Canvas      ?  Eigenschaften?
?   Liste     ?    (mit Overlays)     ?    Panel      ?
?             ?                       ?               ?
? ? Text1     ?                       ? Farbe: [?]   ?
? ? Geo1      ?      [  BILD  ]       ? Größe: 48px  ?
? ? Text2     ?                       ? X: 100       ?
?             ?                       ? Y: 200       ?
? [+ Layer]   ?                       ?              ?
???????????????????????????????????????????????????????
? Status: Zoom 100% | Image: 4032x3024               ?
???????????????????????????????????????????????????????
```

### Datenmodell:

```csharp
class TextLayer
{
    public string Text { get; set; }
    public Point Position { get; set; }
    public Color Color { get; set; }
    public int FontSize { get; set; }
    public bool Visible { get; set; }
    public string FontFamily { get; set; }
}

class PixelationArea
{
    public Rectangle Bounds { get; set; }
    public int PixelSize { get; set; }
}
```

## ?? Implementierungs-Phasen

### Phase 1: Zoom & Pan (JETZT)
- ScrollViewer mit Zoom
- Mausrad-Zoom
- Pan mit Maus

### Phase 2: Layer-System
- TextLayer-Klasse
- Layer-Canvas über Bild
- Drag & Drop für Layer

### Phase 3: Eigenschaften-Panel
- ColorPicker
- Slider für Größe
- Position-Input

### Phase 4: EXIF-Editor
- EXIF-Dialog
- MetadataExtractor nutzen
- ExifTool integration

## ?? Code-Struktur

### Neue Dateien:
```
/Controls/
  - LayerControl.xaml/cs       # Layer-Item in Liste
  - PropertiesPanel.xaml/cs    # Eigenschaften rechts
  - ExifEditorDialog.xaml/cs   # EXIF-Editor

/Models/
  - TextLayer.cs
  - GeoWatermarkLayer.cs
  - PixelationArea.cs
  - ExifData.cs

/ImageEditorWindow.xaml/cs     # Haupt-Editor
```

## ?? Start-Implementierung

Wir beginnen mit einer **erweiterten ImageEditorWindow** mit:
1. Zoom-Funktionalität
2. Einfaches Layer-System
3. Basic Properties Panel

Danach erweitern wir schrittweise.

## ?? Technologie-Stack

- **WPF Canvas** für Layer-Overlays
- **ScrollViewer** für Zoom/Pan
- **InkCanvas** (optional) für Freihand
- **ColorPicker** (Extended WPF Toolkit)
- **MetadataExtractor** für EXIF
- **SixLabors.ImageSharp** für Rendering

## ?? Geschätzte Implementierungszeit

- Phase 1 (Zoom): 30-45 Min ?
- Phase 2 (Layer): 60-90 Min
- Phase 3 (Properties): 45-60 Min
- Phase 4 (EXIF): 30-45 Min

**Gesamt: ~3-4 Stunden Development**

---

**Soll ich mit Phase 1 (Zoom & Pan) beginnen?**
