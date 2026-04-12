# Unity Fullscreen Play - Code Review

**Date:** 2026-04-11
**Scope:** Full project review + competitive analysis against existing solutions documented in DOCUMENTATION.md

---

## Overall Assessment

The project is well-structured and solid. Architecture decisions (new window vs. reposition, ShowPopup vs. ContainerWindow, EditorPrefs vs. ScriptableObject) are sound and well-justified. No critical or show-stopping bugs were found. The code demonstrates careful attention to edge cases, clean lifecycle management, and forward compatibility. The issues below are correctness and trust problems, not architectural ones.

---

## Table of Contents

1. [Remaining Bugs & Code Issues](#remaining-bugs--code-issues)
2. [Documentation Inaccuracies](#documentation-inaccuracies)
3. [Feature Gaps vs. Competitors](#feature-gaps-vs-competitors)
4. [What's Done Well](#whats-done-well)
5. [Recommended Priority Order](#recommended-priority-order)

---

## Fixed (2026-04-11)

The following issues from the original review have been resolved:

- **JSON parser `\uXXXX` decoding** — replaced chained `.Replace()` with proper character-by-character unescape handling all standard JSON escapes (`2dda83a`)
- **F11 modifier key conflict** — added `!e.shift && !e.control && !e.alt && !e.command` guard so Ctrl+Shift+F11 no longer also toggles fullscreen (`23c1359`)
- **CopyGameViewSettings arbitrary source** — now captures source GameView before creating clone, preferring `EditorWindow.focusedWindow` if it is a GameView (`f78c696`)
- **PackageUpdater version comparison** — compares installed vs. resolved version, shows "Already up to date" when they match (`ad120af`)
- **PackageUpdater concurrent call guard** — returns early if a list or add request is already in progress (`d776d21`)

---

## Remaining Bugs & Code Issues

### Important

**1. PackageUpdater installs unreleased HEAD instead of the latest tagged release**
`Editor/PackageUpdater.cs` line 15

The Git URL (`https://github.com/Shilo/unity-fullscreen-play.git`) has no `#tag` suffix. `Client.Add()` resolves to HEAD of the default branch, which may contain unreleased commits. Users can silently install unreleased code while `package.json` still reports the old version string. This breaks the release model described in DOCUMENTATION.md.

The version comparison fix (above) mitigates the UX problem — users now see "Already up to date" when versions match — but the underlying issue remains: the updater fetches HEAD, not the latest tagged release.

Fix: resolve the latest release tag (e.g., via the GitHub API or by maintaining a `latest` tag) and install `https://github.com/Shilo/unity-fullscreen-play.git#vX.Y.Z`.

**2. Orphan detection uses fragile position heuristic**
`Editor/FullscreenGameView.cs` lines 178-195

`CloseOrphanedFullscreenWindows()` identifies orphans by `pos.x == 0 && pos.y == 0 && pos.width >= screenW`. This could false-positive with a maximized editor window at position (0,0) on the primary monitor. It could also false-negative on multi-monitor setups where the fullscreen popup was on a non-primary monitor (coordinates would not start at 0,0).

Fix: use the `titleContent.text` value "FullscreenPlayPopup" (already set on line 69 for Win32 handle lookup) as the orphan signature. Set it unconditionally on all platforms and match on it during recovery.

### Minor

**3. Several empty catch blocks swallow diagnostic information**
`Editor/FullscreenGameView.cs` line 232, `Editor/FullscreenPlayController.cs` line 229, `Editor/I18n.cs` lines 68-69 and 97

Multiple catch blocks are completely empty (`catch { }`). While the "non-critical, carry on" philosophy is correct for an editor extension, adding `Debug.LogWarning` with the `[Fullscreen Play]` prefix (matching the existing pattern elsewhere) would help diagnose user-reported issues.

**4. No automated tests**

The package relies heavily on internal Unity reflection and OS/windowing behavior. There is no editor test coverage in the repository. Reflection regressions are easy to introduce and hard to catch, especially across Unity updates.

At minimum, add editor tests around non-runtime logic: settings persistence round-tripping, i18n loading and fallback behavior, orphan detection heuristics, and graceful no-op behavior when reflection members are missing. If full editor automation is too expensive, a documented manual smoke checklist for release testing would help.

---

## Documentation Inaccuracies

**5. DOCUMENTATION.md describes toast as a "separate EditorWindow" but it is a VisualElement overlay**

The "Decision 5: Toast as Separate Window" section describes implementing the toast as a separate popup EditorWindow. The actual implementation uses `window.rootVisualElement.Add(_root)` (`Editor/FullscreenToast.cs` line 159), making it a VisualElement overlay on the fullscreen GameView, not a separate native window. The mention of `BringWindowToTop()` for the toast is also incorrect for the current implementation. The current approach is actually cleaner than the documented one.

**6. CHANGELOG.md has everything under [Unreleased] despite being at v0.4.0**

The changelog has a single `[Unreleased]` section with all changes since 0.1.0, but `package.json` is at version 0.4.0. Versions 0.2.0 through 0.4.0 have no individual changelog entries. All changes are lumped together.

**7. README version pin example is outdated**

The README (line 46) shows `#v0.1.0` as the version pin example, but the package is at 0.4.0. Should reference the current or latest version.

**8. CHANGELOG 0.1.0 references a GameView dropdown entry that no longer exists**

The 0.1.0 changelog says "Play Fullscreen option in the GameView play-mode dropdown (alongside Play Focused / Maximized / Unfocused)." The [Unreleased] section says "Removed: Edit > Fullscreen Play menu entries." The current code only exposes `Tools/Fullscreen Play` menu items plus shortcuts. If the GameView dropdown entry was intentionally removed, that's a discoverability regression worth considering. The GameView dropdown is where users most naturally look for play mode options.

**9. Release workflow does not match DOCUMENTATION.md description**

DOCUMENTATION.md says the workflow supports "version bump type selection (patch/minor/major)" via `workflow_dispatch`. The actual workflow has no inputs and always bumps minor. Minor also incorrectly rolls over to major at 9 (0.12.0 is valid semver). No support for patch-only or major-only bumps.

---

## Feature Gaps vs. Competitors

### High Priority

Features that paid/free alternatives offer and users commonly expect:

| Gap | Who Has It | Details |
|-----|-----------|---------|
| **Multi-monitor support** | Fullscreen Editor, Fullscreen Anything | Highest-impact gap. The current implementation always targets the primary monitor at (0,0) via `Screen.currentResolution`. Multi-monitor setups are extremely common among Unity developers. Could use `Display.displays` or `EditorWindow.position` of the source GameView to determine the correct screen, or add a "Target Monitor" dropdown in Settings. |
| **Fullscreen outside Play mode** | fnuecke's gist, Fullscreen Editor | The current implementation guards `EnterFullscreen()` with an `isPlaying` check (line 37). Users may want fullscreen for layout inspection, screenshot capture, or presentation without entering Play mode. The fnuecke gist does not have this restriction. |
| **GameView-local entry point** | Fullscreen Editor, initial v0.1.0 | The 0.1.0 release had a GameView play-mode dropdown entry. This was removed in favor of the Tools menu. A GameView-local entry point (dropdown or overlay button) is the most natural place users look for play mode options. Restoring this would improve discoverability without changing the core architecture. Unity 6's Overlay Toolbar API could be used here. |

### Medium Priority

| Gap | Details |
|-----|---------|
| **Fullscreen any editor window** | Fullscreen Editor ($15) and Fullscreen Anything support fullscreening Scene view, Inspector, or any editor window. This is a different product scope — this project is focused on play mode testing — but adding Scene view support alone would increase competitiveness. |
| **Toast doesn't reflect rebound shortcuts** | Keycap labels hardcode "F11" and "Esc". If the user rebinds the shortcut via Edit > Shortcuts, the toast still shows "F11". Should query the Shortcut Manager for the current binding and display the actual key. |
| **No re-enter fullscreen after domain reload** | If scripts recompile during play mode while fullscreen is active, the user must manually re-enter fullscreen. Could auto-restore via an EditorPrefs flag, opt-in via a setting. |
| **Per-project settings option** | All settings are in EditorPrefs (per-machine). Teams cannot enforce consistent fullscreen behavior across developers. A `ProjectSettings` scope alongside the existing `Preferences` scope could address this. |
| **No macOS/Linux native integration** | Win32 P/Invoke code for taskbar coverage has no macOS (Cocoa/NSWindow) or Linux (X11/Wayland) equivalents. On macOS, the dock may not be fully covered. On Linux, behavior varies by window manager. |

### Lower Priority

| Gap | Details |
|-----|---------|
| **Click to dismiss toast** | The toast is non-interactive (`pickingMode = PickingMode.Ignore`). Users might expect to click the toast to dismiss it early. |
| **Compatibility discipline across Unity versions** | The paid Fullscreen Editor product maintains compatibility across many Unity versions. If broad version support is a goal, a compatibility test matrix and policy would help. If Unity 6+ only is the intended scope, that should be stated more prominently. |

### Not recommended near-term

| Item | Reasoning |
|------|-----------|
| **Exclusive fullscreen** | Already correctly deferred in DOCUMENTATION.md. High risk in editor context — if Unity crashes mid-session the display stays at the changed resolution. Should stay behind safer priorities. |

---

## What's Done Well

- **Non-destructive architecture** — Creating a separate GameView popup rather than modifying the existing one is the correct decision. The user's editor layout is never at risk.

- **State copying breadth** — The fullscreen view copies significantly more GameView state than any open-source reference: Gizmos, Stats, VSync, low-resolution rendering, XR render mode, and camera warning state. This is a clear advantage over the gist-based solutions.

- **Reflection safety** — Every reflection call is wrapped in null checks and try/catch with graceful degradation. The `CopyProperty` and `CopyField` helpers are clean and consistent.

- **Lifecycle management** — The `beforeAssemblyReload` + `[InitializeOnLoad]` two-phase cleanup is thorough. The `OnWantsToQuit` handler for Cmd+Q/Ctrl+Q is a smart catch.

- **Crash recovery** — The triple-fallback approach (immediate + update loop + delayCall) for orphan cleanup is well-engineered and accounts for timing differences in Unity's startup sequence.

- **Win32 integration** — Using `FindWindow` by title instead of `GetForegroundWindow` to find the HWND avoids race conditions. Using `HWND_TOP` instead of `HWND_TOPMOST` is the correct choice for alt-tab friendliness.

- **Code organization** — Clear single-responsibility separation across 6 files. Each file has a well-defined role.

- **Technical documentation** — DOCUMENTATION.md is exceptionally thorough, documenting not just what was built but why each decision was made, what alternatives were considered, and what the known limitations are.

---

## Recommended Priority Order

### Must Fix (correctness)

1. PackageUpdater should install tagged releases, not bare HEAD
2. Orphan detection should use window title instead of position heuristic (deferred — handling separately to avoid regression)

### Should Fix (quality)

3. Update DOCUMENTATION.md — correct toast description, release workflow description
4. Fix CHANGELOG.md — add proper version sections for 0.2.0 through 0.4.0
5. Update README version pin example to current version
6. Make toast keycap labels reflect actual shortcut bindings from the Shortcut Manager
7. Add lightweight editor tests or a documented release smoke checklist

### Feature Additions (competitiveness)

8. Multi-monitor support (biggest competitive gap)
9. Restore a GameView-local entry point (dropdown or overlay button)
10. Allow fullscreen outside Play mode
11. Auto-restore fullscreen after domain reload during Play mode
