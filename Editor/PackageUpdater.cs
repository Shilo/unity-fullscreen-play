using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Handles checking for and applying package updates via the GitHub
    /// Releases API and UPM Client API. Queries the latest GitHub Release
    /// to discover the newest tagged version, then installs that specific
    /// tag — ensuring users never receive unreleased HEAD commits.
    /// </summary>
    internal static class PackageUpdater
    {
        private const string PackageName = "com.shilo.fullscreen-play";
        private const string GitUrl = "https://github.com/Shilo/unity-fullscreen-play.git";
        private const string ReleaseApiUrl = "https://api.github.com/repos/Shilo/unity-fullscreen-play/releases/latest";
        private const string DialogTitle = "Fullscreen Play";

        private static ListRequest s_ListRequest;
        private static AddRequest s_AddRequest;
        private static UnityWebRequest s_WebRequest;
        private static string s_InstalledVersion;

        /// <summary>
        /// Checks for a newer version of the package and prompts the user to update.
        /// </summary>
        public static void CheckForUpdate()
        {
            if (s_ListRequest != null && !s_ListRequest.IsCompleted) return;
            if (s_AddRequest  != null && !s_AddRequest.IsCompleted)  return;
            if (s_WebRequest  != null && !s_WebRequest.isDone)       return;

            EditorUtility.DisplayProgressBar(DialogTitle, I18n.Tr("update_checking"), 0.2f);
            s_ListRequest = Client.List(offlineMode: false);
            EditorApplication.update += OnListRequestComplete;
        }

        private static void OnListRequestComplete()
        {
            if (!s_ListRequest.IsCompleted) return;
            EditorApplication.update -= OnListRequestComplete;

            if (s_ListRequest.Status == StatusCode.Failure)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_check_failed"), s_ListRequest.Error.message),
                    "OK");
                return;
            }

            string installedVersion = null;
            foreach (var pkg in s_ListRequest.Result)
            {
                if (pkg.name == PackageName)
                {
                    installedVersion = pkg.version;
                    break;
                }
            }

            if (installedVersion == null)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    I18n.Tr("update_not_found"),
                    "OK");
                return;
            }

            s_InstalledVersion = installedVersion;
            EditorUtility.DisplayProgressBar(DialogTitle, I18n.Tr("update_fetching"), 0.5f);

            s_WebRequest = UnityWebRequest.Get(ReleaseApiUrl);
            s_WebRequest.SetRequestHeader("User-Agent", "UnityEditor");
            s_WebRequest.SendWebRequest();
            EditorApplication.update += OnReleaseFetched;
        }

        private static void OnReleaseFetched()
        {
            if (!s_WebRequest.isDone) return;
            EditorApplication.update -= OnReleaseFetched;
            EditorUtility.ClearProgressBar();

            if (s_WebRequest.result != UnityWebRequest.Result.Success)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_check_failed"), s_WebRequest.error),
                    "OK");
                s_WebRequest.Dispose();
                s_WebRequest = null;
                return;
            }

            string tagName = ExtractTagName(s_WebRequest.downloadHandler.text);
            s_WebRequest.Dispose();
            s_WebRequest = null;

            if (tagName == null)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_check_failed"), "Could not parse release info."),
                    "OK");
                return;
            }

            // Tag format is "v0.5.0" — strip the leading "v" to get the version.
            string releaseVersion = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;

            if (releaseVersion == s_InstalledVersion)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_already_current"), s_InstalledVersion),
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                DialogTitle,
                string.Format(I18n.Tr("update_available"), releaseVersion, s_InstalledVersion),
                "Update",
                "Cancel");

            if (!confirmed) return;

            EditorUtility.DisplayProgressBar(DialogTitle, I18n.Tr("update_installing"), 0.8f);
            s_AddRequest = Client.Add(GitUrl + "#" + tagName);
            EditorApplication.update += OnAddRequestComplete;
        }

        private static void OnAddRequestComplete()
        {
            if (!s_AddRequest.IsCompleted) return;
            EditorApplication.update -= OnAddRequestComplete;
            EditorUtility.ClearProgressBar();

            if (s_AddRequest.Status == StatusCode.Failure)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_failed"), s_AddRequest.Error.message),
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                DialogTitle,
                string.Format(I18n.Tr("update_success"), s_AddRequest.Result.version),
                "OK");
        }

        /// <summary>
        /// Extracts the "tag_name" value from a GitHub Releases API JSON response.
        /// Uses simple string search to avoid requiring a full JSON parser.
        /// </summary>
        private static string ExtractTagName(string json)
        {
            const string key = "\"tag_name\":";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += key.Length;

            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++; // skip opening quote

            int end = json.IndexOf('"', idx);
            if (end < 0) return null;
            return json.Substring(idx, end - idx);
        }
    }
}
