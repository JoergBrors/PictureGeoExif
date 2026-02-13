# Fehlende Features - Implementierungsplan

## Status
- ? ImageEditorWindow.xaml existiert
- ? ImageEditorWindow.xaml.cs existiert  
- ? AppSettings.cs existiert
- ? MainWindow.xaml fehlt Tooltips
- ? MainWindow.xaml fehlt LargePreview
- ? MainWindow.xaml fehlt Speicherort UI
- ? MainWindow.xaml.cs fehlt EditImageButton Integration
- ? MainWindow.xaml.cs fehlt Speicherort-Funktionalität
- ? MainWindow.xaml.cs fehlt Referenzbild-Tooltip

## Zu implementieren

### 1. MainWindow.xaml - Tooltips auf Thumbnails
ListBox ItemTemplate ändern: Nur Bild anzeigen, Filename/GPS/EXIF als Tooltip

### 2. MainWindow.xaml - LargePreview hinzufügen
Rechte Spalte: Split in Bildvorschau (links) + Karte (rechts)

### 3. MainWindow.xaml - Speicherort UI
GPS Info Bereich erweitern mit OutputFolderTextBox und ChangeOutputFolderButton

### 4. MainWindow.xaml.cs - ImageItem erweitern
ExifInfo Property hinzufügen

### 5. MainWindow.xaml.cs - Constructor erweitern
AppSettings laden und OutputFolderTextBox setzen

### 6. MainWindow.xaml.cs - LoadImages erweitern
ExifInfo befüllen (Dateigröße, Datum)

### 7. MainWindow.xaml.cs - EditImageButton_Click neu implementieren
ImageEditorWindow öffnen, bearbeitetes Bild in konfigurierten Ordner speichern

### 8. MainWindow.xaml.cs - ChangeOutputFolderButton_Click hinzufügen
Folder Dialog öffnen, Einstellungen speichern

### 9. MainWindow.xaml.cs - ImageListBox_SelectionChanged erweitern  
LargePreview aktualisieren, GPS-Marker setzen

### 10. MainWindow.xaml.cs - UseReferenceImageButton_Click erweitern
Tooltip mit Referenzbild-Thumbnail am Button setzen

### 11. PictureExifclone.csproj - Ookii.Dialogs.Wpf
Package für Folder Browser Dialog hinzufügen
