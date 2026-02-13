# PATCH ANLEITUNG - Bitte manuell anwenden

## Problem
Nach git checkout wurden alle Features zurückgesetzt.

## Lösung - In dieser Reihenfolge anwenden:

### 1. MainWindow.xaml.cs - EditImageButton_Click ersetzen (Zeile ~253-266)

ERSETZE die aktuelle Methode mit:

```csharp
private void EditImageButton_Click(object sender, RoutedEventArgs e)
{
    if (ImageListBox.SelectedItem is ImageItem selectedImage)
    {
        var editor = new ImageEditorWindow(
            selectedImage.FilePath, 
            selectedImage.HasGps ? (double?)selectedImage.Latitude : null, 
            selectedImage.HasGps ? (double?)selectedImage.Longitude : null)
        {
            Owner = this
        };

        if (editor.ShowDialog() == true && editor.EditedImageBytes != null)
        {
            try
            {
                var outDir = Path.Combine(Path.GetDirectoryName(selectedImage.FilePath) ?? ".", "edited", DateTime.Now.ToString("yyyyMMdd"));
                System.IO.Directory.CreateDirectory(outDir);

                var baseName = Path.GetFileNameWithoutExtension(selectedImage.FilePath);
                var ext = Path.GetExtension(selectedImage.FilePath);
                var newName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}_copy{ext}";
                var newPath = Path.Combine(outDir, newName);

                File.WriteAllBytes(newPath, editor.EditedImageBytes);

                selectedImage.FilePath = newPath;
                selectedImage.FileName = newName;
                selectedImage.Thumbnail = CreateThumbnail(newPath);

                if (selectedImage.HasGps)
                {
                    WriteGpsToImage(newPath, selectedImage.Latitude, selectedImage.Longitude);
                }

                ImageListBox.Items.Refresh();
                MessageBox.Show($"Bearbeitetes Bild gespeichert:\n{newPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
```

### 2. MainWindow.xaml.cs - ImageItem Class erweitern (Zeile ~529)

FÜGE HINZU nach "public bool IsModified { get; set; }":

```csharp
public string ExifInfo { get; set; } = string.Empty;
```

### 3. MainWindow.xaml.cs - LoadImages erweitern (nach ReadExifData Aufruf, Zeile ~175)

FÜGE HINZU:

```csharp
try
{
    var fileInfo = new FileInfo(filePath);
    imageItem.ExifInfo = $"Größe: {fileInfo.Length / 1024} KB | Erstellt: {fileInfo.CreationTime:dd.MM.yyyy HH:mm}";
}
catch
{
    imageItem.ExifInfo = "";
}
```

### 4. MainWindow.xaml - ListBox ItemTemplate ändern (Zeile ~44-62)

ERSETZE die DataTemplate mit:

```xaml
<DataTemplate>
    <Border BorderBrush="#FFDDDDDD" BorderThickness="1" 
            Margin="5" Padding="5" Background="White"
            ToolTipService.InitialShowDelay="1000">
        <Border.ToolTip>
            <ToolTip>
                <StackPanel MaxWidth="300">
                    <TextBlock Text="{Binding FileName}" FontWeight="Bold" TextWrapping="Wrap"/>
                    <TextBlock Text="{Binding GpsInfo}" Margin="0,5,0,0" TextWrapping="Wrap"/>
                    <TextBlock Text="{Binding ExifInfo}" Margin="0,5,0,0" TextWrapping="Wrap"/>
                </StackPanel>
            </ToolTip>
        </Border.ToolTip>
        <Image Source="{Binding Thumbnail}" 
               Height="100" Stretch="Uniform"/>
    </Border>
</DataTemplate>
```

## Testen
1. Build das Projekt
2. Starte die App
3. Lade ein Bild
4. Klicke "Bild bearbeiten"
5. Editor sollte sich öffnen mit Zoom/Crop/Text Funktionen

## Falls weitere Features gewünscht:
- Speicherort-Verwaltung: Siehe AppSettings.cs (schon vorhanden)
- LargePreview: Grid in MainWindow.xaml rechts splitten
- Referenzbild-Tooltips: UseReferenceImageButton_Click erweitern

Alle Dateien für Editor sind vorhanden:
? ImageEditorWindow.xaml
? ImageEditorWindow.xaml.cs  
? AppSettings.cs
