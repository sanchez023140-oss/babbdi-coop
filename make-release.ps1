# Builds the friend-facing zip: the plugin only, no game files.
#   powershell -ExecutionPolicy Bypass -File make-release.ps1
# Result: E:\858\BabbdiWork\Release\BabbdiCoop-v<version>.zip
# ASCII only on purpose: PowerShell 5.1 misreads non-BOM UTF-8 Cyrillic in scripts.
# All Russian text lives in RELEASE-README.txt and is read as UTF-8 explicitly.

$ErrorActionPreference = 'Stop'

$root    = $PSScriptRoot
$plugin  = Join-Path $root 'BabbdiCoop\Plugin.cs'
$readmeT = Join-Path $root 'RELEASE-README.txt'
$game    = 'E:\858\BABBDI_WINDOWS_1.2.3'
$src     = Join-Path $game 'BepInEx\plugins\BabbdiCoop'
$relDir  = 'E:\858\BabbdiWork\Release'

$version = (Select-String -LiteralPath $plugin -Pattern 'PluginVersion\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value
if (-not $version) { throw 'PluginVersion not found in Plugin.cs' }
Write-Host "version: $version"

if (-not (Test-Path $src)) { throw "plugin is not deployed: $src (run build.ps1 first)" }
$dll = Join-Path $game 'BepInEx\plugins\BabbdiCoop.dll'
if (-not (Test-Path $dll)) { throw "BabbdiCoop.dll is missing: $dll" }

$stage = Join-Path $relDir "BabbdiCoop-v$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$dst = Join-Path $stage 'BepInEx\plugins\BabbdiCoop'
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# the DLL sits in plugins\, its assets sit in plugins\BabbdiCoop\ - the zip must mirror that
Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'BepInEx\plugins\BabbdiCoop.dll') -Force
# runtime debug output is not part of a release
$junk = @('*.csv', '*.txt')
$junkDirs = @('map', 'preview', 'texdump', 'npcscan')
Get-ChildItem $src -Recurse -File | Where-Object {
        $n = $_.Name
        if ($junk | Where-Object { $n -like $_ }) { return $false }
        $rel = $_.FullName.Substring($src.Length).TrimStart('\')
        $parts = $rel -split '\\'
        foreach ($p in $parts) { if ($junkDirs -contains $p) { return $false } }
        return $true
    } |
    ForEach-Object {
        $rel = $_.FullName.Substring($src.Length).TrimStart('\')
        $out = Join-Path $dst $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $out -Force
    }
$mb = (Get-ChildItem $stage -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("plugin copied: {0:N0} MB" -f $mb)

$readme = [System.IO.File]::ReadAllText($readmeT, [System.Text.Encoding]::UTF8)
$readme = $readme.Replace('{{VERSION}}', $version)
$readmePath = Join-Path $stage 'README-INSTALL.txt'
[System.IO.File]::WriteAllText($readmePath, $readme, (New-Object System.Text.UTF8Encoding($true)))

$zip = Join-Path $relDir "BabbdiCoop-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$size = (Get-Item $zip).Length / 1MB
Write-Host ("done: {0} ({1:N0} MB)" -f $zip, $size)
