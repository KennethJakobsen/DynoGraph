#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="$ROOT_DIR/artifacts/publish/$RID"
APP_DIR="$ROOT_DIR/artifacts/RollerGraph.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

dotnet publish "$ROOT_DIR/src/RollerGraph.App/RollerGraph.App.csproj" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o "$PUBLISH_DIR"

rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

cp -R "$PUBLISH_DIR"/. "$MACOS_DIR"/
cp "$ROOT_DIR/src/RollerGraph.App/Assets/rollergraph-icon.icns" "$RESOURCES_DIR/rollergraph-icon.icns"
chmod +x "$MACOS_DIR/RollerGraph"

cat > "$CONTENTS_DIR/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>RollerGraph</string>
  <key>CFBundleExecutable</key>
  <string>RollerGraph</string>
  <key>CFBundleIconFile</key>
  <string>rollergraph-icon</string>
  <key>CFBundleIdentifier</key>
  <string>app.rollergraph.RollerGraph</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>RollerGraph</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

echo "Created $APP_DIR"
