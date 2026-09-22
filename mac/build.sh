#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

# Keep this identity stable across updates to preserve macOS permission identity.
SIGNING_IDENTITY="${SIGNING_IDENTITY:-4B99D3E06A52B42C8F5B103AB3C7738C436D3288}"
security find-identity -v -p codesigning | /usr/bin/grep -q "$SIGNING_IDENTITY" || {
    echo "Signing identity unavailable. Install the selected Apple Development certificate and private key."
    exit 1
}

ARCH="${ARCH:-$(uname -m)}"
case "$ARCH" in arm64|x86_64) ;; *) echo "Unsupported architecture: $ARCH"; exit 1 ;; esac
VERSION=$(/usr/bin/plutil -extract version raw -o - ../app.json)
APP_NAME=$(/usr/bin/plutil -extract name raw -o - ../app.json)
[ -n "$APP_NAME" ] || { echo "Invalid application name"; exit 1; }
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Invalid VERSION"; exit 1; }
swift build -c release --arch "$ARCH"
APP_DIR="build/KeyboardLauncher.app/Contents"
mkdir -p "$APP_DIR/MacOS" "$APP_DIR/Resources"
cp ".build/$ARCH-apple-macosx/release/KeyboardLauncher" "$APP_DIR/MacOS/"
cp Info.plist "$APP_DIR/"
/usr/bin/plutil -replace CFBundleName -string "$APP_NAME" "$APP_DIR/Info.plist"
/usr/bin/plutil -replace CFBundleDisplayName -string "$APP_NAME" "$APP_DIR/Info.plist"
/usr/libexec/PlistBuddy -c "Set CFBundleShortVersionString $VERSION" "$APP_DIR/Info.plist"
/usr/libexec/PlistBuddy -c "Set CFBundleVersion $VERSION" "$APP_DIR/Info.plist"
cp -R Resources/en.lproj Resources/zh-Hans.lproj "$APP_DIR/Resources/"
cp AppIcon.icns "$APP_DIR/Resources/"
cp assets/MenuBarTemplate.png assets/MenuBarTemplate@2x.png "$APP_DIR/Resources/"
codesign --force --options runtime --sign "$SIGNING_IDENTITY" build/KeyboardLauncher.app
codesign --verify --deep --strict build/KeyboardLauncher.app

mkdir -p build/keyboard-dmg dist
ditto build/KeyboardLauncher.app build/keyboard-dmg/KeyboardLauncher.app
if [ ! -e build/keyboard-dmg/Applications ]; then
    ln -s /Applications build/keyboard-dmg/Applications
fi
hdiutil create -volname KeyboardLauncher -srcfolder build/keyboard-dmg -ov -format UDZO "dist/KeyboardLauncher-$VERSION-$ARCH.dmg"
echo "Built signed app and DMG in build/ and dist/."
