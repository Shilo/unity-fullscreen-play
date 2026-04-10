# GameView Toolbar Button Redesign

**Date:** 2026-04-10
**Status:** Approved
**Approach:** A — VisualElement Tree Injection

## Problem

The current `GameViewToolbarInjector` overlays a fake dropdown menu on top of Unity's built-in "Play Focused / Play Maximized / Play Unfocused" dropdown. This approach:

- Doesn't position correctly across different window widths
- Doesn't fully cover the target dropdown
- Is fragile and likely to break across Unity versions
- Contains ~440 lines of complex positioning math and dropdown mirroring

The good news is it no-ops on failure. The bad news is it rarely works properly.

## Solution

Replace the overlay dropdown with a small **icon toggle button** injected to the right of the existing Play Mode dropdown. The button toggles the "Auto-Fullscreen on Play" setting.

## Button Specification

### Appearance
- Small icon-only toolbar toggle (~22×18px)
- Positioned to the right of the "Play Focused/Maximized/Unfocused" dropdown
- Uses Unity built-in icon via `EditorGUIUtility.IconContent()` with cascading fallback
- Drawn as `GUILayout.Toggle(..., EditorStyles.toolbarButton)` inside an `IMGUIContainer`
- Standard toolbar pressed/depressed look when active (matches Gizmos, Stats, etc.)
- Localized tooltip: "Auto-Fullscreen on Play (Enabled)" / "(Disabled)"

### Icon Resolution Order
```
"d_FullscreenOn"     → Unity 6+ dark theme fullscreen icon
"FullscreenOn"       → light theme variant
"d_Fullscreen"       → alternate naming
"Fullscreen"         → alternate naming
"d_ScaleTool"        → fallback to a recognizable expand icon
→ GUIContent("FS")   → final text fallback
```

The exact list will be validated at implementation time. The pattern is: try multiple names, fall back to text.

### Behavior
- **Click:** Toggles `FullscreenPlaySettings.PlayFullscreen`
- **Toggle ON while playing:** Immediately enters fullscreen via `FullscreenGameView.EnterFullscreen()`
- **Toggle OFF while fullscreen:** Immediately exits fullscreen via `FullscreenGameView.ExitFullscreen()`
- **Core identity:** This is a persistent setting toggle. It simply reacts to being enabled while in play mode.

## Injection Strategy

### Finding the anchor
- Walk the GameView's `rootVisualElement` tree to find the toolbar area
- Look for the IMGUI toolbar container (typically the first `IMGUIContainer` child or a known container class name)
- If not found, silently no-op

### Inserting the button
- Create an `IMGUIContainer` that draws the toggle
- Position with `Position.Absolute`, anchored from the right side of the toolbar
- Simpler offset calculation than the current overlay approach — only need approximate dropdown position
- Small button size gives generous positioning error tolerance

### Lifecycle (unchanged patterns)
- `[InitializeOnLoad]` static class
- Event-driven scanning: `windowFocusChanged`, `playModeStateChanged`, `delayCall`
- `ScanAndInject` / `RemoveAllOverlays` API
- `WeakReference` for captured EditorWindow references
- `DetachFromPanelEvent` triggers re-scan

## Error Handling Contract — Zero Noise

**Absolute rule: no crashes, no freezes, no errors, no warnings, no `Debug.Log` output.** This button is a convenience feature. If anything goes wrong at any level, the button silently doesn't appear. The user never knows it failed — they just use the Tools menu or F11 instead.

Every operation is individually wrapped in bare `catch` (not `catch (Exception e)` with logging). No `Debug.LogWarning`, no `Debug.LogError`, no re-throws. Silent swallow everywhere.

### Failure cascade

| What fails | What happens | User sees |
|---|---|---|
| Static constructor reflection | `s_Ready = false`, entire class is permanently inert | Nothing — no button |
| `Resources.FindObjectsOfTypeAll` | Scan silently skips | Nothing — no button |
| VisualElement tree walk fails | Injection silently skips that GameView | Nothing — no button |
| All icon names fail to resolve | Fall back to `GUIContent("FS")` text label | Text button "FS" |
| `GUIContent("FS")` somehow fails | Draw callback returns without drawing | Nothing — no button |
| IMGUIContainer creation/insertion throws | Injection silently skips | Nothing — no button |
| Toggle click handler throws | Click is silently swallowed | Button appears but click does nothing |
| Button element detached from panel | Schedule re-scan (may re-inject) | Button briefly disappears, may reappear |
| `FullscreenPlaySettings` read/write throws | Toggle silently fails | Button appears but state doesn't change |
| `FullscreenGameView.EnterFullscreen()` throws | Caught inside click handler | Button toggles but fullscreen doesn't activate |
| GeometryChangedEvent callback throws | Position update silently skips | Button may be slightly mispositioned |
| `RemoveAllOverlays` fails during cleanup | Silently skips — overlay will be GC'd anyway | Nothing visible |

### Implementation pattern

```csharp
// EVERY method body follows this pattern:
private static void SomeMethod()
{
    try
    {
        // ... actual logic ...
    }
    catch { /* silent no-op */ }
}

// EVERY lambda/callback follows this pattern:
var overlay = new IMGUIContainer(() =>
{
    try { DrawToggle(target); }
    catch { /* silent no-op */ }
});
```

No exceptions. No "this one is safe enough to skip the try/catch." Every single code path that could throw is wrapped.

The Tools menu and F11 shortcut work regardless of injection success — they are completely independent code paths in `FullscreenPlayController`.

## Localization

New i18n keys in `en.json` and `de.json`:
- `fullscreen_button_tooltip_enabled` — "Auto-Fullscreen on Play (Enabled)"
- `fullscreen_button_tooltip_disabled` — "Auto-Fullscreen on Play (Disabled)"

## Reflection Dependencies

### Kept
- `GameView` type (to find GameView instances)
- `GameView.showToolbar` (to skip injecting into fullscreen GameViews)

### Removed
- `PlayModeView.enterPlayModeBehavior` property
- `PlayModeView.EnterPlayModeBehavior` enum type

## Code Impact

- `GameViewToolbarInjector.cs`: ~440 lines → ~200 lines
- Remove: overlay positioning math, fake dropdown, behavior enum reflection
- Add: ~2 lines per locale file (tooltip keys)
- No changes to `FullscreenPlayController`, `FullscreenGameView`, or `FullscreenPlaySettings`
