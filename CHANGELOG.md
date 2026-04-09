# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Tools > Fullscreen Play menu with Toggle Fullscreen (Ctrl+F11), Auto-Fullscreen on Play, and Settings
- Ctrl+F11 keyboard shortcut — toggles fullscreen or starts play+fullscreen from anywhere

### Fixed

- Fullscreen window no longer pins above all windows permanently (alt-tab now works)
- Toast notification now appears above the fullscreen window on Windows
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
