using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    internal enum FullscreenMode
    {
        FullscreenWindowed,
        ExclusiveFullscreen
    }

    /// <summary>
    /// Persistent settings stored in EditorPrefs.
    /// </summary>
    internal static class FullscreenPlaySettings
    {
        private const string Prefix = "FullscreenPlay.";

        public static bool PlayFullscreen
        {
            get => EditorPrefs.GetBool(Prefix + "PlayFullscreen", false);
            set => EditorPrefs.SetBool(Prefix + "PlayFullscreen", value);
        }

        public static FullscreenMode Mode
        {
            get => (FullscreenMode)EditorPrefs.GetInt(Prefix + "Mode", 0);
            set => EditorPrefs.SetInt(Prefix + "Mode", (int)value);
        }

        public static bool ShowToast
        {
            get => EditorPrefs.GetBool(Prefix + "ShowToast", true);
            set => EditorPrefs.SetBool(Prefix + "ShowToast", value);
        }

        public static float ToastDuration
        {
            get => EditorPrefs.GetFloat(Prefix + "ToastDuration", 3f);
            set => EditorPrefs.SetFloat(Prefix + "ToastDuration", value);
        }

        public static bool EnableHotkey
        {
            get => EditorPrefs.GetBool(Prefix + "EnableHotkey", true);
            set => EditorPrefs.SetBool(Prefix + "EnableHotkey", value);
        }
    }

    /// <summary>
    /// Settings UI in Edit > Preferences > Fullscreen Play.
    /// </summary>
    internal class FullscreenPlaySettingsProvider : SettingsProvider
    {
        public FullscreenPlaySettingsProvider()
            : base("Preferences/Fullscreen Play", SettingsScope.User)
        {
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);

                FullscreenPlaySettings.PlayFullscreen = EditorGUILayout.Toggle(
                    new GUIContent("Play Fullscreen",
                        "Automatically enter fullscreen when entering Play mode"),
                    FullscreenPlaySettings.PlayFullscreen);

                FullscreenPlaySettings.Mode = (FullscreenMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Fullscreen Mode",
                        "Fullscreen Windowed: borderless window covering the screen.\n" +
                        "Exclusive Fullscreen: takes over the display (Windows only)."),
                    FullscreenPlaySettings.Mode);

                if (FullscreenPlaySettings.Mode == FullscreenMode.ExclusiveFullscreen)
                {
#if !UNITY_EDITOR_WIN
                    EditorGUILayout.HelpBox(
                        "Exclusive Fullscreen is only supported on Windows. " +
                        "Falling back to Fullscreen Windowed on this platform.",
                        MessageType.Warning);
#else
                    EditorGUILayout.HelpBox(
                        "Exclusive Fullscreen changes the display resolution. " +
                        "Use with caution in the editor.",
                        MessageType.Info);
#endif
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Hotkey", EditorStyles.boldLabel);

                FullscreenPlaySettings.EnableHotkey = EditorGUILayout.Toggle(
                    new GUIContent("Enable F11 Hotkey",
                        "Toggle fullscreen with the F11 key during Play mode"),
                    FullscreenPlaySettings.EnableHotkey);

                if (FullscreenPlaySettings.EnableHotkey)
                {
                    EditorGUILayout.HelpBox(
                        "The hotkey is F11 by default. You can rebind it in\n" +
                        "Edit \u2192 Shortcuts \u2192 Fullscreen Play.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Toast Notification", EditorStyles.boldLabel);

                FullscreenPlaySettings.ShowToast = EditorGUILayout.Toggle(
                    new GUIContent("Show Toast",
                        "Show a brief overlay with exit instructions when entering fullscreen"),
                    FullscreenPlaySettings.ShowToast);

                using (new EditorGUI.DisabledGroupScope(!FullscreenPlaySettings.ShowToast))
                {
                    FullscreenPlaySettings.ToastDuration = EditorGUILayout.Slider(
                        new GUIContent("Toast Duration (s)",
                            "How long the toast notification is visible"),
                        FullscreenPlaySettings.ToastDuration, 1f, 10f);
                }
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new FullscreenPlaySettingsProvider
            {
                keywords = new[] { "fullscreen", "play", "game", "F11", "hotkey", "toast" }
            };
        }
    }
}
