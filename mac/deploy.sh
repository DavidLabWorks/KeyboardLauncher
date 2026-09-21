#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
bash build.sh

# Migrate the earlier misspelled app without changing its signing identity.
legacy_app=/Applications/KeybordLauncher.app
login_agent="$HOME/Library/LaunchAgents/com.custom.launchpick.plist"
if [ -f "$login_agent" ]; then
    launchctl unload "$login_agent" 2>/dev/null || true
fi
pkill -x KeybordLauncher 2>/dev/null || true
for attempt in {1..50}; do
    pgrep -x KeybordLauncher >/dev/null || break
    sleep 0.1
done
if pgrep -x KeybordLauncher >/dev/null; then
    echo "Previous app has not exited; installation stopped."
    exit 1
fi

if [ -d /Applications/KeyboardLauncher.app ]; then
    mkdir -p dist/backup
    ditto /Applications/KeyboardLauncher.app "dist/backup/KeyboardLauncher-$(date +%Y%m%d-%H%M%S).app"
fi
pkill -x KeyboardLauncher 2>/dev/null || true
# Wait for Launch Services to observe termination before replacing and reopening.
for attempt in {1..50}; do
    pgrep -x KeyboardLauncher >/dev/null || break
    sleep 0.1
done
if pgrep -x KeyboardLauncher >/dev/null; then
    echo "KeyboardLauncher has not exited; installation stopped."
    exit 1
fi
ditto build/KeyboardLauncher.app /Applications/KeyboardLauncher.app
codesign --verify --deep --strict /Applications/KeyboardLauncher.app
if [ -d "$legacy_app" ]; then
    mkdir -p dist/backup
    mv "$legacy_app" "dist/backup/KeybordLauncher-legacy-$(date +%Y%m%d-%H%M%S).app"
fi
if [ -f "$login_agent" ]; then
    plutil -replace ProgramArguments -json '["/Applications/KeyboardLauncher.app/Contents/MacOS/KeyboardLauncher"]' "$login_agent"
    launchctl load "$login_agent"
else
    open /Applications/KeyboardLauncher.app || { sleep 1; open /Applications/KeyboardLauncher.app; }
fi
# Do not reset TCC: stable signing and bundle ID preserve existing grants.
echo "Installed and launched KeyboardLauncher."
