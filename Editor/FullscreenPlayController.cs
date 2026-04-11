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
            EditorApplication.wantsToQuit += OnWantsToQuit;
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

        // ---- Quit interception ----

        /// <summary>
        /// Intercepts application-level quit shortcuts (Cmd+Q on macOS,
        /// Ctrl+Q on Linux, File > Quit) while fullscreen is active.
        /// Exits fullscreen and cancels the quit so the editor stays open.
        /// Note: Alt+F4 on Windows already closes just the popup window
        /// (native Win32 behavior) and does not trigger this callback.
        /// </summary>
        private static bool OnWantsToQuit()
        {
            if (FullscreenGameView.IsFullscreen)
            {
                Debug.Log("[Fullscreen Play] Quit intercepted — exiting fullscreen instead.");
                FullscreenGameView.ExitFullscreen();
                return false;
            }
            return true;
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
        private const string ToolsAuto      = "Tools/Fullscreen Play/Auto-Fullscreen on Play %#F11";
        private const string ToolsSettings  = "Tools/Fullscreen Play/Settings...";
        private const string ToolsUpdate    = "Tools/Fullscreen Play/Check for Update...";


        // --- Tools menu ---

        [MenuItem(ToolsAuto, false, 100)]
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

        [MenuItem(ToolsToggle, false, 101)]
        private static void ToolsToggleFullscreen()
        {
            // Guard needed because menu shortcuts bypass validation.
            if (!EditorApplication.isPlaying) return;
            FullscreenGameView.ToggleFullscreen();
        }

        [MenuItem(ToolsToggle, true)]
        private static bool ValidateToolsToggleFullscreen()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(ToolsUpdate, false, 200)]
        private static void ToolsCheckForUpdate()
        {
            PackageUpdater.CheckForUpdate();
        }

        [MenuItem(ToolsSettings, false, 201)]
        private static void ToolsOpenSettings()
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
