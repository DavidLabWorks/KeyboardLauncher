# Keyboard Launcher

[English](README.md) · [简体中文](README.zh-CN.md)

A keyboard-shaped launcher for apps, URLs, shell commands, and keyboard shortcuts. Bring up the panel, then press a bound key or click its icon to run an action.

The repository contains separate native implementations:

| Platform | Implementation | Documentation |
| --- | --- | --- |
| macOS | Swift, SwiftUI, AppKit | This README |
| Windows | C#, WinUI 3, Windows App SDK, Win32 | [Windows guide](windows/README.md) |

The features below describe the **macOS implementation**. Windows has its own behavior and build instructions; the two versions do not have complete feature parity.

## macOS features

- **38 physical key positions:** a four-row keyboard layout. Bindings stay in their slots when another binding is removed. Configurations spanning multiple pages show page navigation controls.
- **Four action types:** open an app with optional arguments, open a URL, run a shell command, or send a recorded keyboard shortcut.
- **Quick binding:** click an empty key to create a binding; right-click a bound key to edit. Hover over a bound key and click the upper-right × to remove it.
- **Drag to rebind:** drag a bound keycap to an empty key to move it, or onto another binding to swap them. The destination highlights and icons update immediately while changes save in the background.
- **Independent editor:** the binding editor opens in its own window while the launcher remains visible. Select applications from a searchable icon grid.
- **Custom icons:** use the application icon, a categorized and searchable SF Symbols collection, an exact supported symbol name, or a local image. Symbol availability depends on macOS.
- **Configurable activation:** record a key combination or choose a key to double-tap, including left/right modifier keys. Each activation method has a Clear button. Long holds, repeats, and chords do not count as double-taps.
- **Multi-display behavior:** opens centered in the usable area of the display containing the pointer. Invoke it on another display while it is visible to move the panel there. Drag the header to reposition it.
- **Appearance and language:** System, Light, and Dark themes; English and Simplified Chinese, switchable immediately in General settings. Binding names are not translated.
- **Native presentation:** Liquid Glass on macOS 26 and later, with a visual-effect background on earlier systems. Clickable controls and the draggable header have matching pointer feedback.
- **Menu bar and startup:** menu bar access to the launcher, settings, and quit; optional launch at login.
- **Shortcut controls:** Accessibility permission status, optional system-shortcut conflict override, and a separate Spotlight shortcut setting.

## Using the macOS app

1. Open `KeyboardLauncher.app` from Applications.
2. Use **⌘⇧Space** to show or hide the panel. This is the default combination; double-tap activation must first be configured in **Settings → Shortcuts**.
3. Click an empty key, choose an action, give it a name, and save.
4. Open the launcher and press the corresponding physical key, or click its icon, to run the action. The panel closes after launching.

**Esc** closes the panel. Clicking the panel's blank area keeps it open; clicking another application outside the panel closes it. The gear opens Settings.

For a shortcut action such as `⌘⇧X`, choose **Keyboard Shortcut** in the editor and record the combination. Recording consumes the input to prevent it from also triggering another application's shortcut. Execution returns focus to the previous application when available and sends the combination through the system event stream, allowing global shortcuts such as screenshot tools to respond.

Grant **Accessibility** access when prompted for double-tap detection, shortcut recording/interception, and sending shortcuts. Keep the bundle identifier and signing identity consistent across updates to help preserve the permission identity; changing them can require authorization again.

### Shared keyboards and mice

Daily double-tap activation is processed at the session event stage, after HID-level input filters. Early input interception is reserved for shortcut recording. This avoids acting on input before sharing software has a chance to filter it.

The fix has been manually verified with **Deskflow**: double-tapping on the other computer no longer opens the Mac launcher. It does not read Deskflow logs or require a Deskflow integration. Other sharing tools have not been individually verified.

## Build and install on macOS

The deployment target is **macOS 13 or later**. Building the current source requires an Xcode toolchain with the **macOS 26 SDK**, because it references the Liquid Glass API behind a runtime availability check.

Run from the repository root:

```sh
# Compile the executable without packaging or signing.
(cd mac && swift build -c release)

# Build a signed app and a DMG for the current machine architecture.
SIGNING_IDENTITY="YOUR_CODE_SIGNING_IDENTITY" bash mac/build.sh
# Set ARCH=x86_64 for Intel, or ARCH=arm64 for Apple Silicon.

# Build, back up the installed app, replace it, and launch it.
SIGNING_IDENTITY="YOUR_CODE_SIGNING_IDENTITY" bash mac/deploy.sh
```

A suitable signing certificate and its private key must be available in the macOS keychain. The scripts currently default to the maintainer's local development identity; override `SIGNING_IDENTITY` on another machine.

Outputs:

- App: `mac/build/KeyboardLauncher.app`
- Disk image: `mac/dist/KeyboardLauncher-1.0.1-<architecture>.dmg`
- Installed app: `/Applications/KeyboardLauncher.app`
- Installation backups: `mac/dist/backup/`

The packaging script signs the app but does **not** notarize it. A development-signed local build is not equivalent to a notarized public release. The scripts build for the host architecture, not a universal binary.

## Configuration

Bindings and activation shortcuts are stored at:

```text
~/.config/launchpick/config.json
```

The legacy directory name and bundle identifier `com.custom.launchpick` are retained for continuity with existing installations. Appearance and language are stored separately in macOS preferences. Login startup uses `~/Library/LaunchAgents/com.custom.launchpick.plist`.

Prefer the editor for routine changes. If editing JSON manually, back up the file first and restart the app afterward. A minimal example:

```json
{
  "shortcut": "cmd+shift+space",
  "doubleTapKey": "59:Left Control",
  "suppressSystemShortcut": false,
  "launchers": [
    {
      "keyIndex": 0,
      "name": "Terminal",
      "exec": "open -a Terminal"
    },
    {
      "keyIndex": 1,
      "name": "Screenshot",
      "exec": "",
      "icon": "sf:camera",
      "keyboardShortcut": "cmd+shift+x"
    }
  ]
}
```

`keyIndex` is zero-based across pages. The example assigns the first two keys, `1` and `2`. The shortcut action invokes whatever that combination is configured to do on your Mac; it does not install a screenshot tool. An empty `shortcut` disables combination activation; omit `doubleTapKey` or set it to `null` to disable double-tap activation.

There is no cloud sync or automatic update mechanism. macOS and Windows commands, application paths, icons, and system settings are platform-specific; similar JSON fields do not make configurations directly portable.

## Checks and source layout

```sh
# Layout, binding persistence, double-tap, window behavior, recording,
# symbol selection, and localization checks.
bash mac/test.sh

# After installing and launching: verify only one app process is running.
bash mac/Tests/InstalledAppCheck.sh
```

| Path | Contents |
| --- | --- |
| `mac/Sources/KeyboardLauncher/` | Native macOS application |
| `mac/Resources/` | English and Simplified Chinese strings |
| `mac/Tests/` | Runnable regression checks |
| `mac/assets/branding/` | App and menu bar icon source assets |
| `windows/` | Independent Windows implementation |

Automated checks do not replace hands-on verification of permissions, multiple displays, keyboard-sharing tools, or other applications' global shortcuts.

The macOS implementation started from [Launchpick](https://github.com/scorredoira/launchpick) and has been adapted into the keyboard-panel workflow described above.

## Release version

Edit the root `app.json` file to set the shared macOS and Windows display name and version. Both build pipelines read it; Windows settings and installer metadata use the same version.
