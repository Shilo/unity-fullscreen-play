# GameView Toolbar Button Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fragile dropdown overlay in `GameViewToolbarInjector` with a simple icon toggle button that toggles "Auto-Fullscreen on Play."

**Architecture:** Rewrite `GameViewToolbarInjector.cs` to inject a small `IMGUIContainer` button (not an overlay dropdown) into each GameView's VisualElement tree. The button uses a Unity built-in icon with cascading fallback. Every code path is wrapped in bare `try/catch` — zero noise on failure.

**Tech Stack:** Unity Editor API (UIElements, IMGUI, Reflection), C# 9

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Editor/GameViewToolbarInjector.cs` | **Rewrite** | Inject icon toggle button into GameView toolbar |
| `Editor/Locales/en.json` | **Modify** | Add tooltip strings |
| `Editor/Locales/de.json` | **Modify** | Add German tooltip strings |

No new files. No changes to `FullscreenPlayController.cs`, `FullscreenGameView.cs`, or `FullscreenPlaySettings.cs`.

---

### Task 1: Add Localization Keys

**Files:**
- Modify: `Editor/Locales/en.json:30` (before closing brace)
- Modify: `Editor/Locales/de.json:30` (before closing brace)

- [ ] **Step 1: Add English tooltip strings**

In `Editor/Locales/en.json`, add these two keys before the closing `}`:

```json
  "update_success": "Package updated to version {0}.",

  "fullscreen_button_tooltip_enabled": "Auto-Fullscreen on Play (Enabled)",
  "fullscreen_button_tooltip_disabled": "Auto-Fullscreen on Play (Disabled)"
}
```

- [ ] **Step 2: Add German tooltip strings**

In `Editor/Locales/de.json`, add these two keys before the closing `}`:

```json
  "update_success": "Paket auf Version {0} aktualisiert.",

  "fullscreen_button_tooltip_enabled": "Auto-Vollbild beim Abspielen (Aktiviert)",
  "fullscreen_button_tooltip_disabled": "Auto-Vollbild beim Abspielen (Deaktiviert)"
}
```

- [ ] **Step 3: Commit**

```bash
git add Editor/Locales/en.json Editor/Locales/de.json
git commit -m "i18n: add toolbar button tooltip strings (en, de)"
```

---

### Task 2: Rewrite GameViewToolbarInjector — Static Init & Reflection

**Files:**
- Rewrite: `Editor/GameViewToolbarInjector.cs` (replace entire file)

This task replaces the file with the new skeleton: static constructor, reflection resolution, and the `s_Ready` gate. No drawing or injection logic yet.

- [ ] **Step 1: Replace `GameViewToolbarInjector.cs` with the new skeleton**

Replace the entire contents of `Editor/GameViewToolbarInjector.cs` with:

```csharp
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Injects a fullscreen toggle button into the GameView toolbar.
    ///
    /// The button toggles <see cref="FullscreenPlaySettings.PlayFullscreen"/>.
    /// If toggled on while already in Play mode, fullscreen activates
    /// immediately. If toggled off while fullscreen, it exits.
    ///
    /// <b>Zero-noise guarantee:</b> every reflection access and every
    /// visual-tree manipulation is individually wrapped in bare
    /// <c>try/catch</c>. If anything fails at any level, the button
    /// silently doesn't appear — no crashes, no errors, no warnings.
    /// The Tools menu and F11 shortcut continue to work regardless.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewToolbarInjector
    {
        // ----- constants -----

        private const string OverlayName = "fullscreen-play-toggle-button";
        private const float ButtonWidth = 28f;
        private const float ButtonHeight = 18f;
        private const float ButtonTopOffset = 1f;

        // ----- reflection handles (resolved once) -----

        private static readonly Type s_GameViewType;
        private static readonly PropertyInfo s_ShowToolbarProp;
        private static readonly bool s_Ready;

        // ----- icon (resolved once) -----

        private static GUIContent s_ButtonContent;

        // ================================================================
        //  Initialisation
        // ================================================================

        static GameViewToolbarInjector()
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;

                s_GameViewType = asm.GetType("UnityEditor.GameView");
                if (s_GameViewType == null) return;

                s_ShowToolbarProp = s_GameViewType.GetProperty(
                    "showToolbar",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                ResolveIcon();

                s_Ready = true;

                EditorWindow.windowFocusChanged += OnWindowFocusChanged;
                EditorApplication.playModeStateChanged += _ => ScheduleScan();
                EditorApplication.delayCall += ScanAndInject;
            }
            catch { /* silent — feature disabled */ }
        }

        // ================================================================
        //  Icon resolution
        // ================================================================

        private static void ResolveIcon()
        {
            // Try built-in icon names in priority order.
            // Unity has renamed/added icons across versions.
            string[] candidates =
            {
                "d_FullscreenOn",
                "FullscreenOn",
                "d_Fullscreen",
                "Fullscreen",
                "d_ScaleTool",
            };

            foreach (var name in candidates)
            {
                try
                {
                    var content = EditorGUIUtility.IconContent(name);
                    if (content != null && content.image != null)
                    {
                        s_ButtonContent = new GUIContent(content.image);
                        return;
                    }
                }
                catch { /* try next */ }
            }

            // Final fallback: text label
            s_ButtonContent = new GUIContent("FS");
        }

        // ================================================================
        //  Event-driven scanning
        // ================================================================

        private static void OnWindowFocusChanged()
        {
            try
            {
                if (!s_Ready) return;
                ScheduleScan();
            }
            catch { /* silent no-op */ }
        }

        private static bool s_ScanScheduled;

        private static void ScheduleScan()
        {
            try
            {
                if (s_ScanScheduled) return;
                s_ScanScheduled = true;
                EditorApplication.delayCall += () =>
                {
                    s_ScanScheduled = false;
                    ScanAndInject();
                };
            }
            catch { /* silent no-op */ }
        }

        private static void ScanAndInject()
        {
            if (!s_Ready) return;

            try
            {
                var views = Resources.FindObjectsOfTypeAll(s_GameViewType);
                foreach (var obj in views)
                {
                    try
                    {
                        var window = obj as EditorWindow;
                        if (window == null) continue;
                        if (IsToolbarHidden(window)) continue;

                        var root = window.rootVisualElement;
                        if (root == null) continue;
                        if (root.Q(OverlayName) != null) continue;

                        Inject(window, root);
                    }
                    catch { /* skip this view */ }
                }
            }
            catch { /* silent no-op */ }
        }

        // ================================================================
        //  Injection (placeholder — Task 3)
        // ================================================================

        private static void Inject(EditorWindow gameView, VisualElement root)
        {
            // Implemented in Task 3
        }

        // ================================================================
        //  Drawing (placeholder — Task 3)
        // ================================================================

        private static void DrawToggle(EditorWindow gameView)
        {
            // Implemented in Task 3
        }

        // ================================================================
        //  Cleanup
        // ================================================================

        internal static void RemoveAllOverlays()
        {
            try
            {
                if (s_GameViewType == null) return;

                var views = Resources.FindObjectsOfTypeAll(s_GameViewType);
                foreach (var obj in views)
                {
                    try
                    {
                        var window = obj as EditorWindow;
                        if (window == null) continue;

                        var root = window.rootVisualElement;
                        var overlay = root?.Q(OverlayName);
                        overlay?.RemoveFromHierarchy();
                    }
                    catch { /* silent no-op */ }
                }
            }
            catch { /* silent no-op */ }
        }

        // ================================================================
        //  Helpers
        // ================================================================

        private static bool IsToolbarHidden(EditorWindow window)
        {
            try
            {
                if (s_ShowToolbarProp == null) return false;
                return !(bool)s_ShowToolbarProp.GetValue(window);
            }
            catch { return false; }
        }
    }
}
```

- [ ] **Step 2: Verify the file compiles**

Open Unity Editor and confirm no compile errors in the Console. The button won't appear yet (Inject is empty), but the existing functionality (Tools menu, F11 shortcut) must still work.

- [ ] **Step 3: Commit**

```bash
git add Editor/GameViewToolbarInjector.cs
git commit -m "refactor(toolbar): rewrite injector skeleton with icon resolution

Replaces the 440-line dropdown overlay with a simpler toggle button
architecture. This commit has the skeleton: static init, reflection,
icon resolution with cascading fallback, scan/inject lifecycle, and
cleanup. Inject and DrawToggle are placeholders for the next task."
```

---

### Task 3: Implement Inject & DrawToggle

**Files:**
- Modify: `Editor/GameViewToolbarInjector.cs` (fill in `Inject` and `DrawToggle` methods)

- [ ] **Step 1: Replace the `Inject` placeholder with the real implementation**

Replace the `Inject` method in `Editor/GameViewToolbarInjector.cs`:

```csharp
        private static void Inject(EditorWindow gameView, VisualElement root)
        {
            try
            {
                var weak = new WeakReference(gameView);

                var button = new IMGUIContainer(() =>
                {
                    try
                    {
                        if (weak.Target is EditorWindow target)
                            DrawToggle(target);
                    }
                    catch { /* silent no-op */ }
                });

                button.name        = OverlayName;
                button.pickingMode = PickingMode.Position;

                button.style.position = Position.Absolute;
                button.style.top      = new StyleLength(ButtonTopOffset);
                button.style.height   = new StyleLength(ButtonHeight);
                button.style.width    = new StyleLength(ButtonWidth);

                // Position to the right of center — near the play-mode dropdown.
                // We use a right offset calculated from the window width.
                button.style.right = new StyleLength(CalculateRightOffset(root.resolvedStyle.width));

                // Recalculate on resize via the parent's geometry changes.
                button.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    try
                    {
                        var parentWidth = button.parent?.resolvedStyle.width ?? 0f;
                        if (parentWidth > 0f)
                            button.style.right = new StyleLength(
                                CalculateRightOffset(parentWidth));
                    }
                    catch { /* silent no-op */ }
                });

                EventCallback<GeometryChangedEvent> rootCallback = evt =>
                {
                    try
                    {
                        button.style.right = new StyleLength(
                            CalculateRightOffset(evt.newRect.width));
                    }
                    catch { /* silent no-op */ }
                };
                root.RegisterCallback(rootCallback);

                button.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    try
                    {
                        root.UnregisterCallback(rootCallback);
                        ScheduleScan();
                    }
                    catch { /* silent no-op */ }
                });

                root.Add(button);
            }
            catch { /* silent no-op */ }
        }
```

- [ ] **Step 2: Replace the `DrawToggle` placeholder with the real implementation**

Replace the `DrawToggle` method in `Editor/GameViewToolbarInjector.cs`:

```csharp
        private static void DrawToggle(EditorWindow gameView)
        {
            bool wasEnabled = FullscreenPlaySettings.PlayFullscreen;

            // Update tooltip based on current state
            s_ButtonContent.tooltip = wasEnabled
                ? I18n.Tr("fullscreen_button_tooltip_enabled")
                : I18n.Tr("fullscreen_button_tooltip_disabled");

            var rect = new Rect(0, 0, ButtonWidth, ButtonHeight);
            bool nowEnabled = GUI.Toggle(rect, wasEnabled, s_ButtonContent,
                EditorStyles.toolbarButton);

            if (nowEnabled != wasEnabled)
            {
                FullscreenPlaySettings.PlayFullscreen = nowEnabled;

                if (nowEnabled && EditorApplication.isPlaying
                    && !FullscreenGameView.IsFullscreen)
                {
                    EditorApplication.delayCall +=
                        () => { try { FullscreenGameView.EnterFullscreen(); } catch { } };
                }
                else if (!nowEnabled && FullscreenGameView.IsFullscreen)
                {
                    EditorApplication.delayCall +=
                        () => { try { FullscreenGameView.ExitFullscreen(); } catch { } };
                }

                gameView.Repaint();
            }
        }
```

- [ ] **Step 3: Add the `CalculateRightOffset` method**

Add this method to the class (in the positioning section, after `Inject`):

```csharp
        // ================================================================
        //  Positioning
        // ================================================================

        /// <summary>
        /// Right-side elements: Gizmos + Stats + Shortcuts + Audio buttons.
        /// </summary>
        private const float RightGroupWidth = 160f;

        /// <summary>
        /// Frame Debugger button between the dropdown and the right group.
        /// </summary>
        private const float MiddleButtonsWidth = 25f;

        /// <summary>
        /// Estimated left-side width (window popup, display, size, zoom).
        /// Any error is halved by the flex split.
        /// </summary>
        private const float EstimatedLeftGroupWidth = 380f;

        /// <summary>
        /// The original play-mode dropdown width (110px in source).
        /// </summary>
        private const float OriginalDropdownWidth = 110f;

        /// <summary>
        /// Calculates the right offset to position the button just after
        /// the play-mode dropdown.
        ///
        /// Toolbar layout: [Left] [Flex] [Dropdown 110px] [Middle] [Flex] [Right]
        /// We want our button between [Dropdown] and [Middle].
        /// </summary>
        private static float CalculateRightOffset(float windowWidth)
        {
            if (windowWidth < 400f)
                return 180f;

            float totalFixed = EstimatedLeftGroupWidth + OriginalDropdownWidth
                             + MiddleButtonsWidth + RightGroupWidth;
            float totalFlex  = Mathf.Max(0f, windowWidth - totalFixed);
            float eachFlex   = totalFlex / 2f;

            // Right edge of the dropdown, measured from the window's right edge:
            //   RightGroup + FlexSpace2 + MiddleButtons
            // Our button sits just left of that (i.e. between dropdown and middle).
            float rightOffset = RightGroupWidth + eachFlex + MiddleButtonsWidth;

            return Mathf.Max(0f, rightOffset);
        }
```

- [ ] **Step 4: Verify in Unity Editor**

1. Open Unity Editor — confirm no compile errors
2. Open a Game View — the toggle button should appear in the toolbar
3. Click the button — it should toggle pressed/depressed appearance
4. Hover over the button — tooltip should show "Auto-Fullscreen on Play (Disabled)" or "(Enabled)"
5. Check Edit > Preferences > Fullscreen Play — the "Play Fullscreen" setting should match the button state
6. Enter Play mode with the button enabled — should go fullscreen
7. Press Esc to exit fullscreen
8. Disable the button — confirm setting persists after editor restart

- [ ] **Step 5: Commit**

```bash
git add Editor/GameViewToolbarInjector.cs
git commit -m "feat(toolbar): implement fullscreen toggle button injection

Injects a small icon toggle button to the right of the play-mode
dropdown in the GameView toolbar. Toggles Auto-Fullscreen on Play
setting. If enabled while playing, enters fullscreen immediately.
Every code path wrapped in bare try/catch — zero noise on failure."
```

---

### Task 4: Clean Up Unused Locale Keys (Optional)

The old dropdown injector used `play_focused`, `play_maximized`, `play_unfocused`, and `play_fullscreen` labels. The new button doesn't use the first three. However, `play_fullscreen` is still referenced in the settings UI tooltip. The three play-mode labels are no longer used by any code in this package.

**Files:**
- Modify: `Editor/Locales/en.json`
- Modify: `Editor/Locales/de.json`

- [ ] **Step 1: Verify which locale keys are still referenced**

Search the codebase for `play_focused`, `play_maximized`, `play_unfocused`:

```bash
grep -r "play_focused\|play_maximized\|play_unfocused" Editor/ --include="*.cs"
```

Expected: no results (these were only used in the old `GameViewToolbarInjector` dropdown labels).

- [ ] **Step 2: Remove unused keys from en.json**

Remove these three lines from `Editor/Locales/en.json`:

```json
  "play_focused": "Play Focused",
  "play_maximized": "Play Maximized",
  "play_unfocused": "Play Unfocused",
```

- [ ] **Step 3: Remove unused keys from de.json**

Remove these three lines from `Editor/Locales/de.json`:

```json
  "play_focused": "Fokussiert abspielen",
  "play_maximized": "Maximiert abspielen",
  "play_unfocused": "Unfokussiert abspielen",
```

- [ ] **Step 4: Verify in Unity Editor**

Open Unity, confirm no errors. The button and all settings should still work.

- [ ] **Step 5: Commit**

```bash
git add Editor/Locales/en.json Editor/Locales/de.json
git commit -m "chore(i18n): remove unused play-mode dropdown labels

These keys were only used by the old dropdown overlay which has been
replaced by the toggle button."
```

---

### Task 5: Update Documentation

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the Game view dropdown feature description**

In `README.md`, find the features list and update the Game view dropdown entry. Replace:

```markdown
- **Game view dropdown** — "Play Fullscreen" alongside Play Focused / Maximized / Unfocused
```

With:

```markdown
- **Game view toolbar button** — fullscreen toggle icon in the GameView toolbar, next to the Play Mode dropdown
```

- [ ] **Step 2: Update the How It Works section**

In `README.md`, find the paragraph about the toolbar dropdown and replace:

```markdown
**The toolbar dropdown** ("Play Fullscreen" alongside Play Focused / Maximized / Unfocused) works by overlaying an invisible button on top of Unity's built-in dropdown. Since the built-in dropdown is driven by an enum that can't be extended, the package draws its own identical dropdown on top that includes the extra option. If Unity changes its internal toolbar layout in a future version, the overlay silently disables itself and the Tools menu / F11 shortcut still work.
```

With:

```markdown
**The toolbar button** (fullscreen toggle icon next to the Play Mode dropdown) is injected into the GameView's visual tree as a small `IMGUIContainer`. It uses a Unity built-in icon with cascading fallbacks for cross-version compatibility. If Unity changes its internal toolbar structure in a future version, the button silently doesn't appear — no errors, no warnings — and the Tools menu / F11 shortcut still work.
```

- [ ] **Step 3: Update the Usage section**

In `README.md`, find:

```markdown
You can also enable this from the Game view toolbar dropdown (select "Play Fullscreen").
```

Replace with:

```markdown
You can also enable this from the Game view toolbar (click the fullscreen toggle icon next to the Play Mode dropdown).
```

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: update README for toolbar button redesign"
```
