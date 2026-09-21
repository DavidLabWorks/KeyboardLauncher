#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
test_binary=$(mktemp /tmp/keyboard-launcher-tests.XXXXXX)
trap 'rm "$test_binary"' EXIT
sources=()
for source in Sources/KeyboardLauncher/*.swift; do
    if [[ "$source" != Sources/KeyboardLauncher/main.swift ]]; then
        sources+=("$source")
    fi
done
swiftc "${sources[@]}" Tests/*.swift -o "$test_binary"
"$test_binary"
