## Unity Fullscreen Play

Editor-only package adding true fullscreen Play mode via a borderless popup GameView. Zero runtime footprint.

## Architecture

Second GameView via ShowPopup(), original tab untouched. Closing the popup exits fullscreen.

Win32: Editor/FullscreenGameView.cs
P/Invoke strips chrome, covers taskbar with HWND_TOP (not HWND_TOPMOST, so alt-tab works).

Toolbar dropdown: Editor/GameViewToolbarInjector.cs
IMGUIContainer overlay on Unity's EnumPopup (enum not extensible). Self-disables if internals change.

Hotkeys: Editor/FullscreenPlayController.cs
F11 via [Shortcut] API + globalEventHandler reflection. Escape via globalEventHandler only (avoids game conflicts).

Domain reload: beforeAssemblyReload cleans up delegates/popups/overlays. [InitializeOnLoad] re-initializes.

Settings: Editor/FullscreenPlaySettings.cs
EditorPrefs with FullscreenPlay. prefix, per-user.

Toast: Editor/FullscreenToast.cs
Localization: Editor/I18n.cs, Editor/Locales/

## Further Reading

Technical deep-dive: DOCUMENTATION.md
User docs and install: README.md
Changelog: CHANGELOG.md
Release workflow: .github/workflows/release.yml
UPM manifest: package.json

## Adding Files

Every new script file (or any asset) must have a corresponding .meta file with a unique GUID. Without the .meta, Unity will not compile the script. Copy an existing .meta as a template and replace the guid field with a new random 32-hex-character GUID.

## Constraints

Editor-only (includePlatforms: Editor in asmdef). No Runtime/ folder.

Reflection on internal GameView, showToolbar, targetDisplay, selectedSizeIndex, globalEventHandler. Each in try/catch.

ExclusiveFullscreen enum exists but unimplemented (resolution corruption risk on crash).

Primary monitor only. Multi-monitor not yet supported.

Platform code behind UNITY_EDITOR_WIN.
