#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_BIN="${DOTNET_BIN:-$HOME/.dotnet/dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
APP_NAME="Valerius AI"
APP="${APP_OUTPUT:-$HOME/Applications/$APP_NAME.app}"
rm -rf artifacts/AppIcon.iconset artifacts/AppIcon.icns artifacts/ValeriusAI.iconset artifacts/ValeriusAI.icns artifacts/publish
rm -f artifacts/Valerius-AI-macos-arm64.zip
find src tests -type f \( -path '*/bin/*' -o -path '*/obj/*' \) -name '* 2.*' -delete
mkdir -p artifacts/ValeriusAI.iconset
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" src/ValeriusAI.App/Assets/valerius-ai-icon.png --out "artifacts/ValeriusAI.iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size*2))
    sips -z "$double" "$double" src/ValeriusAI.App/Assets/valerius-ai-icon.png --out "artifacts/ValeriusAI.iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
iconutil -c icns artifacts/ValeriusAI.iconset -o artifacts/ValeriusAI.icns
"$DOTNET_BIN" publish src/ValeriusAI.App -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -o artifacts/publish
find artifacts/publish -maxdepth 1 -type f -name '* 2.*' -delete
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
<key>CFBundleVersion</key><string>4</string>
<key>CFBundleShortVersionString</key><string>0.2.1</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleIconFile</key><string>ValeriusAI.icns</string>
<key>CFBundleIconName</key><string>ValeriusAI</string>
<key>NSHighResolutionCapable</key><true/>
<key>LSMinimumSystemVersion</key><string>13.0</string>
</dict></plist>
PLIST
rm -f "$APP/Contents/Resources/AppIcon.icns"
cp artifacts/ValeriusAI.icns "$APP/Contents/Resources/ValeriusAI.icns"
chmod +x "$APP/Contents/MacOS/ValeriusAI"
xattr -dr com.apple.FinderInfo "$APP" 2>/dev/null || true
xattr -dr com.apple.ResourceFork "$APP" 2>/dev/null || true
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
ditto -c -k --keepParent "$APP" "artifacts/Valerius-AI-macos-arm64.zip"
printf '%s\n' "$APP"
