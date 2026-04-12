# Roadmap

Feature gaps identified by comparing against existing solutions (Fullscreen Editor, Fullscreen Anything, fnuecke's gist) documented in DOCUMENTATION.md. Review date: 2026-04-11.

## High Priority

### Multi-monitor support
**Who has it:** Fullscreen Editor, Fullscreen Anything

Currently always fullscreens on the primary monitor at (0,0) via `Screen.currentResolution`. Multi-monitor setups are extremely common among Unity developers. Could use `Display.displays` or `EditorWindow.position` of the source GameView to determine the correct screen, or add a "Target Monitor" dropdown in Settings.

### Fullscreen outside Play mode
**Who has it:** fnuecke's gist, Fullscreen Editor

`EnterFullscreen()` guards with an `isPlaying` check. Users may want fullscreen for layout inspection, screenshot capture, or presentation without entering Play mode.

### GameView-local entry point
**Who has it:** Fullscreen Editor, initial v0.1.0

A GameView-local entry point (dropdown or overlay button) is the most natural place users look for play mode options. The v0.1.0 release had a GameView play-mode dropdown entry; it was removed in v0.3.0. Restoring this would improve discoverability without changing the core architecture. Unity 6's Overlay Toolbar API could be used here.

## Medium Priority

### Fullscreen any editor window
**Who has it:** Fullscreen Editor ($15), Fullscreen Anything

Both support fullscreening Scene view, Inspector, or any editor window. This is a different product scope — this project is focused on play mode testing — but adding Scene view support alone would increase competitiveness.

### Toast should reflect rebound shortcuts
Keycap labels hardcode "F11" and "Esc". If the user rebinds the shortcut via Edit > Shortcuts, the toast still shows "F11". Should query the Shortcut Manager for the current binding and display the actual key.

### Auto re-enter fullscreen after domain reload
If scripts recompile during play mode while fullscreen is active, the user must manually re-enter fullscreen. Could auto-restore via an EditorPrefs flag, opt-in via a setting.

### Per-project settings option
All settings are in EditorPrefs (per-machine). Teams cannot enforce consistent fullscreen behavior across developers. A `ProjectSettings` scope alongside the existing `Preferences` scope could address this.

### macOS/Linux native integration
Win32 P/Invoke code for taskbar coverage has no macOS (Cocoa/NSWindow) or Linux (X11/Wayland) equivalents. On macOS, the dock may not be fully covered. On Linux, behavior varies by window manager.

## Lower Priority

### Click to dismiss toast
The toast is non-interactive (`pickingMode = PickingMode.Ignore`). Users might expect to click the toast to dismiss it early.

### Compatibility discipline across Unity versions
The paid Fullscreen Editor product maintains compatibility across many Unity versions. If broad version support is a goal, a compatibility test matrix and policy would help. If Unity 6+ only is the intended scope, that should be stated more prominently.

## Not recommended near-term

### Exclusive fullscreen
Already correctly deferred in DOCUMENTATION.md. High risk in editor context — if Unity crashes mid-session the display stays at the changed resolution. Should stay behind safer priorities.
