using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Injects a "Play Fullscreen" option into the GameView's play-mode
    /// behaviour dropdown by overlaying a custom dropdown on top of the
    /// original <c>EnumPopup</c>.
    ///
    /// The original dropdown (<c>EnterPlayModeBehavior</c>) is rendered via
    /// <c>EditorGUILayout.EnumPopup</c> inside an IMGUI toolbar, which
    /// cannot be extended. This class places an opaque
    /// <c>IMGUIContainer</c> over the exact same screen region, drawing an
    /// identical <c>DropdownButton</c> that opens a <c>GenericMenu</c>
    /// containing the three original options <b>plus</b> "Play Fullscreen".
    ///
    /// <b>Forward-compatibility guarantee:</b> every reflection access and
    /// every visual-tree manipulation is individually wrapped in
    /// <c>try/catch</c>. If Unity renames, moves, or removes any internal
    /// API the injector depends on, it silently disables itself — no errors,
    /// no warnings, no crashes. The <c>Edit &gt; Fullscreen Play</c> menu
    /// and the F11 shortcut continue to work regardless.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewToolbarInjector
    {
        // ----- constants -----

        private const string OverlayName = "fullscreen-play-dropdown-overlay";

        // Overlay width — slightly wider than the original 110 px dropdown
        // to provide error margin for positioning.
        private const float OverlayWidth = 140f;
        private const float OverlayHeight = 19f;
        private const float OverlayTopOffset = 1f;

        // ----- toolbar layout model (from GameView.DoToolbarGUI source) -----
        // The toolbar is:
        //   [LeftGroup] [FlexSpace1] [Dropdown 110px] [MiddleButtons] [FlexSpace2] [RightGroup]
        //
        // FlexSpace1 and FlexSpace2 split remaining space equally.
        // RightGroup and MiddleButtons have deterministic widths;
        // LeftGroup varies (zoom slider fills available space), so we
        // estimate it and accept that the flex-space calculation will
        // have a small error (~10-20 px) that the wider overlay absorbs.

        /// <summary>Right-side always-present elements: Gizmos + Stats + Shortcuts + Audio.</summary>
        private const float RightGroupWidth = 160f;

        /// <summary>Buttons between the dropdown and FlexSpace2 (Frame Debugger is always present).</summary>
        private const float MiddleButtonsWidth = 25f;

        /// <summary>
        /// Estimated total width of left-side elements (window-type popup,
        /// display popup, size popup, zoom slider). This is approximate.
        /// Any error here is halved by the flex-space split.
        /// </summary>
        private const float EstimatedLeftGroupWidth = 380f;

        /// <summary>The original dropdown width (GUILayout.Width(110) in source).</summary>
        private const float OriginalDropdownWidth = 110f;

        // ----- reflection handles (resolved once) -----

        private static readonly Type s_GameViewType;
        private static readonly PropertyInfo s_BehaviorProp;    // PlayModeView.enterPlayModeBehavior
        private static readonly Type s_BehaviorEnumType;        // PlayModeView.EnterPlayModeBehavior
        private static readonly PropertyInfo s_ShowToolbarProp; // GameView.showToolbar
        private static readonly bool s_Ready;

        // ----- cached labels -----

        private static readonly string[] s_Labels =
            { "Play Focused", "Play Maximized", "Play Unfocused" };

        // ================================================================
        //  Initialisation
        // ================================================================

        static GameViewToolbarInjector()
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;

                s_GameViewType = asm.GetType("UnityEditor.GameView");
                var pmvType    = asm.GetType("UnityEditor.PlayModeView");
                if (s_GameViewType == null || pmvType == null) return;

                s_BehaviorProp = pmvType.GetProperty(
                    "enterPlayModeBehavior",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (s_BehaviorProp == null) return;

                s_BehaviorEnumType = s_BehaviorProp.PropertyType;
                if (s_BehaviorEnumType == null) return;

                s_ShowToolbarProp = s_GameViewType.GetProperty(
                    "showToolbar",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                s_Ready = true;

                // Event-driven injection: scan when a window receives focus
                // (new GameViews always receive focus on creation).
                EditorWindow.windowFocusChanged += OnWindowFocusChanged;

                // Re-scan when play mode changes (GameViews may be recreated).
                EditorApplication.playModeStateChanged += _ => ScheduleScan();

                // Initial scan after editor finishes loading.
                EditorApplication.delayCall += ScanAndInject;
            }
            catch
            {
                // Reflection failed — feature silently disabled.
            }
        }

        // ================================================================
        //  Event-driven scanning (replaces polling)
        // ================================================================

        private static void OnWindowFocusChanged()
        {
            if (!s_Ready) return;
            ScheduleScan();
        }

        /// <summary>
        /// Schedules a single <see cref="ScanAndInject"/> call on the next
        /// editor update frame. Multiple calls before the next frame
        /// collapse into one scan.
        /// </summary>
        private static bool s_ScanScheduled;
        private static void ScheduleScan()
        {
            if (s_ScanScheduled) return;
            s_ScanScheduled = true;
            EditorApplication.delayCall += () =>
            {
                s_ScanScheduled = false;
                ScanAndInject();
            };
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
        //  Injection
        // ================================================================

        private static void Inject(EditorWindow gameView, VisualElement root)
        {
            try
            {
                var weak = new WeakReference(gameView);

                var overlay = new IMGUIContainer(() =>
                {
                    try
                    {
                        if (weak.Target is EditorWindow target)
                            DrawDropdown(target);
                    }
                    catch { /* silent no-op */ }
                });

                overlay.name        = OverlayName;
                overlay.pickingMode = PickingMode.Position;

                overlay.style.position = Position.Absolute;
                overlay.style.top      = new StyleLength(OverlayTopOffset);
                overlay.style.height   = new StyleLength(OverlayHeight);
                overlay.style.width    = new StyleLength(OverlayWidth);

                // Initial position — will be recalculated dynamically.
                overlay.style.right = new StyleLength(CalculateRightOffset(root.resolvedStyle.width));

                // Recalculate position when the window resizes.
                // We listen on the overlay (not root) so the callback is
                // automatically removed when the overlay is removed from
                // the hierarchy — no leak across re-injections.
                overlay.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    try
                    {
                        // The overlay's own geometry doesn't change on window
                        // resize (it's absolutely positioned), so read the
                        // parent width instead.
                        var parentWidth = overlay.parent?.resolvedStyle.width ?? 0f;
                        if (parentWidth > 0f)
                            overlay.style.right = new StyleLength(
                                CalculateRightOffset(parentWidth));
                    }
                    catch { /* silent no-op */ }
                });

                // Also listen on root for the initial layout pass, since the
                // overlay may not have resolved dimensions yet.
                // Store the callback so RemoveAllOverlays can unregister it.
                EventCallback<GeometryChangedEvent> rootCallback = evt =>
                {
                    try
                    {
                        overlay.style.right = new StyleLength(
                            CalculateRightOffset(evt.newRect.width));
                    }
                    catch { /* silent no-op */ }
                };
                root.RegisterCallback(rootCallback);

                // If our overlay is removed (domain reload, window rebuild),
                // unregister the root callback and schedule a re-scan.
                overlay.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    root.UnregisterCallback(rootCallback);
                    ScheduleScan();
                });

                root.Add(overlay);
            }
            catch { /* silent no-op */ }
        }

        // ================================================================
        //  Dynamic positioning
        // ================================================================

        /// <summary>
        /// Calculates the <c>style.right</c> offset so the overlay sits on
        /// top of the original play-mode dropdown.
        ///
        /// <para>The GameView toolbar layout (from DoToolbarGUI source):</para>
        /// <code>
        /// [LeftGroup] [FlexSpace1] [Dropdown 110px] [MiddleButtons] [FlexSpace2] [RightGroup]
        /// </code>
        ///
        /// <para>The two FlexibleSpaces split remaining horizontal space equally.
        /// We know RightGroup and MiddleButtons widths exactly (deterministic).
        /// We estimate LeftGroup; any error there is halved by the flex split,
        /// and the overlay's extra width (140 vs 110 px) absorbs the remainder.</para>
        /// </summary>
        private static float CalculateRightOffset(float windowWidth)
        {
            // Guard: if the window is too narrow for the toolbar, use a
            // reasonable fallback so we don't get negative values.
            if (windowWidth < 400f)
                return 180f;

            float middleGroupWidth = OriginalDropdownWidth + MiddleButtonsWidth;
            float totalFixed = EstimatedLeftGroupWidth + middleGroupWidth + RightGroupWidth;
            float totalFlex  = Mathf.Max(0f, windowWidth - totalFixed);
            float eachFlex   = totalFlex / 2f;

            // The dropdown's right edge distance from the window's right edge:
            //   RightGroup + FlexSpace2 + MiddleButtons
            float rightOffset = RightGroupWidth + eachFlex + MiddleButtonsWidth;

            // Centre the wider overlay (140 px) over the original 110 px dropdown.
            // Shift left by half the width difference.
            rightOffset -= (OverlayWidth - OriginalDropdownWidth) / 2f;

            return Mathf.Max(0f, rightOffset);
        }

        // ================================================================
        //  Cleanup (called before assembly reload / package disable)
        // ================================================================

        /// <summary>
        /// Removes all injected overlay elements from every GameView's
        /// visual tree. Called by <see cref="FullscreenPlayController"/>
        /// before assembly reload to prevent stale delegates.
        /// </summary>
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
        //  Drawing
        // ================================================================

        private static void DrawDropdown(EditorWindow gameView)
        {
            int behavior        = GetBehavior(gameView);
            bool playFullscreen = FullscreenPlaySettings.PlayFullscreen;

            string label;
            if (playFullscreen)
                label = "Play Fullscreen";
            else if (behavior >= 0 && behavior < s_Labels.Length)
                label = s_Labels[behavior];
            else
                label = s_Labels[0];

            var rect = new Rect(0, 0, OverlayWidth, OverlayHeight);

            // The original dropdown is disabled while playing and not paused.
            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlaying && !EditorApplication.isPaused))
            {
                if (EditorGUI.DropdownButton(rect, new GUIContent(label),
                        FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    ShowMenu(gameView, behavior, playFullscreen);
                }
            }
        }

        private static void ShowMenu(
            EditorWindow gameView, int currentBehavior, bool playFullscreen)
        {
            var menu = new GenericMenu();

            for (int i = 0; i < s_Labels.Length; i++)
            {
                int idx = i;
                bool on = !playFullscreen && currentBehavior == i;
                menu.AddItem(new GUIContent(s_Labels[i]), on, () =>
                {
                    try
                    {
                        SetBehavior(gameView, idx);
                        FullscreenPlaySettings.PlayFullscreen = false;
                        gameView.Repaint();
                    }
                    catch { /* silent no-op */ }
                });
            }

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("Play Fullscreen"),
                playFullscreen,
                () =>
                {
                    FullscreenPlaySettings.PlayFullscreen = !playFullscreen;
                    gameView.Repaint();
                });

            menu.ShowAsContext();
        }

        // ================================================================
        //  Reflection helpers
        // ================================================================

        private static int GetBehavior(EditorWindow gameView)
        {
            try  { return (int)s_BehaviorProp.GetValue(gameView); }
            catch { return 0; }
        }

        private static void SetBehavior(EditorWindow gameView, int value)
        {
            try
            {
                s_BehaviorProp.SetValue(
                    gameView, Enum.ToObject(s_BehaviorEnumType, value));
            }
            catch { /* silent no-op */ }
        }

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
