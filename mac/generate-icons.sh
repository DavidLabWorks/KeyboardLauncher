#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
iconset="build/AppIcon.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" assets/branding/AppIcon.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
    retina=$((size * 2))
    sips -z "$retina" "$retina" assets/branding/AppIcon.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o AppIcon.icns
sips -z 20 20 assets/branding/MenuBarTemplate.png --out assets/MenuBarTemplate.png >/dev/null
sips -z 40 40 assets/branding/MenuBarTemplate.png --out assets/MenuBarTemplate@2x.png >/dev/null
