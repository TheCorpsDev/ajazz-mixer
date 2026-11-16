# Installa (o aggiorna) il plugin Mixer nel software Ajazz "Stream Dock".
# Uso: tasto destro su install.ps1 -> "Esegui con PowerShell"
$ErrorActionPreference = "Stop"

$name = "it.thecorpscloud.mixer.sdPlugin"
$source = Join-Path $PSScriptRoot $name
$plugins = Join-Path $env:APPDATA "HotSpot\StreamDock\plugins"
$target = Join-Path $plugins $name
$exe = Join-Path $target "bin\AjazzMixer.exe"

if (-not (Test-Path $source)) { throw "Cartella $name non trovata accanto allo script." }
if (-not (Test-Path $plugins)) { throw "Software Ajazz non trovato: manca $plugins" }

$update = Test-Path $exe
if ($update) {
    # Resti di aggiornamenti precedenti
    Get-ChildItem (Join-Path $target "bin") -Filter "*.old" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }
    # Il software Ajazz riavvia subito il plugin se lo chiudo, quindi l'exe resta bloccato.
    # Windows però permette di rinominare un exe in esecuzione: lo sposto e copio il nuovo.
    Rename-Item $exe ("AjazzMixer.{0}.old" -f (Get-Date -Format "yyyyMMddHHmmss"))
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item (Join-Path $source "*") $target -Recurse -Force

if ($update) {
    # Chiudo la vecchia versione: il software Ajazz riavvia il plugin con il nuovo exe
    Get-Process AjazzMixer -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Plugin aggiornato in $target"
} else {
    Write-Host "Plugin installato in $target"
    Write-Host "Riavvia il software Stream Dock AJAZZ (anche dall'icona vicino all'orologio) per caricarlo."
}
