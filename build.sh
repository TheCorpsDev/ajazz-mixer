#!/usr/bin/env bash
# Compila il plugin per Windows e crea dist/ajazz-mixer.zip (cartella del plugin + install.ps1)
# da installare nel software Ajazz "Stream Dock".
set -euo pipefail
cd "$(dirname "$0")"

export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

PLUGIN=it.thecorpscloud.mixer.sdPlugin
OUT=dist/$PLUGIN

rm -rf dist
mkdir -p "$OUT/bin"
cp -r plugin/$PLUGIN/. "$OUT/"
cp install.ps1 dist/

dotnet publish src/OpenDeckMixer/OpenDeckMixer.csproj -c Release -r win-x64 -o "$OUT/bin" --nologo -v quiet

python3 - "$OUT" <<'PY'
import os, sys, zipfile
root = sys.argv[1]
base = os.path.dirname(root)
dest = os.path.join(base, "ajazz-mixer.zip")
with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED) as z:
    for folder, _, files in os.walk(root):
        for name in files:
            path = os.path.join(folder, name)
            z.write(path, os.path.relpath(path, base))
    z.write(os.path.join(base, "install.ps1"), "install.ps1")
print(dest)
PY
