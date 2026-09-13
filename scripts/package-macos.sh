#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_BIN="${DOTNET_BIN:-$HOME/.dotnet/dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
APP_NAME="Valerius AI"
APP="${APP_OUTPUT:-$HOME/Applications/$APP_NAME.app}"
rm -rf artifacts/AppIcon.iconset
mkdir -p artifacts/AppIcon.iconset
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" src/ValeriusAI.App/Assets/valerius-ai-icon.png --out "artifacts/AppIcon.iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size*2))
    sips -z "$double" "$double" src/ValeriusAI.App/Assets/valerius-ai-icon.png --out "artifacts/AppIcon.iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
iconutil -c icns artifacts/AppIcon.iconset -o artifacts/AppIcon.icns
"$DOTNET_BIN" publish src/ValeriusAI.App -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
rsync -a --delete artifacts/publish/ "$APP/Contents/MacOS/"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>$APP_NAME</string>
<key>CFBundleDisplayName</key><string>$APP_NAME</string>
<key>CFBundleExecutable</key><string>ValeriusAI</string>
<key>CFBundleIdentifier</key><string>com.valerius.localai</string>
<key>CFBundleVersion</key><string>3</string>
<key>CFBundleShortVersionString</key><string>0.2.0</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>NSHighResolutionCapable</key><true/>
<key>LSMinimumSystemVersion</key><string>13.0</string>
</dict></plist>
PLIST
cp artifacts/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
chmod +x "$APP/Contents/MacOS/ValeriusAI"
xattr -dr com.apple.FinderInfo "$APP" 2>/dev/null || true
xattr -dr com.apple.ResourceFork "$APP" 2>/dev/null || true
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
ditto -c -k --keepParent "$APP" "artifacts/Valerius-AI-macos-arm64.zip"
printf '%s\n' "$APP"
