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
        static FullscreenPlayController()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            HookGlobalEventHandler();

            // Safety: clean up stale fullscreen state after domain reload
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                FullscreenGameView.Cleanup();
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
                var field = typeof(EditorApplication).GetField(
                    "globalEventHandler",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (field == null) return;

                var existing = (EditorApplication.CallbackFunction)field.GetValue(null);
                existing += OnGlobalEvent;
                field.SetValue(null, existing);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[Fullscreen Play] Could not hook global event handler: {e.Message}");
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
