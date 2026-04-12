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
        private const string DialogTitle = "Fullscreen Play";

        private static ListRequest s_ListRequest;
        private static AddRequest s_AddRequest;
        private static string s_InstalledVersion;

        /// <summary>
        /// Checks for a newer version of the package and prompts the user to update.
        /// </summary>
        public static void CheckForUpdate()
        {
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
                    DialogTitle,
                    string.Format(I18n.Tr("update_failed"), s_AddRequest.Error.message),
                    "OK");
                return;
            }

            var newVersion = s_AddRequest.Result.version;
            if (newVersion == s_InstalledVersion)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_already_current"), newVersion),
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    string.Format(I18n.Tr("update_success"), newVersion),
                    "OK");
            }
        }
    }
}
