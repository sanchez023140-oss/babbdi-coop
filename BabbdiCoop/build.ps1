# Build BabbdiCoop and copy plugin + avatar models into BepInEx plugins folder.
$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User')

$ProjectDir = Split-Path $MyInvocation.MyCommand.Path
$GameDir = "E:\858\BABBDI_WINDOWS_1.2.3"
$PluginsDir = Join-Path $GameDir "BepInEx\plugins"
$ModelDir = Join-Path $PluginsDir "BabbdiCoop"

Write-Host "Building..."
dotnet build (Join-Path $ProjectDir "BabbdiCoop.csproj") -v minimal
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

New-Item -ItemType Directory -Force (Join-Path $ModelDir "Textures") | Out-Null
Copy-Item -Force (Join-Path $ProjectDir "bin\Debug\netstandard2.1\BabbdiCoop.dll") $PluginsDir

# Avatar models (Kenney Blocky Characters, CC0). GLB is preferred by the plugin.
$ObjSrc = Join-Path $ProjectDir "model"
if (Test-Path $ObjSrc) {
    Copy-Item -Force (Join-Path $ObjSrc "*.glb") $ModelDir -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $ObjSrc "*.obj") $ModelDir -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $ObjSrc "*.mtl") $ModelDir -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $ObjSrc "Textures\*") (Join-Path $ModelDir "Textures") -ErrorAction SilentlyContinue
}

# v0.9.25: the five roof figures, built procedurally in the game's own style
# (two primitives: skin + cloth on one 2048x1024 atlas) -> figures\fig_1..5.glb
$FigSrc = Join-Path $ObjSrc "figures"
if (Test-Path $FigSrc) {
    $FigDst = Join-Path $ModelDir "figures"
    New-Item -ItemType Directory -Force $FigDst | Out-Null
    Copy-Item -Force (Join-Path $FigSrc "*.glb") $FigDst -ErrorAction SilentlyContinue
    Write-Host ("Figures: " + ((Get-ChildItem $FigDst -Filter *.glb | Measure-Object).Count) + " GLB in $FigDst")
}

# v0.10.8: the six sloppy neon patterns for the air-hockey tables on the annex's lower floor
$PatSrc = Join-Path $ObjSrc "patterns"
if (Test-Path $PatSrc) {
    $PatDst = Join-Path $ModelDir "patterns"
    New-Item -ItemType Directory -Force $PatDst | Out-Null
    Copy-Item -Force (Join-Path $PatSrc "*.png") $PatDst -ErrorAction SilentlyContinue
    Write-Host ("Patterns: " + ((Get-ChildItem $PatDst -Filter *.png | Measure-Object).Count) + " PNG in $PatDst")
}

# v0.10.17: the five Stable-Diffusion side skins for the arcade cabinets
$ArcSrc = Join-Path $ObjSrc "arcade_art"
if (Test-Path $ArcSrc) {
    $ArcDst = Join-Path $ModelDir "arcade_art"
    New-Item -ItemType Directory -Force $ArcDst | Out-Null
    Copy-Item -Force (Join-Path $ArcSrc "*.png") $ArcDst -ErrorAction SilentlyContinue
    Write-Host ("Arcade art: " + ((Get-ChildItem $ArcDst -Filter *.png | Measure-Object).Count) + " PNG in $ArcDst")
}

# v0.10.18: the "Dinozavrik" game assets (grey brutalist background/ground/sprites)
$DinoSrc = Join-Path $ObjSrc "arcade_game"
if (Test-Path $DinoSrc) {
    $DinoDst = Join-Path $ModelDir "arcade_game"
    New-Item -ItemType Directory -Force $DinoDst | Out-Null
    Copy-Item -Force -Recurse (Join-Path $DinoSrc "*") $DinoDst -ErrorAction SilentlyContinue
    Write-Host ("Dino assets: " + ((Get-ChildItem $DinoDst -Recurse -File | Measure-Object).Count) + " files in $DinoDst")
}

# v0.10.15: the acid-green score digits (0-9 and the hyphen) for the lamp-end boards
$ScoreSrc = Join-Path $ObjSrc "score_glyphs.png"
if (Test-Path $ScoreSrc) {
    Copy-Item -Force $ScoreSrc $ModelDir
    Write-Host ("Score glyphs: score_glyphs.png in " + $ModelDir)
}

# v0.10.11: air-hockey SFX (click, goal banka, insert coin)
$SndSrc = Join-Path $ObjSrc "sounds"
if (Test-Path $SndSrc) {
    $SndDst = Join-Path $ModelDir "sounds"
    New-Item -ItemType Directory -Force $SndDst | Out-Null
    Copy-Item -Force (Join-Path $SndSrc "*.mp3") $SndDst -ErrorAction SilentlyContinue
    Write-Host ("Sounds: " + ((Get-ChildItem $SndDst -Filter *.mp3 | Measure-Object).Count) + " MP3 in $SndDst")
}

Write-Host "Deployed to $PluginsDir"
