# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Quit-shortcut interception — Cmd+Q (macOS), Ctrl+Q (Linux) exit fullscreen instead of quitting Unity
- Crash recovery — orphaned fullscreen windows are automatically cleaned up on editor restart

### Fixed

- JSON parser now decodes `\uXXXX` unicode escapes (fixes garbled text in locale files)
- F11 global handler no longer fires when modifier keys are held (fixes conflict with Ctrl+Shift+F11)
- CopyGameViewSettings now prefers the focused GameView when multiple Game tabs exist
- PackageUpdater shows "Already up to date" when versions match
- PackageUpdater rejects concurrent invocations

## [0.4.0] - 2026-04-10

### Added

- Copy Gizmos visibility state to fullscreen GameView
- Copy VSync, Stats, Low Resolution Aspect Ratios, XR render mode, and No Camera Warning to fullscreen GameView
- CopyProperty/CopyField helpers for cleaner reflection code

## [0.3.0] - 2026-04-10

### Removed

- GameView toolbar dropdown injection (reverted after testing)
- GameView play-mode dropdown entry (moved to Tools menu only)

## [0.2.0] - 2026-04-09

### Added

- Tools > Fullscreen Play menu (standard third-party plugin location) with Auto-Fullscreen on Play (Ctrl+Shift+F11), Toggle Fullscreen (F11), Check for Update, and Settings
- Check for Update menu item — one-click update check and install via UPM Client API
- Material Design 3 styled toast with flat dark theme and rounded keycap badges
- Internationalization (i18n) — all UI strings localized, English and German included, extensible via JSON files in `Editor/Locales/`

### Changed

- Toggle Fullscreen menu items are greyed out when not in Play mode
- "Auto-Fullscreen on Play" is now the first item in the Tools menu

### Removed

- Edit > Fullscreen Play menu entries (Tools menu is now the sole menu location)

### Fixed

- Fullscreen window no longer pins above all windows permanently (alt-tab now works)
- Toast notification now appears above the fullscreen window on Windows
- Toast now stays visible for the full configured duration
- F11 hotkey now works reliably during play mode (handled via globalEventHandler in addition to Shortcuts API)

## [0.1.0] - 2026-04-09

### Added

- Initial release
- Play Fullscreen option via Edit > Fullscreen Play menu
- F11 hotkey to toggle fullscreen (rebindable via Edit > Shortcuts)
- Esc to exit fullscreen without stopping Play
- Toast notification showing exit instructions (configurable fade duration)
- Settings panel in Edit > Preferences > Fullscreen Play
- Fullscreen Windowed mode (Exclusive Fullscreen planned for a future release)
- Clean assembly reload and package enable/disable lifecycle
