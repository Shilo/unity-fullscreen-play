# Unity Fullscreen Play - Technical Documentation

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Research](#research)
   - [Why Unity Lacks Native Fullscreen Play](#why-unity-lacks-native-fullscreen-play)
   - [Existing Solutions](#existing-solutions)
   - [Technical Approaches Analyzed](#technical-approaches-analyzed)
3. [Architecture & Implementation](#architecture--implementation)
   - [Package Structure](#package-structure)
   - [Core Components](#core-components)
   - [Technical Deep Dive](#technical-deep-dive)
4. [Design Decisions](#design-decisions)
5. [Platform Support](#platform-support)
6. [Known Limitations & Edge Cases](#known-limitations--edge-cases)
7. [Release Infrastructure](#release-infrastructure)
8. [Summary](#summary)

---

## Problem Statement

Unity Editor provides three play modes for the Game view: **Play Focused**, **Play Maximized**, and **Play Unfocused**. Even "Play Maximized" only fills the Game tab within the editor layout - it does not provide a true fullscreen experience matching what the player sees in a built executable.

This is a fundamental workflow problem:

- **Building the project** to test fullscreen behavior is extremely time-consuming (minutes to hours depending on project size).
- **Visual differences** between the Game tab and true fullscreen include: visible editor chrome, incorrect aspect ratios, taskbar interference, and display scaling artifacts.
- **Input behavior** differs - fullscreen games handle cursor confinement, multi-monitor focus, and keyboard capture differently than a windowed Game tab.
- **Performance profiling** in fullscreen more accurately reflects production conditions since the GPU isn't also rendering the editor layout.

The goal: create a Unity package that adds a "Play Fullscreen" option, launching the Game view as a borderless window covering the entire screen - mimicking a built executable without the build step.

---

## Research

### Why Unity Lacks Native Fullscreen Play

Unity's editor architecture treats the Game view as just another dockable panel (`EditorWindow`). The editor's window management system is designed for a multi-panel layout, not for individual panels to break out into exclusive fullscreen. Key reasons Unity hasn't added this natively:

1. **Editor stability** - A fullscreen Game view that crashes or freezes would lock the user out of the editor entirely. Unity's conservative approach avoids this risk.
2. **Multi-window architecture** - Unity's `ContainerWindow` system (internal) manages all editor windows. Making one panel fullscreen requires bypassing this system.
3. **Cross-platform complexity** - True fullscreen behavior differs significantly across Windows, macOS, and Linux. Each platform has different APIs for taskbar hiding, window layering, and exclusive display access.
4. **Play Maximized is "good enough"** - For most workflows, Play Maximized approximates the experience. The gap only matters for resolution-sensitive UI work, performance profiling, and immersive testing.

### Existing Solutions

We analyzed several existing approaches:

#### 1. Fullscreen Editor (Asset Store - paid)
- The most established solution (~$15)
- Supports fullscreening any editor window, not just GameView
- Uses Unity's internal `ContainerWindow` API via reflection
- Maintains compatibility across many Unity versions
- Adds toolbar integration and keyboard shortcuts

#### 2. Fullscreen Editor Play Mode Free (Asset Store)
- Simplified free version focused only on play mode
- Uses the `ShowPopup()` approach
- Limited configuration options

#### 3. Fullscreen Anything (Asset Store)
- Newer entry, supports Unity 6
- Generic approach - any window can go fullscreen
- Uses overlay-based UI integration

#### 4. fnuecke's Gist (open source)
```
https://gist.github.com/fnuecke/d4275087cc7969257eae0f939fac3d2f
```
- Clean, minimal ~40-line implementation
- Creates a new `GameView` instance via `ScriptableObject.CreateInstance()`
- Uses `ShowPopup()` for borderless window
- Hides the GameView toolbar via reflection on `showToolbar` property
- Toggle via menu shortcut (Ctrl+Shift+Alt+2)
- No settings, no toast, no play mode integration

**Key code pattern:**
```csharp
var instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
ShowToolbarProperty?.SetValue(instance, false);
instance.ShowPopup();
instance.position = new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
instance.Focus();
```

#### 5. mandarinx's Gist (open source, older)
- Uses `[InitializeOnLoad]` to auto-fullscreen on play
- Retrieves the existing GameView via `GetMainGameView()` reflection
- Repositions and resizes the existing window instead of creating a new one
- Hides toolbar by offsetting Y position by -22px (fragile hack)
- Uses deprecated `playmodeStateChanged` API (pre-2018)

### Technical Approaches Analyzed

We identified three fundamental strategies for achieving fullscreen:

#### Strategy A: Reposition Existing GameView
Move and resize the user's existing Game view to cover the screen.

| Pros | Cons |
|------|------|
| Simple - no new windows | Destructive - must restore original position/size |
| Same GameView state | Risk of corrupting editor layout |
| No duplicate rendering | Tab bar offset hack is fragile across versions |

**Verdict:** Rejected. The layout restoration risk is unacceptable. If Unity crashes during fullscreen or the restoration logic has a bug, the user's editor layout is corrupted.

#### Strategy B: New GameView via ShowPopup()
Create a second GameView instance and display it as a borderless popup.

| Pros | Cons |
|------|------|
| Non-destructive - original GameView untouched | Requires reflection to access internal GameView type |
| Clean enter/exit - just close the popup | Second GameView renders the game simultaneously |
| ShowPopup() removes all chrome natively | May need platform-specific tweaks for taskbar |
| Can copy settings from original | Domain reload loses static reference |

**Verdict: Chosen.** This is the safest and most reliable approach. The original Game tab is never modified, and cleanup is trivial - just close the popup window.

#### Strategy C: ContainerWindow Manipulation
Use Unity's internal `ContainerWindow` API to make the GameView's container fullscreen.

| Pros | Cons |
|------|------|
| Uses Unity's own window management | Heavily depends on internal APIs |
| Potentially more stable z-ordering | ContainerWindow API changes between versions |
| Native feel | Complex reflection required |
| | Hard to isolate from editor window system |

**Verdict:** Rejected for v0.1. While this is what the paid Fullscreen Editor asset uses, it requires significantly more reflection and version-specific compatibility code. The ShowPopup() approach achieves the same visual result with much less fragility.

---

## Architecture & Implementation

### Package Structure

The package follows Unity Package Manager (UPM) conventions for an editor-only package:

```
unity-fullscreen-play/
├── package.json                    # UPM manifest
├── README.md                       # User-facing documentation
├── CHANGELOG.md                    # Version history
├── LICENSE                         # MIT license
├── .gitignore
├── .github/
│   └── workflows/
│       └── release.yml             # Version bump + GitHub Release
└── Editor/
    ├── Shilo.FullscreenPlay.Editor.asmdef    # Assembly definition (editor-only)
    ├── FullscreenGameView.cs                 # Core fullscreen window management
    ├── FullscreenPlayController.cs           # Menu items, shortcuts, play mode hooks
    ├── FullscreenPlaySettings.cs             # Settings + preferences UI
    ├── FullscreenToast.cs                    # Toast notification overlay
    └── GameViewToolbarInjector.cs            # Dropdown injection into GameView toolbar
```

**Why editor-only:** All code runs exclusively in the Unity Editor. The `includePlatforms: ["Editor"]` in the assembly definition ensures none of this code is included in player builds. There is no `Runtime/` folder because the package has zero runtime footprint.

**Why root-level package.json:** Placing `package.json` at the repository root enables the simplest Git URL installation: `https://github.com/Shilo/unity-fullscreen-play.git` with no `?path=` parameter needed.

### Core Components

#### 1. FullscreenGameView.cs - The Engine

This is the core component that creates and manages the fullscreen window. It is a static class with three public methods: `EnterFullscreen()`, `ExitFullscreen()`, and `ToggleFullscreen()`.

**Lifecycle:**

```
EnterFullscreen()
    1. Guard: already fullscreen? not playing? -> abort
    2. Get GameView type via reflection
    3. Calculate target screen rect (DPI-aware)
    4. Create new GameView instance via ScriptableObject.CreateInstance()
    5. Hide toolbar via reflection (showToolbar property)
    6. ShowPopup() -> borderless, chromeless window
    7. Set position to cover full screen
    8. Copy display/resolution settings from existing GameView
    9. [Windows] Apply Win32 TOPMOST + popup style
   10. Show toast notification (delayed, so it layers on top)
   11. Return focus to GameView for input

ExitFullscreen()
    1. Hide toast
    2. [Windows] Remove TOPMOST flag
    3. Close the popup window
    4. Clear state
```

**Key reflection points:**
- `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")` - accesses the internal GameView type
- `GameViewType.GetProperty("showToolbar", NonPublic | Instance)` - hides the toolbar
- `GameViewType.GetProperty("targetDisplay", ...)` - copies display target
- `GameViewType.GetProperty("selectedSizeIndex", ...)` - copies resolution/aspect settings

#### 2. FullscreenPlayController.cs - The Brain

The `[InitializeOnLoad]` controller wires everything together:

**Play mode integration:**
```
EditorApplication.playModeStateChanged
    -> EnteredPlayMode: if PlayFullscreen enabled, enter fullscreen (delayed 1 frame)
    -> ExitingPlayMode: if fullscreen active, exit fullscreen
```

**Menu items:**
- `Edit > Fullscreen Play > Play Fullscreen` - toggle (persists via EditorPrefs)
- `Edit > Fullscreen Play > Enter Fullscreen Now` - one-shot or toggle if already playing
- `Edit > Fullscreen Play > Settings...` - opens Preferences panel

**Keyboard handling:**
- **F11** via `[Shortcut("Fullscreen Play/Toggle Fullscreen", KeyCode.F11)]` - Unity's official Shortcuts API, rebindable via Edit > Shortcuts
- **Escape** via `EditorApplication.globalEventHandler` (reflection) - intercepts Escape globally but only acts when fullscreen is active, then calls `Event.Use()` to consume it

**Why two different keyboard mechanisms:**
- F11 uses the `[Shortcut]` API because it should be discoverable and rebindable through Unity's Shortcuts Manager. Users can change it to any key they want.
- Escape uses the global event handler because registering Escape as a `[Shortcut]` would conflict with every game that uses Escape. The global handler only intercepts it when fullscreen is active and consumes the event so the game doesn't receive it.

**Assembly reload cleanup:**
`AssemblyReloadEvents.beforeAssemblyReload` fires before domain reload (script recompilation, package disable/re-enable). The handler:
1. Unhooks the `globalEventHandler` reflection delegate so it doesn't reference an unloaded assembly
2. Closes any open fullscreen window
3. Calls `GameViewToolbarInjector.RemoveAllOverlays()` to remove all injected `IMGUIContainer` elements from GameView visual trees

This ensures zero stale delegates, zero orphaned visual elements, and zero errors when the package is disabled, re-enabled, or scripts recompile.

#### 3. FullscreenPlaySettings.cs - Configuration

Settings are stored in `EditorPrefs` (per-user, not per-project) with the prefix `FullscreenPlay.`:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `PlayFullscreen` | bool | false | Auto-fullscreen on play |
| `Mode` | int (enum) | 0 (FullscreenWindowed) | Fullscreen mode |
| `ShowToast` | bool | true | Show exit instructions overlay |
| `ToastDuration` | float | 3.0 | Toast visibility duration (seconds) |
| `EnableHotkey` | bool | true | F11 hotkey enabled |

The `FullscreenPlaySettingsProvider` class implements a `SettingsProvider` registered at `Preferences/Fullscreen Play`, accessible via Edit > Preferences. It uses standard IMGUI controls (toggles, dropdowns, sliders) with help boxes for contextual guidance.

**Why EditorPrefs instead of ScriptableObject:** EditorPrefs are simpler for user preferences - no asset file to manage, no accidental version control of personal settings, and they survive project reimports. ScriptableObject settings would be appropriate for project-wide configuration, but fullscreen preferences are inherently per-user.

#### 4. FullscreenToast.cs - Visual Feedback

A small borderless popup (`EditorWindow` via `ShowPopup()`) that displays "Press Esc or F11 to exit fullscreen" at the top-center of the screen. It fades out after the configured duration.

**Rendering:**
- Background: dark semi-transparent rect (`rgba(0.12, 0.12, 0.12, 0.92)`)
- Border: subtle 1px line (`rgba(0.35, 0.35, 0.35, 0.6)`)
- Text: white, centered, 13pt
- Fade: linear alpha reduction from 65% to 100% of duration

**Why a separate EditorWindow:** Since the fullscreen GameView is an internal Unity type, we cannot override its `OnGUI` to draw overlays. A separate popup window layered on top is the only clean approach. The toast is shown with a `delayCall` to ensure it appears above the fullscreen window, then focus is returned to the GameView so game input works.

#### 5. GameViewToolbarInjector.cs - The Dropdown

Injects "Play Fullscreen" as a fourth option into the GameView's play-mode dropdown (alongside Play Focused, Play Maximized, Play Unfocused).

**The challenge:** The original dropdown is rendered via `EditorGUILayout.EnumPopup` driven by the `EnterPlayModeBehavior` enum. `EnumPopup` generates its items strictly from enum values — there is no extensibility hook, no `GenericMenu` to append to, and `DoToolbarGUI()` is private.

**The solution:** Overlay an opaque `IMGUIContainer` (UIToolkit) on top of the original dropdown. The overlay draws an identical `EditorGUI.DropdownButton` styled with `EditorStyles.toolbarDropDown` that opens a `GenericMenu` containing all four options.

**Detection (event-driven, no polling):**
- `EditorWindow.windowFocusChanged` — fires when any window gains focus; new GameViews always receive focus on creation
- `EditorApplication.playModeStateChanged` — GameViews may be recreated on mode change
- `DetachFromPanelEvent` on the overlay — triggers re-injection if the overlay is removed (domain reload, window rebuild)
- `EditorApplication.delayCall` — one initial scan at startup
- Multiple rapid triggers collapse into a single scan via a `s_ScanScheduled` flag

**Positioning (dynamic, width-adaptive):**
The toolbar layout from `DoToolbarGUI()` source is:
```
[LeftGroup] [FlexSpace1] [Dropdown 110px] [MiddleButtons] [FlexSpace2] [RightGroup]
```
The two `GUILayout.FlexibleSpace()` calls split remaining horizontal space equally. RightGroup (Audio, Shortcuts, Stats, Gizmos) and MiddleButtons (Frame Debugger) have deterministic widths. LeftGroup (display/resolution popups, zoom slider) is estimated. The `style.right` offset is recalculated on every `GeometryChangedEvent` (window resize). Any estimation error from the left-side width is halved by the flex-space split, and the overlay's extra width (140 vs 110 px) absorbs the remainder.

**Forward compatibility:** Every reflection access, visual tree operation, and drawing callback is individually wrapped in `try/catch`. If any internal API changes, the injector silently disables itself. The file is fully self-contained — deleting `GameViewToolbarInjector.cs` removes the feature entirely with no impact on other components.

### Technical Deep Dive

#### DPI Scaling

Modern displays (4K at 150% scaling, Retina, etc.) create a mismatch between physical pixels and logical points:

```csharp
var res = Screen.currentResolution;  // Physical pixels (e.g., 3840x2160)
float scale = EditorGUIUtility.pixelsPerPoint;  // e.g., 1.5 for 150%
float w = res.width / scale;   // Logical points (e.g., 2560)
float h = res.height / scale;  // Logical points (e.g., 1440)
```

`EditorWindow.position` operates in logical points (scaled coordinates), while `Screen.currentResolution` returns physical pixels. Dividing by `pixelsPerPoint` gives the correct rect for the window position.

The Win32 `SetWindowPos` call reverses this: it needs physical pixels, so we multiply back by the scale factor.

#### Windows Native Integration

On Windows, `ShowPopup()` alone may not cover the taskbar. We use Win32 P/Invoke to ensure full coverage:

```csharp
// Remove all window chrome
SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

// Position as topmost, covering entire screen in physical pixels
SetWindowPos(hwnd, HWND_TOPMOST, x, y, w, h, SWP_SHOWWINDOW);
```

On exit, `HWND_NOTOPMOST` is applied to clean up the z-order before closing:

```csharp
SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
```

This is wrapped in `#if UNITY_EDITOR_WIN` so it compiles cleanly on macOS/Linux, where `ShowPopup()` alone is sufficient (macOS handles fullscreen differently via its own window management).

#### GameView Settings Copying

When creating a new GameView instance, it starts with default settings (Display 1, Free Aspect). To match the user's existing Game tab configuration, we copy settings via reflection:

- **targetDisplay** - which camera display to render (Display 1, 2, etc.)
- **selectedSizeIndex** - which resolution/aspect ratio preset is selected

This ensures the fullscreen view renders identically to what the user was seeing in their Game tab.

#### Domain Reload & Package Disable Safety

When Unity recompiles scripts or a package is disabled, a domain reload occurs — all managed static state is reset. The package handles this in two phases:

**Before reload** (`AssemblyReloadEvents.beforeAssemblyReload`):
1. Unhook the `globalEventHandler` reflection delegate (prevents stale delegate references)
2. Close the fullscreen popup window if open
3. Remove all injected `IMGUIContainer` overlays from GameView visual trees (prevents dead callbacks)

**After reload** (`[InitializeOnLoad]` static constructor):
1. Re-subscribe managed events (`playModeStateChanged`, `windowFocusChanged`)
2. Re-hook the `globalEventHandler`
3. Re-inject toolbar overlays into GameView instances
4. If not in play mode, call `FullscreenGameView.Cleanup()` to clear orphaned state

This two-phase approach ensures:
- **Package disable:** `beforeAssemblyReload` runs, cleans up all external state, then the assembly unloads cleanly. No stale delegates or visual elements remain.
- **Package re-enable:** `[InitializeOnLoad]` runs fresh in the new domain. All state starts clean.
- **Script recompilation:** Both phases run in sequence — cleanup then re-init. No accumulation of duplicate handlers.

---

## Design Decisions

### Decision 1: New Window vs. Reposition Existing

**Chose: New window.**

Creating a second GameView instance is non-destructive. The user's editor layout is never modified. If anything goes wrong (crash, bug, power loss), the original Game tab is exactly where it was. This is the single most important design decision in the package.

The tradeoff is that two GameView instances render simultaneously, using slightly more GPU. In practice this is negligible - the editor was already rendering the Game tab, and the second instance shares the same render pipeline.

### Decision 2: ShowPopup() vs. ContainerWindow

**Chose: ShowPopup().**

`ShowPopup()` is a public, documented API that creates a borderless window. The internal `ContainerWindow` approach used by some paid plugins gives more control but requires extensive reflection that breaks across Unity versions. Since our goal is simply "borderless window at screen size," ShowPopup() achieves this directly.

### Decision 3: EditorPrefs vs. ScriptableObject for Settings

**Chose: EditorPrefs.**

Fullscreen preferences are per-user, not per-project. A developer might want F11 enabled while their colleague doesn't. EditorPrefs stores per-machine and doesn't pollute the project's Assets folder or version control.

### Decision 4: [Shortcut] API for F11, globalEventHandler for Escape

**Chose: Hybrid approach.**

The `[Shortcut]` attribute integrates with Unity's Shortcuts Manager, making F11 discoverable and rebindable. This is ideal for a persistent, user-configurable hotkey.

Escape cannot use `[Shortcut]` because it would globally intercept Escape in all contexts, breaking games that use Escape for pause menus, etc. The `globalEventHandler` approach (via reflection on `EditorApplication`) lets us intercept Escape only when fullscreen is active and consume the event so the game never sees it.

### Decision 5: Toast as Separate Window

**Chose: Separate popup overlay.**

Since we cannot modify the internal GameView's rendering pipeline, a separate small popup window is the cleanest way to show overlay UI. The toast is deliberately positioned at the top-center (like browser fullscreen notifications) and auto-fades to avoid obstructing the game.

Focus management is critical: after showing the toast, we immediately return focus to the GameView so game input works. The toast is non-interactive - it only displays information.

### Decision 6: Toolbar Dropdown Integration via UIToolkit Overlay

**Chose: Overlay injection with menu-item fallback.**

The play-mode dropdown (`Play Focused / Play Maximized / Play Unfocused`) is rendered by `EditorGUILayout.EnumPopup` inside `GameView.DoToolbarGUI()`. `EnumPopup` is driven by enum values and cannot be extended. Three strategies were considered:

1. **Harmony patching** of `DoToolbarGUI()` — would allow arbitrary injection but adds an external dependency and is fragile across Unity versions.
2. **ContainerWindow / Overlay API** — Unity's `[Overlay]` attribute requires a compile-time `Type` parameter, and `GameView` is internal. Runtime overlay registration is not publicly supported.
3. **UIToolkit overlay on top of IMGUI** — place an opaque `IMGUIContainer` in the GameView's `rootVisualElement` that draws an identical dropdown covering the original.

Option 3 was chosen. It requires no external dependencies, uses only public APIs (`IMGUIContainer`, `EditorGUI.DropdownButton`, `GenericMenu`), and the positioning can be calculated dynamically from the known toolbar layout structure. The overlay is wider than the original (140 vs 110 px) to absorb positioning estimation error.

The `Edit > Fullscreen Play` menu and F11 shortcut remain as universal fallbacks — they work even if the overlay injection silently fails due to an API change.

### Decision 7: UPM Package at Repo Root

**Chose: Root-level package.json.**

This enables the simplest possible installation URL:
```
https://github.com/Shilo/unity-fullscreen-play.git
```

No `?path=` parameter needed. Users can pin versions with `#v0.1.0` tags. The tradeoff is that the repo can only contain one package, but that's appropriate here - this is a single-purpose package.

---

## Platform Support

| Platform | Fullscreen Windowed | Exclusive Fullscreen | Taskbar Coverage |
|----------|-------------------|---------------------|-----------------|
| Windows | Full support | Planned (not in v0.1) | Win32 TOPMOST |
| macOS | ShowPopup() only | Not planned | Native handling |
| Linux | ShowPopup() only | Not planned | Native handling |

Windows receives the most attention because:
1. It's the primary development platform for most Unity developers
2. The taskbar doesn't auto-hide for popup windows without Win32 intervention
3. Exclusive fullscreen via `ChangeDisplaySettings` is a Windows-specific API

macOS and Linux rely on `ShowPopup()` alone. On macOS, popup windows typically cover the dock. On Linux, behavior varies by window manager but is generally correct.

---

## Known Limitations & Edge Cases

### Domain Reload During Play Mode
If scripts are recompiled while fullscreen is active, the popup window will close and the static state will be reset. The user needs to re-enter fullscreen manually. This is an inherent limitation of Unity's domain reload system.

### Multi-Monitor
Currently targets the primary monitor (position 0,0). The screen rect calculation uses `Screen.currentResolution` which returns the primary display's resolution. Multi-monitor targeting (fullscreen on a secondary display) is a planned future feature.

### Cursor Lock
Games using `Cursor.lockState = CursorLockMode.Locked` will lock the cursor in the fullscreen window. Pressing Escape may first unlock the cursor (Unity's default behavior) before a second Escape exits fullscreen. This is consistent with built game behavior.

### Alt-Tab
Alt-tabbing while fullscreen will bring other windows in front. The fullscreen window remains open in the background. The user can alt-tab back or press F11/Esc to exit. The `HWND_TOPMOST` flag is only set during the initial window creation; it doesn't prevent alt-tab from working normally.

### Exclusive Fullscreen
The `FullscreenMode.ExclusiveFullscreen` enum value exists in settings but is not yet implemented in v0.1. True exclusive fullscreen in the editor would require `ChangeDisplaySettings` (Windows) to change the display resolution, which risks destabilizing the editor. This is deferred to a future version with proper safety guards.

### Toolbar Dropdown Overlay Positioning
The `GameViewToolbarInjector` overlay position is calculated from the known toolbar layout structure, but the left-side element width (display popup, resolution popup, zoom slider) is estimated. Any estimation error is halved by the flex-space split and absorbed by the overlay's extra width. In rare configurations (e.g., XR mode active, RenderDoc attached), additional conditional toolbar buttons may shift the dropdown further. If the overlay visually misaligns, it remains functional — and the Edit menu / F11 shortcut always work. Adjust `EstimatedLeftGroupWidth` in `GameViewToolbarInjector.cs` to fine-tune for a specific setup.

### GameView Toolbar Property
The `showToolbar` property accessed via reflection is internal to Unity and may change or be removed in future versions. If the property is not found, the toolbar will simply remain visible - the fullscreen experience still works, just with a small toolbar at the top.

---

## Release Infrastructure

### GitHub Actions Workflow

The `release.yml` workflow automates version management:

1. **Trigger**: Manual dispatch (`workflow_dispatch`) with version bump type selection (patch/minor/major)
2. **Version bump**: Reads current version from `package.json`, increments the selected component, writes back
3. **Commit**: Creates a `chore: bump version to X.Y.Z` commit
4. **Tag**: Creates a `vX.Y.Z` Git tag
5. **Release**: Creates a GitHub Release with:
   - Install URL with version pin: `https://github.com/.../unity-fullscreen-play.git#vX.Y.Z`
   - Auto-generated release notes from commit messages since last tag

This enables a one-click release process that keeps `package.json`, Git tags, and GitHub Releases in sync.

### Version Pinning for Users

UPM Git dependencies support version pinning via Git tags:
```
https://github.com/Shilo/unity-fullscreen-play.git#v0.1.0
```

Without a tag, Unity resolves `HEAD` of the default branch, which may include unreleased changes. The release workflow ensures every published version has a corresponding tag.

---

## Summary

**Unity Fullscreen Play** solves the gap between Unity Editor's Play Maximized mode and true fullscreen by creating a borderless popup GameView window that covers the entire screen.

**The approach is deliberately conservative:**
- Uses `ShowPopup()` (public API) instead of internal `ContainerWindow` manipulation
- Creates a new window instead of modifying the existing Game tab
- Settings stored in EditorPrefs (per-user, not per-project)
- Platform-specific code isolated behind `#if` directives
- Graceful fallbacks when reflection targets are missing

**The implementation is focused:**
- 5 C# files, ~1,100 lines of code total
- Zero runtime footprint (editor-only assembly)
- No dependencies beyond Unity itself
- Installs via a single Git URL

**What it achieves:**
- True fullscreen game testing without building
- "Play Fullscreen" option in the GameView's play-mode dropdown
- Familiar UX (Esc/F11, toast notification like browser fullscreen)
- Configurable behavior through Unity's native Preferences UI
- Rebindable hotkey through Unity's native Shortcuts Manager
- Safe cleanup on play mode exit, domain reload, and editor restart
- Forward-compatible: every reflection point silently no-ops on failure

The package prioritizes reliability and simplicity over feature count. Future versions may add multi-monitor support and true exclusive fullscreen mode.
