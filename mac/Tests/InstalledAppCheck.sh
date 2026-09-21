#!/bin/bash
# Run after deployment with the installed app running.
set -euo pipefail
count=$(pgrep -x KeyboardLauncher | wc -l | tr -d ' ')
if [ "$count" != 1 ]; then
    echo "Expected one KeyboardLauncher instance; found $count."
    exit 1
fi
echo "Installed app single-instance check passed."
