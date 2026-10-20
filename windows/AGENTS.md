# Windows local deployment

- Keep a real tray icon in development and smoke tests.
- All unpackaged local builds must run from `%LOCALAPPDATA%\Programs\KeyboardLauncher\KeyboardLauncher.exe`. Use `windows/build.ps1 -Run` or deploy existing output with `windows/deploy-local.ps1 -SourceDirectory ... -LaunchArguments ...`.
- Do not launch test builds from temporary, versioned, architecture-specific, or repository output directories. The non-interactive `--installation-check` and tray-free internal `--launch-probe` are the only exceptions.
- Preserve `LocalInstallation.TrayIconGuid` across every version and architecture. Do not bypass the installation-path guard or fall back to a path-dependent tray identity.
- Keep configuration in `%LOCALAPPDATA%\KeyboardLauncher\config.json`. Do not overwrite configuration during deployment. Local login startup must target `LocalInstallation.StartupCommand`.
- Local deployment must preserve the existing startup enabled/disabled state and installer files. MSIX uses its own package-managed deployment and startup; do not copy local test builds into a package directory.
- Switching a local build ends the current local instance; save editing work before deploying. Run interactive smoke tests without concurrent keyboard/mouse input.
