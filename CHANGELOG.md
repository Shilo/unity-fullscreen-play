# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Tools > Fullscreen Play menu (standard third-party plugin location) with Auto-Fullscreen on Play, Toggle Fullscreen (Ctrl+Shift+F11), and Settings
- Edit > Fullscreen Play menu now mirrors Tools menu exactly for discoverability
- Material Design 3 styled toast with flat dark theme and anti-aliased rounded keycap badges
- Internationalization (i18n) — all UI strings localized, English and German included, extensible via JSON files in `Editor/Locales/`
- Settings panel strings (labels, tooltips, help text) are now fully localized

### Changed

- Toggle Fullscreen menu items are greyed out when not in Play mode (no more forced play start)
- "Auto-Fullscreen on Play" is now the first item in both Tools and Edit menus
- "Show on Refocus" setting moved above "Toast Duration" in Preferences

### Fixed

- Fullscreen window no longer pins above all windows permanently (alt-tab now works)
- Toast notification now appears above the fullscreen window on Windows
- Toast now stays visible for the full configured duration (was ~1 second due to wrong HWND lookup)
- F11 hotkey now works reliably during play mode (handled via globalEventHandler in addition to Shortcuts API)

## [0.1.0] - 2026-04-09

### Added

- Initial release
- Play Fullscreen option in the GameView play-mode dropdown (alongside Play Focused / Maximized / Unfocused)
- Edit > Fullscreen Play menu with toggle, instant-enter, and settings
- F11 hotkey to toggle fullscreen (rebindable via Edit > Shortcuts)
- Esc to exit fullscreen without stopping Play
- Toast notification showing exit instructions (configurable fade duration)
- Settings panel in Edit > Preferences > Fullscreen Play
- Fullscreen Windowed mode (Exclusive Fullscreen planned for a future release)
- Clean assembly reload and package enable/disable lifecycle
