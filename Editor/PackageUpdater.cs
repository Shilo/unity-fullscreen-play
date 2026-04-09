using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Handles checking for and applying package updates via the UPM Client API.
    /// </summary>
    internal static class PackageUpdater
    {
        private const string PackageName = "com.shilo.fullscreen-play";
        private const string GitUrl = "https://github.com/Shilo/unity-fullscreen-play.git";

        private static ListRequest s_ListRequest;
        private static AddRequest s_AddRequest;

        /// <summary>
        /// Checks for a newer version of the package and prompts the user to update.
        /// </summary>
        public static void CheckForUpdate()
        {
            EditorUtility.DisplayProgressBar("Fullscreen Play", "Checking for updates...", 0.2f);
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
                    "Fullscreen Play",
                    $"Failed to check for updates:\n{s_ListRequest.Error.message}",
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
                    "Fullscreen Play",
                    "Could not find the installed package. It may have been embedded or installed manually.",
                    "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Fullscreen Play", "Fetching latest version...", 0.5f);
            s_AddRequest = Client.Add(GitUrl);
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
                    "Fullscreen Play",
                    $"Update failed:\n{s_AddRequest.Error.message}",
                    "OK");
                return;
            }

            var newVersion = s_AddRequest.Result.version;
            EditorUtility.DisplayDialog(
                "Fullscreen Play",
                $"Package updated to version {newVersion}.",
                "OK");
        }
    }
}
