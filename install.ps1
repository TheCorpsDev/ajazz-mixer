# Installa (o aggiorna) il plugin Mixer nel software Ajazz "Stream Dock".
# Uso: tasto destro su install.ps1 -> "Esegui con PowerShell"
$ErrorActionPreference = "Stop"

$name = "it.thecorpscloud.mixer.sdPlugin"
$source = Join-Path $PSScriptRoot $name
$plugins = Join-Path $env:APPDATA "HotSpot\StreamDock\plugins"
$target = Join-Path $plugins $name

if (-not (Test-Path $source)) { throw "Cartella $name non trovata accanto allo script." }
if (-not (Test-Path $plugins)) { throw "Software Ajazz non trovato: manca $plugins" }

# Chiude la versione in esecuzione, altrimenti l'exe è bloccato e non si può sostituire
Get-Process AjazzMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Copy-Item $source $target -Recurse

Write-Host "Plugin installato in $target"
Write-Host "Riavvia il software Stream Dock AJAZZ (anche dall'icona vicino all'orologio) per caricarlo."
