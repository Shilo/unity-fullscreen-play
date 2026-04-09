# Code Review: Unity Fullscreen Play

## Overview

A well-crafted Unity Editor package (~790 LOC across 5 files) that adds true fullscreen play-mode testing. The implementation matches the README and DOCUMENTATION specs closely, with a conservative, safety-first architecture.

---

## Correctness

### Strong points

- The new-window-via-`ShowPopup()` approach is exactly as documented — non-destructive, original GameView untouched
- Domain reload lifecycle is thorough: `beforeAssemblyReload` cleans up delegates, overlays, and windows; `[InitializeOnLoad]` re-initializes cleanly
- Every reflection access is individually wrapped in try/catch with graceful fallbacks
- DPI scaling math is correct: divides physical pixels by `pixelsPerPoint` for logical coordinates, multiplies back for Win32 calls

### Issues found

1. **`FullscreenMode` setting is never read** — `FullscreenPlaySettings.cs:8-9` defines `FullscreenMode.ExclusiveFullscreen` and the preferences UI exposes it, but `FullscreenGameView.cs` never checks `FullscreenPlaySettings.Mode`. The setting is dead code. The documentation acknowledges this ("not yet implemented in v0.1") but the UI lets users select it with no effect, which is misleading.

   **Suggestion:** Either disable the `ExclusiveFullscreen` option in the dropdown with a note, or remove it entirely until implemented.

2. **`GetForegroundWindow()` is fragile for window handle acquisition** — `FullscreenGameView.cs:233` assumes the foreground window after `delayCall` is the fullscreen popup. If another window steals focus in the interim (notification, another editor window), the wrong HWND gets `TOPMOST` + `WS_POPUP` styling applied. Consider using the `EditorWindow`'s native handle instead (via `GetForegroundWindow` right after `Focus()` in the same frame, or via the window's `nativeHandle` if available).

3. **Win32 original style saved but never restored** — `FullscreenGameView.cs:237` saves `s_OriginalStyle` but `RestoreWindow()` never calls `SetWindowLong(s_WindowHandle, GWL_STYLE, s_OriginalStyle)`. Since the window is being closed anyway this is benign, but the dead `s_OriginalStyle` field is misleading.

4. **`CornerRadius` constant unused** — `FullscreenToast.cs:22` declares `CornerRadius = 8f` but `DrawBorder` draws straight rectangles. Either implement rounded corners or remove the constant.

---

## Code Quality & Style

- Consistent namespace, naming, and access modifiers throughout
- Good use of `internal` for all types — nothing leaks to consuming assemblies
- `WeakReference` in the toolbar injector overlay callback prevents preventing GC of closed windows
- `s_ScanScheduled` debounce flag is a clean pattern for collapsing rapid event triggers

### Minor nits

- The nested `delayCall += () => { delayCall += () => { ... } }` chain in `FullscreenGameView.cs:69-93` is 3 levels deep. A coroutine-like helper or a single delayed method with a step counter would be clearer.
- Empty `catch` blocks throughout `GameViewToolbarInjector.cs` are intentional per the forward-compatibility design, but a conditional `#if DEBUG` log would help during development.

---

## Security & Safety

- No security concerns — editor-only code with no network, file I/O, or user data handling
- Win32 P/Invoke is correctly isolated behind `#if UNITY_EDITOR_WIN`
- `allowUnsafeCode: false` in the asmdef is appropriate
- Assembly reload cleanup is comprehensive and prevents stale delegate issues

---

## Documentation Alignment

The code matches the DOCUMENTATION.md spec almost exactly. Two discrepancies:

1. **DOCUMENTATION.md line 499** claims "~750 lines of code total" — actual total is ~790 lines. Minor, but worth updating.
2. **DOCUMENTATION.md describes `ExclusiveFullscreen`** as a future feature, but the UI exposes it today with no guard. The code and docs should agree on whether users can see this option.

---

## Summary

| Area | Rating |
|------|--------|
| Architecture | Excellent — conservative, non-destructive design |
| Correctness | Good — one dead setting, one fragile HWND pattern |
| Code quality | Very good — clean, consistent, well-structured |
| Safety/cleanup | Excellent — thorough domain reload handling |
| Doc alignment | Good — minor drift from spec |

### Top 3 action items

1. Guard or hide the `ExclusiveFullscreen` option until implemented
2. Harden the Win32 window handle acquisition (avoid `GetForegroundWindow` race)
3. Remove unused `s_OriginalStyle` field and `CornerRadius` constant
