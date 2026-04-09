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

        public static bool ShowToastOnRefocus
        {
            get => EditorPrefs.GetBool(Prefix + "ShowToastOnRefocus", true);
            set => EditorPrefs.SetBool(Prefix + "ShowToastOnRefocus", value);
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
            // One-time migration: revert any stale ExclusiveFullscreen pref
            // saved before the feature was disabled.
            if (FullscreenPlaySettings.Mode == FullscreenMode.ExclusiveFullscreen)
                FullscreenPlaySettings.Mode = FullscreenMode.FullscreenWindowed;
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(I18n.Tr("settings_play_mode"), EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                bool playFullscreen = EditorGUILayout.Toggle(
                    new GUIContent(I18n.Tr("settings_play_fullscreen"),
                        I18n.Tr("settings_play_fullscreen_tooltip")),
                    FullscreenPlaySettings.PlayFullscreen);
                if (EditorGUI.EndChangeCheck())
                    FullscreenPlaySettings.PlayFullscreen = playFullscreen;

                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.EnumPopup(
                        new GUIContent(I18n.Tr("settings_fullscreen_mode"),
                            I18n.Tr("settings_fullscreen_mode_tooltip")),
                        FullscreenPlaySettings.Mode);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(I18n.Tr("settings_hotkey"), EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                bool enableHotkey = EditorGUILayout.Toggle(
                    new GUIContent(I18n.Tr("settings_enable_f11_hotkey"),
                        I18n.Tr("settings_enable_f11_tooltip")),
                    FullscreenPlaySettings.EnableHotkey);
                if (EditorGUI.EndChangeCheck())
                    FullscreenPlaySettings.EnableHotkey = enableHotkey;

                if (FullscreenPlaySettings.EnableHotkey)
                {
                    EditorGUILayout.HelpBox(
                        I18n.Tr("settings_hotkey_help"),
                        MessageType.Info);
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(I18n.Tr("settings_toast"), EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                bool showToast = EditorGUILayout.Toggle(
                    new GUIContent(I18n.Tr("settings_show_toast"),
                        I18n.Tr("settings_show_toast_tooltip")),
                    FullscreenPlaySettings.ShowToast);
                if (EditorGUI.EndChangeCheck())
                    FullscreenPlaySettings.ShowToast = showToast;

                using (new EditorGUI.DisabledGroupScope(!FullscreenPlaySettings.ShowToast))
                {
                    EditorGUI.BeginChangeCheck();
                    bool showOnRefocus = EditorGUILayout.Toggle(
                        new GUIContent(I18n.Tr("settings_show_on_refocus"),
                            I18n.Tr("settings_show_on_refocus_tooltip")),
                        FullscreenPlaySettings.ShowToastOnRefocus);
                    if (EditorGUI.EndChangeCheck())
                        FullscreenPlaySettings.ShowToastOnRefocus = showOnRefocus;

                    EditorGUI.BeginChangeCheck();
                    float toastDuration = EditorGUILayout.Slider(
                        new GUIContent(I18n.Tr("settings_toast_duration"),
                            I18n.Tr("settings_toast_duration_tooltip")),
                        FullscreenPlaySettings.ToastDuration, 1f, 10f);
                    if (EditorGUI.EndChangeCheck())
                        FullscreenPlaySettings.ToastDuration = toastDuration;
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
