# ?? NEUE BILDEDITOR-ARCHITEKTUR

## ? **ALTES SYSTEM - PROBLEMATISCH:**

```
???????????????????????
?   ScrollViewer      ?
?  ?????????????????  ?
?  ?  Grid         ?  ?
?  ?  ???????????  ?  ?
?  ?  ? Image   ?  ?  ?  ? Zeigt Bild
?  ?  ???????????  ?  ?
?  ?  ???????????  ?  ?
?  ?  ? Canvas  ?  ?  ?  ? Overlay für Marker
?  ?  ???????????  ?  ?
?  ?????????????????  ?
???????????????????????
```

**Probleme:**
- Canvas muss EXAKT auf Bild ausgerichtet sein
- Koordinaten-Transformation komplex
- Zoom beeinflusst Canvas und Bild unterschiedlich
- Scroll-Position schwer zu managen

## ? **NEUES SYSTEM - EINFACH:**

```
???????????????????????
?   ScrollViewer      ?
?  ?????????????????  ?
?  ?  Image        ?  ?  ? Zeigt NUR Endergebnis
?  ?????????????????  ?
???????????????????????

Workflow:
1. User klickt ? Koordinaten werden gespeichert
2. User klickt "Text einfügen" ? Bild wird direkt bearbeitet
3. Neues Bild wird angezeigt ? FERTIG!
```

**Vorteile:**
- ? KEINE Canvas-Overlay-Probleme
- ? KEINE Koordinaten-Transformation
- ? KEINE Zoom-Sync-Probleme
- ? KEINE Scroll-Position-Probleme
- ? Direktes Feedback (WYSIWYG)

## ?? **IMPLEMENTATION:**

### 1. XAML - Nur Image, kein Canvas

```xaml
<ScrollViewer x:Name="ImageScrollViewer">
    <Image x:Name="DisplayImage" 
           Stretch="None"
           MouseLeftButtonDown="Image_MouseDown"
           Cursor="Cross"/>
</ScrollViewer>
```

### 2. Code - Direkter Workflow

```csharp
// Klick auf Bild
private void Image_MouseDown(object sender, MouseButtonEventArgs e)
{
    var position = e.GetPosition(DisplayImage);
    
    // Transformiere zu Original-Bild-Koordinaten
    var scaleX = imageOriginalWidth / DisplayImage.ActualWidth;
    var scaleY = imageOriginalHeight / DisplayImage.ActualHeight;
    
    clickX = position.X * scaleX;
    clickY = position.Y * scaleY;
    
    ShowPreview(); // Zeige Vorschau wo Text hin kommt
}

// Text einfügen - SOFORT sichtbar
private void ApplyText_Click(...)
{
    using (var img = Image.Load(workingFilePath))
    {
        img.Mutate(x => x.DrawText(...));
        img.Save(workingFilePath);
    }
    
    ReloadImage(); // Einfach neu laden - KEINE Scroll-Probleme!
}
```

### 3. Koordinaten-Transformation - EINFACH

```csharp
// Anzeige ? Original
double imageX = displayX * (imageOriginalWidth / DisplayImage.ActualWidth);
double imageY = displayY * (imageOriginalHeight / DisplayImage.ActualHeight);

// Original ? Anzeige
double displayX = imageX * (DisplayImage.ActualWidth / imageOriginalWidth);
double displayY = imageY * (DisplayImage.ActualHeight / imageOriginalHeight);
```

## ?? **VORSCHAU-SYSTEM:**

Statt Canvas-Marker verwenden wir ein **temporäres Overlay-Image**:

```csharp
<Grid>
    <Image x:Name="DisplayImage"/>
    <Image x:Name="PreviewOverlay" Opacity="0.7"/>
</Grid>
```

Wenn User klickt:
1. Erstelle Vorschau-Bild mit Marker
2. Zeige in PreviewOverlay
3. Bei "Text einfügen" ? Schreibe direkt ins Hauptbild
4. Verstecke PreviewOverlay

## ? **VORTEILE:**

| Aspekt | Altes System | Neues System |
|--------|-------------|--------------|
| Koordinaten | Komplex mit Canvas | Einfache Skalierung |
| Zoom | Sync-Probleme | Automatisch korrekt |
| Scroll | Manuelles Restore | Automatisch korrekt |
| Preview | Canvas-Marker | Echtes Bild-Overlay |
| Code | 500+ Zeilen | ~200 Zeilen |

## ?? **NÄCHSTE SCHRITTE:**

1. Entferne Canvas aus XAML
2. Füge MouseDown zu Image hinzu
3. Implementiere einfache Koordinaten-Transformation
4. Teste!

**Soll ich diese NEUE ARCHITEKTUR implementieren?**
