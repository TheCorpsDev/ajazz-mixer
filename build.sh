#!/usr/bin/env bash
# Compila il plugin per Windows e crea dist/it.thecorpscloud.mixer.plugin.zip
# da installare in OpenDeck con Plugins → Install from file.
set -euo pipefail
cd "$(dirname "$0")"

export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

PLUGIN=it.thecorpscloud.mixer.sdPlugin
OUT=dist/$PLUGIN

rm -rf dist
mkdir -p "$OUT/bin"
cp -r plugin/$PLUGIN/. "$OUT/"

dotnet publish src/OpenDeckMixer/OpenDeckMixer.csproj -c Release -r win-x64 -o "$OUT/bin" --nologo -v quiet

python3 - "$OUT" <<'PY'
import os, sys, zipfile
root = sys.argv[1]
base = os.path.dirname(root)
dest = os.path.join(base, "it.thecorpscloud.mixer.plugin.zip")
with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED) as z:
    for folder, _, files in os.walk(root):
        for name in files:
            path = os.path.join(folder, name)
            z.write(path, os.path.relpath(path, base))
print(dest)
PY
