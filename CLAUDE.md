## Unity Fullscreen Play

Unity Editor package that adds true fullscreen Play mode. Creates a borderless popup GameView window covering the entire screen without modifying the user's editor layout. Editor-only with zero runtime footprint, installed via UPM Git URL.

## Architecture

Creates a second GameView instance via ShowPopup() (public API) rather than repositioning the existing one. Original Game tab is never touched — cleanup is just closing the popup.

On Windows, Win32 P/Invoke (SetWindowPos, SetWindowLong) strips window chrome and covers the taskbar. Uses HWND_TOP (not HWND_TOPMOST) so alt-tab works normally.

The toolbar dropdown overlays an opaque IMGUIContainer on top of Unity's built-in EnumPopup dropdown since the internal enum can't be extended. Silently disables itself if internals change.

F11 uses both the [Shortcut] API (discoverability) and globalEventHandler reflection (reliability during play). Escape uses only globalEventHandler to avoid conflicting with games.

Domain reload safety: beforeAssemblyReload unhooks delegates, closes popups, removes overlays. [InitializeOnLoad] re-initializes everything in the new domain.

Settings are stored in EditorPrefs with the FullscreenPlay. prefix (per-user, not per-project).

Localization is file-based i18n via JSON files in Editor/Locales/, extensible by adding new locale files.

## Further Reading

Full technical deep-dive: DOCUMENTATION.md
User-facing docs and installation: README.md
Version history: CHANGELOG.md
Release automation: .github/workflows/release.yml
UPM manifest: package.json
Locale strings: Editor/Locales/en.json, Editor/Locales/de.json

## Constraints

All code is editor-only (includePlatforms: Editor in asmdef). No Runtime/ folder.

Reflection is used to access internal GameView type, showToolbar, targetDisplay, selectedSizeIndex, and globalEventHandler. Each access is wrapped in try/catch for forward compatibility.

ExclusiveFullscreen mode exists as an enum value but is deliberately unimplemented (risk of display resolution corruption on editor crash).

Multi-monitor support is not yet implemented — targets primary monitor only.

Platform-specific code is behind UNITY_EDITOR_WIN preprocessor directive.
