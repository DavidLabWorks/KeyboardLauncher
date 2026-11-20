# Keyboard Launcher design system

## Overview

This document governs the Windows WinUI 3 interface. The macOS implementation retains its own native visual conventions. Windows is an everyday keyboard utility: rapid scanning, predictable controls, and precise alignment take priority over decorative effects. Preserve the keyboard-shaped launcher, native acrylic backdrop, and existing app artwork.

This pass follows Impeccable 4.7.0 (upstream revision `374a0e179bb36725283a612462131705ba4780ae`), adapted to Windows. Web-only styling and browser detectors do not define WinUI behavior.

## Colors

Use WinUI semantic theme resources for settings surfaces and text:

- Window body: `SolidBackgroundFillColorBaseBrush`.
- Sidebar: `LayerFillColorDefaultBrush`.
- Group surface and outline: `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`.
- Dividers: `DividerStrokeColorDefaultBrush`.
- Supporting text: `TextFillColorSecondaryBrush`.
- Accent: `AccentFillColorDefaultBrush`, shared by the native Switch, selected segments, and recording feedback. On-accent text uses `TextOnAccentFillColorPrimaryBrush`; do not force white text in both themes.

Support Light, Dark, and System. Do not hard-code a separate blue accent or use unrelated category colors in navigation. Keep the launcher's existing `SurfaceStrokeColorDefaultBrush` outline subtle in both themes.

## Typography

Use the native WinUI font family; retain Chinese fallback. Page titles are 24 DIP semibold, setting names 14 DIP medium, descriptions and actions 13 DIP. Launcher captions and short footer hints are 12 DIP. Long setting descriptions wrap, launcher captions truncate. User-authored names and icons are content, not a typography palette to replace.

## Layout

Use a 4-DIP base rhythm: 8 within action groups, 12 within icon-label pairs, 20 within settings groups, 24 between groups, and 28 at the content edge. Settings have a 220-DIP sidebar, a centered content column capped at 740 DIP, and a minimum window of 880 by 560 DIP. Content scrolls vertically at the minimum size and remains centered when maximized.

Rows use a flexible text column and a right-aligned control column with 24 DIP separation. Related shortcut recorders are both 228 DIP wide. Both Clear buttons have a 64-DIP minimum width and 44-DIP minimum height. Do not size recorders to their current value. Keep controls and text aligned even when localized descriptions wrap.

## Elevation & Depth

Settings groups use one subtle border, without a decorative shadow. The sidebar uses a neutral tonal layer. Preserve native focus indicators and the launcher's existing acrylic material, key depth, and desktop clipping. Do not introduce glowing borders, gradients, loading animations, or new effects without a state that needs them.

## Shapes

Settings groups and sidebar: 12-DIP corners. Navigation and ordinary buttons: 8 DIP. Shortcut recorders: 10 DIP; internal keycaps: 4 DIP. Launcher keycaps: existing 16 DIP; panel: existing 26 DIP.

## Components

- Navigation: a single Fluent glyph family, 18-DIP icons in a 24-DIP slot, a 44-DIP minimum row, neutral selection fill, visible keyboard focus. Avoid colored icon tiles.
- Sidebar footer: quiet 12-DIP Exit action with a 14-DIP Fluent sign-out glyph, native hover/focus, and a single-line 11-DIP brand caption. Align the icon slot with navigation and omit the decorative divider.
- Settings buttons: native Button template and theme states; 36-DIP minimum height, except shortcut actions at 44 DIP.
- Shortcut recorders: keyboard glyph plus individual keycaps, fixed shared width. A click starts capture, release saves, Escape or focus loss cancels, and 15 seconds without input stops capture. Merely focusing a recorder never starts it. Labels and tooltips remain accessible.
- Segmented choices: shared system accent, 32-DIP visible segments, keyboard focus and exclusive selection retained.
- Binding editor: reuse `Ui.Card`, `Ui.SecondaryText`, and `Ui.ActionButton`; 12-DIP card corners, 20-DIP insets, native Save/Cancel states. Action selection and the edit-icon badge use the same system accent as settings. Keep the footer fixed while the form scrolls.
- Action shortcut recording: share the keycap recorder presentation; plain keys and Escape remain valid actions. Click again or leave the control to cancel. Values remain a draft until Save; Cancel closes without changing configuration.
- Application and icon pickers: share `Ui.PickerStyle`, 12-DIP corners, 16-DIP insets and native flyout outline. Search at 13 DIP, app names at 12 DIP with up to two lines and a full-name tooltip. Show loading and no-match guidance, with Browse as a recovery path. Custom image/symbol input and automatic reset remain available.
- Launcher: preserve physical positions, bindings, drag behavior, invocation, and focus restoration. Default fallback symbols use Fluent icons; user-selected icons remain untouched.

## Do's and Don'ts

- Check actual Light and Dark windows, Chinese and English, the minimum size and maximized layout.
- Verify default, hover, focused, recording, cleared, and disabled states.
- Preserve the single installation path, tray GUID, startup setting, and config file during visual updates.
- Never remove native affordances or change shortcut handling to satisfy a web-oriented style rule.
- Compare changes with this document before adding a new local color, size, or control template.

## Interaction refinements

- Shortcut setting labels align with equal-width recorders; help spans the full card width. Show the timing hint only when double-tap and triple-tap share a physical key.
- Binding validation explains disabled Save next to the name or action. Errors update as the draft changes.
- Launcher removal has a 28-DIP target and immediately returns the key to its empty state. Full binding names are available on hover.
- Navigation selection and drag targets resolve semantic theme brushes. About shows the version once and groups licensing content without unnecessary dividers.

## Appearance baseline

Preserve the established keyboard composition, native acrylic, full rounded keycaps, artwork proportions, settings and editor layouts. Empty positions remain visible and legible. Retain keyboard/context-menu removal, accurate script terminology, accessible validation and local release links. Visual changes require a concrete reference accepted by the user before replacing the running appearance.
