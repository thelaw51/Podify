#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

APP_NAME="Podify.app"
SRC="bin/Release/net10.0-maccatalyst/maccatalyst-arm64/$APP_NAME"
DEST="/Applications/$APP_NAME"

echo "Publishing Release build for Apple Silicon…"
dotnet publish -f net10.0-maccatalyst -c Release -r maccatalyst-arm64 -p:CreatePackage=false

if [ ! -d "$SRC" ]; then
    echo "Build did not produce $SRC" >&2
    exit 1
fi

if [ -d "$DEST" ]; then
    echo "Removing previous install at $DEST"
    rm -rf "$DEST"
fi

echo "Copying to $DEST"
cp -R "$SRC" "$DEST"

xattr -dr com.apple.quarantine "$DEST" 2>/dev/null || true

echo "Installed. Launch with: open \"$DEST\""
