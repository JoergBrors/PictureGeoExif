# fetch-dependency-licenses.ps1
# Erstellt Lizenz-Downloads/Infos für alle bekannten NuGet-Abhängigkeiten im Repo.
# Pfade anpassen falls nötig. Ausführen in PowerShell 7+ empfohlen.

$repoRoot = 'E:\Code\PictureGeoExif'
$licensesDir = Join-Path $repoRoot 'licenses'
$scriptsDir = Join-Path $repoRoot 'scripts'
New-Item -Path $licensesDir -ItemType Directory -Force | Out-Null
New-Item -Path $scriptsDir -ItemType Directory -Force | Out-Null

# Paketliste (Name + Version). Falls Version leer, wird die letzte verfügbare Version aus der NuGet-Registrierung ermittelt.
$packages = @(
    @{ Id = 'MetadataExtractor'; Version = '2.9.0' },
    @{ Id = 'Microsoft.Web.WebView2'; Version = '1.0.3719.77' },
    @{ Id = 'SixLabors.ImageSharp'; Version = '3.1.12' },
    @{ Id = 'SixLabors.Fonts'; Version = '2.1.3' },
    @{ Id = 'SixLabors.ImageSharp.Drawing'; Version = '2.1.7' },
    @{ Id = 'Ookii.Dialogs.Wpf'; Version = '5.0.1' },
    @{ Id = 'XmpCore'; Version = '' } # Version leer -> Latest wird ermittelt
)

# Adobe XMP SDK LICENSE (raw URL) - wird verwendet, falls XmpCore keine licenseUrl in NuGet hat
$xmpAdobeRawLicenseUrl = 'https://raw.githubusercontent.com/adobe/XMP-Toolkit-SDK/main/LICENSE'

# Hilfsfunktion: sichere Web-Anfrage
function Safe-InvokeWebRequest {
    param($uri, $outFile)
    try {
        Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 30 -OutFile $outFile -ErrorAction Stop
        return $true
    } catch {
        Write-Host "WARN: Fehler beim Herunterladen von $uri : $($_.Exception.Message)"
        return $false
    }
}

$summary = @()

foreach ($p in $packages) {
    $id = $p.Id
    $ver = $p.Version
    $idLower = $id.ToLower()
    $nugetApiUrl = "https://api.nuget.org/v3/registration5-gz-semver2/$idLower/index.json"

    $info = [ordered]@{
        Id = $id
        Version = $ver
        NuGetApiUrl = $nugetApiUrl
        NuGetPackagePage = $null
        NuGetLicensePage = $null
        LicenseExpression = $null
        LicenseUrl = $null
        LocalLicensePage = $null
        LocalLicenseFile = $null
        Note = $null
    }

    # 1) NuGet registration API abfragen (liefert licenseExpression, licenseUrl und ggf. Versionen)
    try {
        $apiJson = Invoke-RestMethod -Uri $nugetApiUrl -UseBasicParsing -ErrorAction Stop
        # Speichere Raw API-Ausgabe
        $pkgDirTemp = Join-Path $licensesDir $id
        New-Item -Path $pkgDirTemp -ItemType Directory -Force | Out-Null
        $apiOut = Join-Path $pkgDirTemp "$id-api.json"
        $apiJson | ConvertTo-Json -Depth 10 | Out-File -FilePath $apiOut -Encoding utf8

        # Versuche, die letzte catalogEntry zu lesen (Robustheit)
        $lastItem = $null
        try {
            $lastItem = $apiJson.items[-1].items[-1].catalogEntry
        } catch {
            # fallback: durchsuchen aller items
            foreach ($it in $apiJson.items) {
                if ($it.items) { $lastItem = $it.items[-1].catalogEntry }
            }
        }

        if ($lastItem) {
            if ([string]::IsNullOrEmpty($ver)) {
                if ($null -ne $lastItem.version) { $ver = $lastItem.version; $info.Version = $ver }
            }
            if ($null -ne $lastItem.licenseExpression) { $info.LicenseExpression = $lastItem.licenseExpression }
            if ($null -ne $lastItem.licenseUrl) { $info.LicenseUrl = $lastItem.licenseUrl }
        } else {
            $info.Note = "WARN: Konnte keine catalogEntry aus der NuGet-API extrahieren."
        }
    } catch {
        $info.Note = "WARN: NuGet API Abfrage schlug fehl: $($_.Exception.Message)"
    }

    # Erstelle Paket-spezifische Pfade (jetzt mit Version bekannt)
    if ([string]::IsNullOrEmpty($ver)) { $ver = 'unknown' ; $info.Version = $ver }
    $pkgBase = "$id-$ver"
    $pkgDir = Join-Path $licensesDir $id
    New-Item -Path $pkgDir -ItemType Directory -Force | Out-Null

    $nugetPackagePage = "https://www.nuget.org/packages/$id/$ver"
    $nugetLicensePage = "https://www.nuget.org/packages/$id/$ver/license"

    $apiOut = Join-Path $pkgDir "$pkgBase-api.json"
    $pageOut = Join-Path $pkgDir "$pkgBase-license-page.html"
    $licenseFileOut = Join-Path $pkgDir "$pkgBase-license.txt"

    $info.NuGetPackagePage = $nugetPackagePage
    $info.NuGetLicensePage = $nugetLicensePage
    $info.LocalLicensePage = $pageOut
    $info.LocalLicenseFile = $licenseFileOut

    # 2) NuGet-Lizenzseite als HTML speichern (für manuelle Auswertung)
    Safe-InvokeWebRequest -uri $nugetLicensePage -outFile $pageOut | Out-Null

    # 3) Falls licenseUrl vorhanden und direkt abrufbar (txt/md), herunterladen
    if ($info.LicenseUrl) {
        # manche licenseUrl sind nuget-Seiten, andere direkte txt/md URLs
        if ($info.LicenseUrl -match '\.txt$|\.md$|\.license$|raw') {
            Safe-InvokeWebRequest -uri $info.LicenseUrl -outFile $licenseFileOut | Out-Null
        } else {
            # versuche dennoch einen GET und speichere wenn möglich
            try {
                $resp = Invoke-WebRequest -Uri $info.LicenseUrl -UseBasicParsing -Method Get -TimeoutSec 20 -ErrorAction Stop
                $resp.Content | Out-File -FilePath $licenseFileOut -Encoding utf8
            } catch {
                $info.Note += " Hinweis: licenseUrl nicht direkt als Text speicherbar."
            }
        }
    }

    # Spezielles Handling für XmpCore: Adobe XMP SDK Lizenz verwenden, falls NuGet keine Lizenz-URL liefert
    if ($id -eq 'XmpCore') {
        if ([string]::IsNullOrEmpty($info.LicenseUrl)) {
            $info.LicenseUrl = $xmpAdobeRawLicenseUrl
            $info.LicenseExpression = 'BSD (as per Adobe XMP SDK)'
            $info.Note += " Lizenz-URL für XmpCore von Adobe XMP-Toolkit gesetzt."
            # versuche die Adobe LICENSE raw-URL herunterzuladen
            Safe-InvokeWebRequest -uri $xmpAdobeRawLicenseUrl -outFile $licenseFileOut | Out-Null
        } else {
            # Falls licenseUrl schon vorhanden, sicherstellen, dass eine Kopie lokal existiert
            if (-not (Test-Path $licenseFileOut)) {
                try {
                    Safe-InvokeWebRequest -uri $info.LicenseUrl -outFile $licenseFileOut | Out-Null
                } catch {
                    $info.Note += " Hinweis: Konnte XmpCore-Lizenz nicht automatisch herunterladen."
                }
            }
        }
    }

    # 4) Falls SixLabors.Fonts: schon bekannte Split-License lokal überschreiben/verifizieren (optional)
    if ($id -eq 'SixLabors.Fonts') {
        $known = Join-Path $licensesDir 'SixLabors.Fonts-2.1.3-license.txt'
        if (Test-Path $known) {
            Copy-Item -Path $known -Destination $licenseFileOut -Force
            $info.Note += " Lokale SixLabors-License-Datei kopiert."
            $info.LicenseExpression = 'SixLabors-Split-License'
        }
    }

    # 5) Falls MetadataExtractor und XmpCore: Vermerk hinzufügen
    if ($id -eq 'MetadataExtractor') {
        $info.Note += " Hinweis: MetadataExtractor kann XmpCore als Ergänzung benötigen."
    }

    $summary += $info
    Write-Host "Processed: $id $ver -> licenseExpression: $($info.LicenseExpression)  licenseUrl: $($info.LicenseUrl)"
}

# 6) Zusammenfassung als JSON speichern
$summaryOut = Join-Path $licensesDir 'dependency-licenses-summary.json'
$summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $summaryOut -Encoding utf8

Write-Host ""
Write-Host "Fertig. Gespeicherte Dateien unter: $licensesDir"
Write-Host "Zusammenfassung: $summaryOut"
Write-Host "Prüfe die HTML-Dateien und license.txt-Dateien manuell. Für WebView2 und SixLabors-Commercial-Szenarien besondere Prüfung erforderlich."