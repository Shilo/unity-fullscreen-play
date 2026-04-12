using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// File-based internationalization. Loads JSON locale files from
    /// <c>Editor/Locales/{lang}.json</c> at domain load.
    ///
    /// <para>The editor language is detected via
    /// <see cref="LocalizationDatabase.currentEditorLanguage"/>.
    /// English (<c>en.json</c>) is always loaded as the fallback.
    /// The current language file is loaded on top, overriding
    /// matching keys.</para>
    ///
    /// <para><b>To add a new language:</b> create a new JSON file in
    /// <c>Editor/Locales/</c> named with the language code (e.g.
    /// <c>fr.json</c>, <c>ja.json</c>). Use <c>en.json</c> as a
    /// template. The file is picked up automatically.</para>
    /// </summary>
    internal static class I18n
    {
        private static readonly Dictionary<string, string> s_Strings =
            new Dictionary<string, string>();

        private static readonly Dictionary<SystemLanguage, string> s_LangCodes =
            new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.English,    "en" },
            { SystemLanguage.German,     "de" },
            { SystemLanguage.French,     "fr" },
            { SystemLanguage.Spanish,    "es" },
            { SystemLanguage.Italian,    "it" },
            { SystemLanguage.Portuguese, "pt" },
            { SystemLanguage.Russian,    "ru" },
            { SystemLanguage.Chinese,    "zh" },
            { SystemLanguage.Japanese,   "ja" },
            { SystemLanguage.Korean,     "ko" },
            { SystemLanguage.Dutch,      "nl" },
            { SystemLanguage.Polish,     "pl" },
            { SystemLanguage.Turkish,    "tr" },
            { SystemLanguage.Arabic,     "ar" },
        };

        static I18n()
        {
            try
            {
                string localesDir = FindLocalesDirectory();
                if (localesDir == null) return;

                // Always load English as the base/fallback
                LoadJson(Path.Combine(localesDir, "en.json"));

                // Detect the editor language. LocalizationDatabase is internal
                // in some Unity versions, so fall back to reading the pref directly.
                var lang = GetEditorLanguage();
                if (lang != SystemLanguage.English
                    && s_LangCodes.TryGetValue(lang, out var code))
                {
                    string langPath = Path.Combine(localesDir, code + ".json");
                    if (File.Exists(langPath))
                        LoadJson(langPath);
                }
            }
            catch
            {
                // Silent — English strings (if loaded) or key fallback
            }
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Falls back to the key itself if not found.
        /// </summary>
        public static string Tr(string key)
        {
            return s_Strings.TryGetValue(key, out var value) ? value : key;
        }

        private static SystemLanguage GetEditorLanguage()
        {
            try
            {
                // Try the public API first (available in some Unity versions)
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LocalizationDatabase");
                if (type != null)
                {
                    var prop = type.GetProperty("currentEditorLanguage",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (prop != null)
                        return (SystemLanguage)prop.GetValue(null);
                }
            }
            catch { /* silent */ }

            // Fallback: read the editor_language pref directly
            // This is an int matching the SystemLanguage enum
            try
            {
                int langInt = EditorPrefs.GetInt("editor_language", (int)SystemLanguage.English);
                return (SystemLanguage)langInt;
            }
            catch { return SystemLanguage.English; }
        }

        private static void LoadJson(string path)
        {
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);

            // Minimal JSON parser — handles flat { "key": "value" } objects.
            // Avoids dependency on JsonUtility (needs a wrapper class) or
            // third-party JSON libraries.
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return;

            // Remove outer braces
            json = json.Substring(1, json.Length - 2);

            // Split by commas, handling quoted strings
            int i = 0;
            while (i < json.Length)
            {
                string key = ReadJsonString(json, ref i);
                if (key == null) break;

                SkipWhitespace(json, ref i);
                if (i >= json.Length || json[i] != ':') break;
                i++; // skip ':'

                string value = ReadJsonString(json, ref i);
                if (value == null) break;

                s_Strings[key] = value;

                SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ',')
                    i++; // skip ','
            }
        }

        private static string ReadJsonString(string json, ref int i)
        {
            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i] != '"') return null;
            i++; // skip opening quote

            int start = i;
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\') i++; // skip escaped char
                i++;
            }

            if (i >= json.Length) return null;
            string raw = json.Substring(start, i - start);
            i++; // skip closing quote
            return UnescapeJsonString(raw);
        }

        private static string UnescapeJsonString(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"':  sb.Append('"');  i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case '/':  sb.Append('/');  i++; break;
                        case 'n':  sb.Append('\n'); i++; break;
                        case 'r':  sb.Append('\r'); i++; break;
                        case 't':  sb.Append('\t'); i++; break;
                        case 'b':  sb.Append('\b'); i++; break;
                        case 'f':  sb.Append('\f'); i++; break;
                        case 'u':
                            if (i + 5 < s.Length)
                            {
                                string hex = s.Substring(i + 2, 4);
                                if (int.TryParse(hex,
                                    System.Globalization.NumberStyles.HexNumber,
                                    null, out int codePoint))
                                {
                                    sb.Append((char)codePoint);
                                    i += 5;
                                    break;
                                }
                            }
                            sb.Append(s[i]);
                            break;
                        default:
                            sb.Append(s[i]);
                            break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        private static void SkipWhitespace(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        /// <summary>
        /// Finds the Locales directory relative to this package's Editor folder.
        /// Works both when installed via Git URL (PackageCache) and when
        /// embedded/local.
        /// </summary>
        private static string FindLocalesDirectory()
        {
            // Use the package path from UPM
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(I18n).Assembly);

            if (packageInfo != null)
            {
                string dir = Path.Combine(packageInfo.resolvedPath, "Editor", "Locales");
                if (Directory.Exists(dir))
                    return dir;
            }

            // Fallback: search relative to this script via CallerFilePath
            // (works in development/local packages)
            string[] guids = AssetDatabase.FindAssets("t:TextAsset en",
                new[] { "Packages/com.shilo.fullscreen-play/Editor/Locales" });
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return Path.GetDirectoryName(Path.GetFullPath(assetPath));
            }

            return null;
        }
    }
}
