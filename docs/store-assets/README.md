# Microsoft Store artwork

## Saved to Partner Center — 2026-09-24

Product `9N5W5NWFBWNM`, submission `1152921505701964431`.

- Chinese and English listings each contain **5 desktop feature posters**, with localized screenshot captions. Both pages showed Desktop (5) before Save and returned to the product overview after saving.
- `gallery/zh-CN-01-launcher.png` through `gallery/zh-CN-05-system.png`: Chinese gallery.
- `gallery/en-US-01-launcher.png` through `gallery/en-US-05-system.png`: English gallery.
- All ten files are valid 1920 × 1080 PNGs. They combine real localized Windows app captures with black/silver framing, headlines, feature copy and decorative keycaps, as explicitly requested by the user.
- `poster-zh-CN-1440x2160.png` and `poster-en-US-1440x2160.png` are saved in the corresponding poster-art slots. These resize the user-approved original black/silver `poster-*.png` files (1024 × 1536).
- The rejected ice-blue hero was removed from both listing drafts. `gallery/hero-*` remains local only and is not the selected artwork.

## Sources

`screenshots/` contains ten native app-only captures using temporary demonstration bindings. Capture was explicitly authorized by the user. The original Windows configuration and its backup were restored and the app restarted after capture. Temporary scheduled tasks were removed, and VM idle-pause restored.

`gallery/screenshots.html` and `gallery/captions.json` contain the layout and bilingual copy. `gallery/contact-sheet.html` previews all ten final images. Serve the repository root locally to render the source template; final PNGs are standalone.

## Submission status

Artwork upload and draft saving are complete; certification has **not** been submitted. On 2026-09-24, the current source was rebuilt as 1.0.2.0 ARM64/x64, all 16 checks passed, and both MSIX packages were uploaded for replacement. See `windows/artifacts/store/build-source-sha256.json` for source hashes. IARC terms/age confirmation, other incomplete submission sections and the privacy-policy review remain separate from this artwork task.

## Local refresh — 2026-09-24

The six Chinese/English Settings captures and matching gallery images were regenerated with the neutral gray Quit icon and the double-tap recorder. Settings screenshots occupy more of the gallery canvas for readability. These replacements have not been uploaded to Partner Center.

`gallery/zh-CN-00-wide-poster.png` is the new landscape adaptation of the supplied black/silver portrait poster, generated using the built-in imagegen tool. Actual output: 1672 × 941. Prompt: preserve the original monochrome style, silver arrow keycap, glowing orbit and exact Chinese copy; recompose with the headline on the left and keycap on the right in a wide landscape layout, with crisp text and safe margins. The requested 3840 × 2160 resolution was not returned by the tool; this file is its native output, without upscaling.
