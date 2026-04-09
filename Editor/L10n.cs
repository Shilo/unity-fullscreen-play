using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Minimal localization for user-facing strings. Language is detected
    /// once from <see cref="LocalizationDatabase.currentEditorLanguage"/>
    /// at domain load and cached. Falls back to English for unsupported
    /// languages.
    ///
    /// To add a new language, add entries to the <see cref="s_Table"/>
    /// dictionary for each key.
    /// </summary>
    internal static class L10n
    {
        private static readonly SystemLanguage s_Lang;

        private static readonly Dictionary<string, Dictionary<SystemLanguage, string>> s_Table =
            new Dictionary<string, Dictionary<SystemLanguage, string>>
        {
            ["exit_fullscreen"] = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Exit fullscreen" },
                { SystemLanguage.German,  "Vollbild beenden" },
            },
            ["play_focused"] = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Play Focused" },
                { SystemLanguage.German,  "Fokussiert abspielen" },
            },
            ["play_maximized"] = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Play Maximized" },
                { SystemLanguage.German,  "Maximiert abspielen" },
            },
            ["play_unfocused"] = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Play Unfocused" },
                { SystemLanguage.German,  "Unfokussiert abspielen" },
            },
            ["play_fullscreen"] = new Dictionary<SystemLanguage, string>
            {
                { SystemLanguage.English, "Play Fullscreen" },
                { SystemLanguage.German,  "Vollbild abspielen" },
            },
        };

        static L10n()
        {
            try
            {
                s_Lang = LocalizationDatabase.currentEditorLanguage;
            }
            catch
            {
                s_Lang = SystemLanguage.English;
            }
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Falls back to English if the current language has no translation.
        /// Returns the key itself if the key is not found.
        /// </summary>
        public static string Tr(string key)
        {
            if (!s_Table.TryGetValue(key, out var langs))
                return key;

            if (langs.TryGetValue(s_Lang, out var localized))
                return localized;

            if (langs.TryGetValue(SystemLanguage.English, out var fallback))
                return fallback;

            return key;
        }
    }
}
