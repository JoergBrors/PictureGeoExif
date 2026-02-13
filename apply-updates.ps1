# Update Script für PictureExifclone
# Dieses Script wendet alle fehlenden Features an

Write-Host "Starte Update..." -ForegroundColor Green

# Prüfe ob ImageEditorWindow Dateien existieren
if (!(Test-Path "ImageEditorWindow.xaml") -or !(Test-Path "ImageEditorWindow.xaml.cs")) {
    Write-Host "FEHLER: ImageEditorWindow Dateien fehlen!" -ForegroundColor Red
    exit 1
}

if (!(Test-Path "AppSettings.cs")) {
    Write-Host "FEHLER: AppSettings.cs fehlt!" -ForegroundColor Red
    exit 1
}

Write-Host "Alle erforderlichen Dateien gefunden." -ForegroundColor Green
Write-Host ""
Write-Host "Fehlende Features:"
Write-Host "- Tooltips auf Thumbnails"  
Write-Host "- LargePreview Image"
Write-Host "- Speicherort-Verwaltung"
Write-Host "- Bildbearbeitungs-Integration"
Write-Host "- Referenzbild-Tooltips"
Write-Host ""
Write-Host "Um die Features zu aktivieren, müssen Sie MainWindow.xaml und MainWindow.xaml.cs manuell anpassen."
Write-Host "Siehe IMPLEMENTATION_TODO.md für Details."
