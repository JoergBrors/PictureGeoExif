<#
.SYNOPSIS
  Rebuilds licenses/ from the packages that are actually shipped with PictureExifclone.

.DESCRIPTION
  Reads obj/project.assets.json (runtime dependencies incl. transitive ones) and copies the
  license/notice files straight out of the local NuGet cache. Packages that only declare an
  SPDX expression get the standard text from licenses/_spdx plus the copyright from their nuspec.
  The self-contained .NET and Windows Desktop runtime packs are included as well, because the
  release workflow publishes self-contained.

  Hand-maintained entries (Leaflet, OpenStreetMap, packages without license metadata in
  licenses/_manual) are kept. Nothing is downloaded from package pages; no HTML is stored.

  Afterwards update THIRD-PARTY-LICENSES.md if the summary lists new packages or licenses.

.EXAMPLE
  pwsh scripts/Update-ThirdPartyLicenses.ps1
#>
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$licenses = Join-Path $root 'licenses'
$project = Join-Path $root 'PictureExifclone.csproj'

dotnet restore $project -r $Runtime | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }

$assets = Get-Content (Join-Path $root 'obj/project.assets.json') -Raw | ConvertFrom-Json
$cache = $assets.project.restore.packagesPath
$targetName = $assets.targets.PSObject.Properties.Name | Where-Object { $_ -like "*/$Runtime" } | Select-Object -First 1
if (-not $targetName) { throw "No restore target for runtime $Runtime" }
$shipped = $assets.targets.$targetName.PSObject.Properties |
    Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Name }

function Get-SpdxText([string]$expression, [string]$copyright) {
    $file = Join-Path $licenses "_spdx/$expression.txt"
    if (-not (Test-Path $file)) { return $null }
    $text = Get-Content $file -Raw
    $holder = $copyright -replace '^(Copyright\s*)?(\(c\)|©)?\s*', ''
    return $text.Replace('<copyright holders>', $holder)
}

function Export-Package([string]$id, [string]$version, [string]$packageDir, [string]$kind) {
    $nuspec = Get-ChildItem $packageDir -Filter '*.nuspec' | Select-Object -First 1
    [xml]$xml = Get-Content $nuspec.FullName -Raw
    $meta = $xml.package.metadata
    $licenseNode = $meta.license
    $expression = if ($licenseNode -and $licenseNode.type -eq 'expression') { $licenseNode.'#text' } else { $null }
    $copyright = if ($meta.copyright) { $meta.copyright.Trim() } else { $meta.authors }

    $target = Join-Path $licenses $id
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    New-Item $target -ItemType Directory | Out-Null

    $copied = @(Get-ChildItem $packageDir -File | Where-Object { $_.Name -match '^(LICEN[CS]E|NOTICE|THIRD-PARTY-NOTICES)' })
    foreach ($f in $copied) { Copy-Item $f.FullName (Join-Path $target $f.Name) }

    $source = 'package file'
    # Notice files are not license texts; add the SPDX text whenever no LICENSE file is shipped.
    $hasLicense = @($copied | Where-Object { $_.Name -match '^LICEN[CS]E' }).Count -gt 0
    if (-not $hasLicense -and $expression) {
        $text = Get-SpdxText $expression $copyright
        if (-not $text) { throw "No SPDX template for $expression ($id)" }
        Set-Content (Join-Path $target "LICENSE.$expression.txt") "$id $version`nCopyright: $copyright`nLicense: $expression (SPDX, from nuspec)`n`n$text" -Encoding utf8
        $source = 'SPDX expression + licenses/_spdx template'
    }
    elseif (-not $hasLicense) {
        $manual = Join-Path $licenses "_manual/$id.txt"
        if (-not (Test-Path $manual)) { throw "No license information for $id; add licenses/_manual/$id.txt" }
        Copy-Item $manual (Join-Path $target 'LICENSE.txt')
        $source = "licenses/_manual (nuspec licenseUrl: $($meta.licenseUrl))"
    }

    [pscustomobject]@{
        Id = $id; Version = $version; Kind = $kind
        License = if ($expression) { $expression } elseif ($licenseNode) { "file: $($licenseNode.'#text')" } else { 'see licenses/_manual' }
        Copyright = $copyright; ProjectUrl = $meta.projectUrl; Source = $source
        Files = @(Get-ChildItem $target -File | ForEach-Object { "licenses/$id/$($_.Name)" })
    }
}

$summary = @()
foreach ($name in $shipped | Sort-Object) {
    $id, $version = $name -split '/', 2
    $library = $assets.libraries.$name
    $summary += Export-Package $id $version (Join-Path $cache $library.path) 'NuGet (Laufzeit)'
}

# Self-contained publish ships the runtime packs; use the newest pack of the target major version in the cache.
$major = ($assets.project.frameworks.PSObject.Properties.Name | Select-Object -First 1) -replace '^net(\d+).*', '$1'
foreach ($pack in "Microsoft.NETCore.App.Runtime.$Runtime", "Microsoft.WindowsDesktop.App.Runtime.$Runtime") {
    $dir = Join-Path $cache $pack.ToLowerInvariant()
    $version = Get-ChildItem $dir -Directory -ErrorAction SilentlyContinue | Where-Object Name -like "$major.*" |
        Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (-not $version) { Write-Warning "$pack $major.x not in cache; run a self-contained publish first."; continue }
    $summary += Export-Package $pack $version.Name $version.FullName 'Runtime (self-contained)'
}

$summary | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $licenses 'dependency-licenses-summary.json') -Encoding utf8
$summary | Format-Table Id, Version, License -AutoSize
Write-Host "Updated $($summary.Count) entries. Check THIRD-PARTY-LICENSES.md for new packages or license changes."
