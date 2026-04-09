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

        /// <summary>
        /// How often (in editor update ticks) we scan for new GameView
        /// instances that need injection. ~30 ticks ≈ 0.5 s at 60 fps.
        /// </summary>
        private const int ScanInterval = 30;

        // ----- reflection handles (resolved once) -----

        private static readonly Type s_GameViewType;
        private static readonly PropertyInfo s_BehaviorProp;   // PlayModeView.enterPlayModeBehavior
        private static readonly Type s_BehaviorEnumType;       // PlayModeView.EnterPlayModeBehavior
        private static readonly PropertyInfo s_ShowToolbarProp; // GameView.showToolbar
        private static readonly bool s_Ready;

        // ----- cached labels -----

        private static readonly string[] s_Labels =
            { "Play Focused", "Play Maximized", "Play Unfocused" };

        // ----- state -----

        private static int s_Tick;

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

                // Optional – used to skip the fullscreen GameView (toolbar hidden).
                s_ShowToolbarProp = s_GameViewType.GetProperty(
                    "showToolbar",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                s_Ready = true;
                EditorApplication.update += OnUpdate;
            }
            catch
            {
                // Reflection failed — feature silently disabled.
            }
        }

        // ================================================================
        //  Periodic scan
        // ================================================================

        private static void OnUpdate()
        {
            if (!s_Ready) return;
            if (++s_Tick % ScanInterval != 0) return;

            try
            {
                var views = Resources.FindObjectsOfTypeAll(s_GameViewType);
                foreach (var obj in views)
                {
                    try
                    {
                        var window = obj as EditorWindow;
                        if (window == null) continue;

                        // Skip the fullscreen popup (its toolbar is hidden).
                        if (IsToolbarHidden(window)) continue;

                        var root = window.rootVisualElement;
                        if (root == null) continue;
                        if (root.Q(OverlayName) != null) continue; // already injected

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

                // --- Positioning ---------------------------------------------------
                // The original dropdown is rendered by IMGUI inside the GameView
                // toolbar with GUILayout.Width(110). We overlay on top of it.
                //
                // In Unity 6.x the toolbar elements to the RIGHT of the dropdown are
                // (right-to-left): Gizmos (~65 px) · Stats (~35 px) · icon buttons
                // (~55 px) · VSync icon (~25 px) ≈ 180 px total.
                //
                // If Unity changes its toolbar layout these values will be wrong,
                // but the overlay will simply appear offset — still functional, and
                // the Edit menu / F11 shortcut always work as a fallback.
                // -----------------------------------------------------------------------
                overlay.style.position = Position.Absolute;
                overlay.style.top      = new StyleLength(1);
                overlay.style.height   = new StyleLength(19);
                overlay.style.width    = new StyleLength(130);
                overlay.style.right    = new StyleLength(180);

                root.Add(overlay);
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

            // Determine the collapsed label.
            string label;
            if (playFullscreen)
                label = "Play Fullscreen";
            else if (behavior >= 0 && behavior < s_Labels.Length)
                label = s_Labels[behavior];
            else
                label = s_Labels[0];

            var rect = new Rect(0, 0, 130, 19);

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

            // --- Original options ---
            for (int i = 0; i < s_Labels.Length; i++)
            {
                int idx  = i;
                bool on  = !playFullscreen && currentBehavior == i;
                menu.AddItem(new GUIContent(s_Labels[i]), on, () =>
                {
                    try
                    {
                        SetBehavior(gameView, idx);
                        FullscreenPlaySettings.PlayFullscreen = false;
                    }
                    catch { /* silent no-op */ }
                });
            }

            menu.AddSeparator("");

            // --- Our addition ---
            menu.AddItem(
                new GUIContent("Play Fullscreen"),
                playFullscreen,
                () => FullscreenPlaySettings.PlayFullscreen = !playFullscreen);

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
