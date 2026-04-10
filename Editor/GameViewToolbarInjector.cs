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
        //  Injection
        // ================================================================

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

        // ================================================================
        //  Drawing
        // ================================================================

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

        // ================================================================
        //  Cleanup
        // ================================================================

        /// <summary>
        /// Removes all injected button elements from every GameView's
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
