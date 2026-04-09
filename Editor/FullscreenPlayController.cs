using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Central controller that wires up menu items, keyboard shortcuts,
    /// and play-mode state changes for the fullscreen feature.
    /// </summary>
    [InitializeOnLoad]
    internal static class FullscreenPlayController
    {
        private static FieldInfo s_GlobalEventHandlerField;

        static FullscreenPlayController()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            HookGlobalEventHandler();

            // Clean up all injected state before the next domain reload
            // (assembly unload, package disable, script recompilation).
            // This prevents stale delegates and visual-tree elements from
            // persisting into a domain that no longer contains our assembly.
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // Safety: clean up stale fullscreen state after domain reload
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                FullscreenGameView.Cleanup();
        }

        // ---- Cleanup ----

        private static void OnBeforeAssemblyReload()
        {
            // Remove our global event handler hook so the delegate doesn't
            // reference an unloaded assembly.
            UnhookGlobalEventHandler();

            // Close any open fullscreen window.
            if (FullscreenGameView.IsFullscreen)
                FullscreenGameView.ExitFullscreen();

            // Remove injected toolbar overlays from GameView visual trees.
            GameViewToolbarInjector.RemoveAllOverlays();
        }

        // ---- Play mode integration ----

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (FullscreenPlaySettings.PlayFullscreen)
                    {
                        // Delay one frame so the GameView is fully initialised
                        EditorApplication.delayCall += () =>
                        {
                            if (EditorApplication.isPlaying)
                                FullscreenGameView.EnterFullscreen();
                        };
                    }
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    if (FullscreenGameView.IsFullscreen)
                        FullscreenGameView.ExitFullscreen();
                    break;
            }
        }

        // ---- Menu items ----

        private const string MenuPlayFullscreen = "Edit/Fullscreen Play/Play Fullscreen";
        private const string MenuEnterNow = "Edit/Fullscreen Play/Enter Fullscreen Now";
        private const string MenuSettings = "Edit/Fullscreen Play/Settings...";

        [MenuItem(MenuPlayFullscreen, false, 160)]
        private static void TogglePlayFullscreen()
        {
            FullscreenPlaySettings.PlayFullscreen = !FullscreenPlaySettings.PlayFullscreen;
        }

        [MenuItem(MenuPlayFullscreen, true)]
        private static bool ValidateTogglePlayFullscreen()
        {
            Menu.SetChecked(MenuPlayFullscreen, FullscreenPlaySettings.PlayFullscreen);
            return true;
        }

        [MenuItem(MenuEnterNow, false, 161)]
        private static void EnterFullscreenNow()
        {
            if (EditorApplication.isPlaying)
            {
                FullscreenGameView.ToggleFullscreen();
            }
            else
            {
                // Not playing yet – enable the toggle and start Play mode.
                // The playModeStateChanged handler will enter fullscreen.
                FullscreenPlaySettings.PlayFullscreen = true;
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem(MenuSettings, false, 200)]
        private static void OpenSettings()
        {
            SettingsService.OpenUserPreferences("Preferences/Fullscreen Play");
        }

        // ---- Shortcut (F11 by default, rebindable via Edit > Shortcuts) ----

        [Shortcut("Fullscreen Play/Toggle Fullscreen", KeyCode.F11)]
        private static void ToggleFullscreenShortcut()
        {
            if (!FullscreenPlaySettings.EnableHotkey) return;
            if (!EditorApplication.isPlaying) return;

            FullscreenGameView.ToggleFullscreen();
        }

        // ---- Global event handler for Escape ----

        private static void HookGlobalEventHandler()
        {
            try
            {
                s_GlobalEventHandlerField = typeof(EditorApplication).GetField(
                    "globalEventHandler",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (s_GlobalEventHandlerField == null) return;

                var existing = (EditorApplication.CallbackFunction)
                    s_GlobalEventHandlerField.GetValue(null);
                existing += OnGlobalEvent;
                s_GlobalEventHandlerField.SetValue(null, existing);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[Fullscreen Play] Could not hook global event handler: {e.Message}");
            }
        }

        private static void UnhookGlobalEventHandler()
        {
            try
            {
                if (s_GlobalEventHandlerField == null) return;

                var existing = (EditorApplication.CallbackFunction)
                    s_GlobalEventHandlerField.GetValue(null);
                existing -= OnGlobalEvent;
                s_GlobalEventHandlerField.SetValue(null, existing);
            }
            catch
            {
                // Silent — we're shutting down anyway.
            }
        }

        private static void OnGlobalEvent()
        {
            if (!FullscreenGameView.IsFullscreen) return;

            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Escape)
            {
                FullscreenGameView.ExitFullscreen();
                e.Use();
            }
        }
    }
}
