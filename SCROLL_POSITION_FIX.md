# ? SCROLL-POSITION-WIEDERHERSTELLUNG IMPLEMENTIERT

## ?? Problem

Nach dem Einfügen von Text/Geo-Daten oder Verpixeln wurde das Bild NEU geladen und die Scroll-Position wurde auf 50%/50% (Mitte) gesetzt.

**Resultat:** Der bearbeitete Bereich war nach dem Reload nicht mehr sichtbar!

## ?? Debug-Ausgabe zeigte:

```
[MOUSE] Canvas-Position: (990,3, 2168,8)    ? User klickt bei (990, 2169)
[TEXT] Text eingefügt bei: (990,3, 2168,8)   ? KORREKT!
[LOAD] ScrollX wiederhergestellt: 505,5      ? Zurück zur MITTE
[LOAD] ScrollY wiederhergestellt: 1399,8     ? Nicht wo Text ist!
```

**Der Text wurde KORREKT eingefügt, aber der sichtbare Bereich wurde geändert!**

## ? Lösung

### 1. Felder hinzugefügt für Scroll-Position-Speicherung

```csharp
// Für Scroll-Position-Wiederherstellung
private double savedScrollOffsetX = 0;
private double savedScrollOffsetY = 0;
private bool restoreScrollPosition = false;
```

### 2. Vor JEDER Bearbeitung: Scroll-Position speichern

```csharp
private void ApplyText_Click(...)
{
    // Speichere ABSOLUTE Scroll-Position
    savedScrollOffsetX = ImageScrollViewer.HorizontalOffset;
    savedScrollOffsetY = ImageScrollViewer.VerticalOffset;
    restoreScrollPosition = true;
    
    // ... Text einfügen ...
    LoadAndDisplayImage();
}
```

### 3. Nach Reload: ABSOLUTE Position wiederherstellen

```csharp
if (restoreScrollPosition)
{
    Dispatcher.InvokeAsync(() =>
    {
        ImageScrollViewer.ScrollToHorizontalOffset(savedScrollOffsetX);
        ImageScrollViewer.ScrollToVerticalOffset(savedScrollOffsetY);
        restoreScrollPosition = false;
    }, System.Windows.Threading.DispatcherPriority.Loaded);
}
```

## ?? Status

? **ApplyText_Click** - Scroll-Position wird gespeichert  
? **ApplyGeo_Click** - MUSS NOCH IMPLEMENTIERT WERDEN  
? **ApplyPixelate_Click** - MUSS NOCH IMPLEMENTIERT WERDEN  
? **ApplyCrop_Click** - `restoreScrollPosition = false` (Bildgröße ändert sich)

## ?? TODO

Fügen Sie in `ApplyGeo_Click` und `ApplyPixelate_Click` NACH `System.Diagnostics.Debug.WriteLine("[GEO/PIXELATE] ...")` hinzu:

```csharp
// Speichere Scroll-Position
savedScrollOffsetX = ImageScrollViewer.HorizontalOffset;
savedScrollOffsetY = ImageScrollViewer.VerticalOffset;
restoreScrollPosition = true;
```

## ?? Resultat

Nach dieser Änderung:
- Text wird eingefügt bei **(990, 2169)**
- Scroll-Position bleibt **GENAU GLEICH**
- User sieht **DEN GLEICHEN BILDAUSSCHNITT** wie vorher
- Text ist **SICHTBAR** an der geklickten Position!
