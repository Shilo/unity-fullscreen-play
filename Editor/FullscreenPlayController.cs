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

        // ---- Menu items (Tools/ — standard for third-party plugins) ----

        private const string ToolsToggle    = "Tools/Fullscreen Play/Toggle Fullscreen";
        private const string ToolsAuto      = "Tools/Fullscreen Play/Auto-Fullscreen on Play";
        private const string ToolsSettings  = "Tools/Fullscreen Play/Settings...";

        // Legacy Edit/ menu entries kept for discoverability
        private const string EditAuto       = "Edit/Fullscreen Play/Play Fullscreen";
        private const string EditEnterNow   = "Edit/Fullscreen Play/Enter Fullscreen Now";
        private const string EditSettings   = "Edit/Fullscreen Play/Settings...";

        // --- Tools menu ---

        [MenuItem(ToolsToggle, false, 100)]
        private static void ToolsToggleFullscreen()
        {
            FullscreenGameView.ToggleFullscreen();
        }

        [MenuItem(ToolsToggle, true)]
        private static bool ValidateToolsToggleFullscreen()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(ToolsAuto, false, 101)]
        private static void ToolsToggleAuto()
        {
            FullscreenPlaySettings.PlayFullscreen = !FullscreenPlaySettings.PlayFullscreen;
        }

        [MenuItem(ToolsAuto, true)]
        private static bool ValidateToolsToggleAuto()
        {
            Menu.SetChecked(ToolsAuto, FullscreenPlaySettings.PlayFullscreen);
            return true;
        }

        [MenuItem(ToolsSettings, false, 200)]
        private static void ToolsOpenSettings()
        {
            SettingsService.OpenUserPreferences("Preferences/Fullscreen Play");
        }

        // --- Edit menu (fallback / discoverability) ---

        [MenuItem(EditAuto, false, 160)]
        private static void EditToggleAuto()
        {
            FullscreenPlaySettings.PlayFullscreen = !FullscreenPlaySettings.PlayFullscreen;
        }

        [MenuItem(EditAuto, true)]
        private static bool ValidateEditToggleAuto()
        {
            Menu.SetChecked(EditAuto, FullscreenPlaySettings.PlayFullscreen);
            return true;
        }

        [MenuItem(EditEnterNow, false, 161)]
        private static void EditEnterFullscreenNow()
        {
            FullscreenGameView.ToggleFullscreen();
        }

        [MenuItem(EditEnterNow, true)]
        private static bool ValidateEditEnterFullscreenNow()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(EditSettings, false, 200)]
        private static void EditOpenSettings()
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
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            // Escape: exit fullscreen (only when fullscreen is active)
            if (e.keyCode == KeyCode.Escape && FullscreenGameView.IsFullscreen)
            {
                FullscreenGameView.ExitFullscreen();
                e.Use();
                return;
            }

            // F11: toggle fullscreen during play mode.
            // The [Shortcut] attribute doesn't fire when the fullscreen
            // GameView captures keyboard input, so we handle it here too.
            if (e.keyCode == KeyCode.F11
                && FullscreenPlaySettings.EnableHotkey
                && EditorApplication.isPlaying)
            {
                FullscreenGameView.ToggleFullscreen();
                e.Use();
            }
        }
    }
}
