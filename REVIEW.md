# Unity Fullscreen Play - Code Review

**Date:** 2026-04-11
**Scope:** Full project review + competitive analysis against existing solutions documented in DOCUMENTATION.md

---

## Overall Assessment

The project is well-structured and solid. Architecture decisions (new window vs. reposition, ShowPopup vs. ContainerWindow, EditorPrefs vs. ScriptableObject) are sound and well-justified. No critical or show-stopping bugs were found. The code demonstrates careful attention to edge cases, clean lifecycle management, and forward compatibility.

---

## Fixed (2026-04-11)

- **JSON parser `\uXXXX` decoding** — replaced chained `.Replace()` with proper character-by-character unescape handling all standard JSON escapes (`2dda83a`)
- **F11 modifier key conflict** — added `!e.shift && !e.control && !e.alt && !e.command` guard so Ctrl+Shift+F11 no longer also toggles fullscreen (`23c1359`)
- **CopyGameViewSettings arbitrary source** — now captures source GameView before creating clone, preferring `EditorWindow.focusedWindow` if it is a GameView (`f78c696`)
- **PackageUpdater version comparison** — compares installed vs. resolved version, shows "Already up to date" when they match (`ad120af`)
- **PackageUpdater concurrent call guard** — returns early if a list or add request is already in progress (`d776d21`)
- **DOCUMENTATION.md toast description** — updated from "separate EditorWindow" to VisualElement overlay, removed stale `BringWindowToTop()` reference (`b62a0e1`)
- **DOCUMENTATION.md release workflow description** — corrected from "patch/minor/major selection" to minor-only bumps (`b62a0e1`)
- **CHANGELOG.md version sections** — split [Unreleased] into proper 0.2.0, 0.3.0, 0.4.0 sections based on git tag history (`b62a0e1`)
- **CHANGELOG.md 0.1.0 GameView dropdown reference** — corrected; that feature was added in 0.2.0 and removed in 0.3.0 (`b62a0e1`)
- **README version pin example** — updated from `#v0.1.0` to `#v0.4.0` (`b62a0e1`)

---

## Remaining Issues

**1. Orphan detection uses fragile position heuristic** (deferred — handling separately to avoid regression)
`Editor/FullscreenGameView.cs` lines 178-195

`CloseOrphanedFullscreenWindows()` identifies orphans by `pos.x == 0 && pos.y == 0 && pos.width >= screenW`. This could false-positive with a maximized editor window at position (0,0) on the primary monitor. It could also false-negative on multi-monitor setups.

Fix: use the `titleContent.text` value "FullscreenPlayPopup" (already set on line 69 for Win32 handle lookup) as the orphan signature. Set it unconditionally on all platforms and match on it during recovery.

**2. PackageUpdater installs HEAD instead of the latest tagged release**
`Editor/PackageUpdater.cs` line 15

The Git URL has no `#tag` suffix, so `Client.Add()` resolves to HEAD. Users can silently install unreleased commits. The version comparison fix mitigates the UX (shows "Already up to date" when versions match), but the underlying issue remains. Fix requires resolving the latest release tag at runtime (GitHub API or maintaining a `latest` tag).

---

## Feature Gaps vs. Competitors

### High Priority

| Gap | Who Has It | Details |
|-----|-----------|---------|
| **Multi-monitor support** | Fullscreen Editor, Fullscreen Anything | Highest-impact gap. Always targets primary monitor at (0,0). Could use `Display.displays` or `EditorWindow.position` to determine the correct screen. |
| **Fullscreen outside Play mode** | fnuecke's gist, Fullscreen Editor | `EnterFullscreen()` guards with `isPlaying` check. Users may want fullscreen for layout inspection or screenshots. |
| **GameView-local entry point** | Fullscreen Editor, initial v0.1.0 | A GameView-local entry point (dropdown or overlay button) is the most natural place users look for play mode options. Unity 6's Overlay Toolbar API could be used. |

### Medium Priority

| Gap | Details |
|-----|---------|
| **Fullscreen any editor window** | Fullscreen Editor ($15) and Fullscreen Anything support this. Different product scope, but Scene view support alone would help. |
| **Toast doesn't reflect rebound shortcuts** | Keycap labels hardcode "F11" and "Esc". Should query the Shortcut Manager for the actual binding. |
| **No re-enter fullscreen after domain reload** | Could auto-restore via an EditorPrefs flag, opt-in via a setting. |
| **Per-project settings option** | All settings are EditorPrefs (per-machine). Teams cannot enforce consistent behavior. |
| **No macOS/Linux native integration** | No Cocoa/NSWindow or X11/Wayland equivalents for taskbar coverage. |

### Lower Priority

| Gap | Details |
|-----|---------|
| **Click to dismiss toast** | Toast is non-interactive. Users might expect to click to dismiss. |
| **Compatibility discipline across Unity versions** | A test matrix would help if broad version support is a goal. |

### Not recommended near-term

| Item | Reasoning |
|------|-----------|
| **Exclusive fullscreen** | High risk in editor context. Correctly deferred. |

---

## What's Done Well

- **Non-destructive architecture** — separate GameView popup, editor layout never at risk
- **State copying breadth** — copies more GameView state than any open-source reference (Gizmos, Stats, VSync, Low Res, XR, No Camera Warning)
- **Reflection safety** — every reflection call wrapped in null checks and try/catch
- **Lifecycle management** — `beforeAssemblyReload` + `[InitializeOnLoad]` two-phase cleanup
- **Crash recovery** — triple-fallback orphan cleanup (immediate + update loop + delayCall)
- **Win32 integration** — `FindWindow` by title, `HWND_TOP` not `HWND_TOPMOST`
- **Code organization** — clear single-responsibility separation across 6 files
- **Technical documentation** — exceptionally thorough DOCUMENTATION.md
