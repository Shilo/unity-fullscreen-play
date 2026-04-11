using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Creates and manages a fullscreen borderless GameView window.
    /// </summary>
    internal static class FullscreenGameView
    {
        private static EditorWindow s_FullscreenWindow;
        private static Rect s_FullscreenRect;

        public static bool IsFullscreen => s_FullscreenWindow != null;

        public static Rect FullscreenRect => s_FullscreenRect;

        private static Type s_GameViewType;
        private static Type GameViewType
        {
            get
            {
                if (s_GameViewType == null)
                    s_GameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                return s_GameViewType;
            }
        }

        public static void EnterFullscreen()
        {
            if (IsFullscreen) return;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (GameViewType == null)
            {
                Debug.LogError("[Fullscreen Play] Cannot find UnityEditor.GameView type.");
                return;
            }

            // Calculate the target screen rect before creating the window
            s_FullscreenRect = GetTargetScreenRect();

            // Create a new GameView instance (separate from the user's existing one)
            s_FullscreenWindow = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
            if (s_FullscreenWindow == null)
            {
                Debug.LogError("[Fullscreen Play] Failed to create GameView instance.");
                return;
            }

            // Hide the GameView toolbar (display/resolution selectors) via reflection
            HideToolbar(s_FullscreenWindow);

            // ShowPopup creates a borderless, chromeless window
            s_FullscreenWindow.ShowPopup();
            s_FullscreenWindow.position = s_FullscreenRect;
            s_FullscreenWindow.Focus();

#if UNITY_EDITOR_WIN
            // Apply Win32 fullscreen styling SYNCHRONOUSLY — right after
            // ShowPopup + Focus, the window is the foreground window on this
            // same frame. Applying it here (not via delayCall) prevents the
            // one-frame flash where the taskbar and window gutters are visible.
            s_FullscreenWindow.titleContent = new GUIContent("FullscreenPlayPopup");
            MakeWindowFullscreen(s_FullscreenRect);
#endif

            // Copy target display and resolution settings from the existing GameView
            CopyGameViewSettings(s_FullscreenWindow);

            // Show toast notification (overlay on the fullscreen window)
            if (FullscreenPlaySettings.ShowToast)
                FullscreenToast.Show(s_FullscreenWindow);

            // Track fullscreen state in EditorPrefs so Cleanup() can find
            // orphaned popup windows after a hard exit (crash, Exit(0), etc.).
            EditorPrefs.SetBool("FullscreenPlay.Active", true);

            // Listen for app-level focus changes (alt-tab back) to re-show toast.
            // EditorApplication.focusChanged fires when the entire Unity app
            // gains/loses OS focus — NOT when clicking between editor windows.
            EditorApplication.focusChanged += OnAppFocusChanged;
        }

        public static void ExitFullscreen()
        {
            if (!IsFullscreen) return;

            EditorApplication.focusChanged -= OnAppFocusChanged;
            FullscreenToast.Hide();

#if UNITY_EDITOR_WIN
            RestoreWindow();
#endif

            if (s_FullscreenWindow != null)
            {
                s_FullscreenWindow.Close();
                s_FullscreenWindow = null;
            }

            s_FullscreenRect = Rect.zero;
            EditorPrefs.DeleteKey("FullscreenPlay.Active");
        }

        private static void OnAppFocusChanged(bool focused)
        {
            if (!focused || !IsFullscreen) return;

            // Unity app just regained OS focus (user alt-tabbed back)
            if (FullscreenPlaySettings.ShowToast
                && FullscreenPlaySettings.ShowToastOnRefocus)
            {
                FullscreenToast.ResetTimer(s_FullscreenWindow);
            }
        }

        public static void ToggleFullscreen()
        {
            if (IsFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        /// <summary>
        /// Called during domain reload or editor startup to clean up orphaned state.
        /// After a hard exit (crash, EditorApplication.Exit, etc.) the static
        /// s_FullscreenWindow reference is null but the popup window may still
        /// exist in the restored layout. The EditorPrefs flag lets us detect
        /// this and close the orphaned window.
        /// </summary>
        public static void Cleanup()
        {
            EditorApplication.focusChanged -= OnAppFocusChanged;
            FullscreenToast.Hide();

            if (s_FullscreenWindow != null)
            {
                try { s_FullscreenWindow.Close(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Fullscreen Play] Failed to close fullscreen window: {e.Message}");
                }
            }

            // Orphaned windows from a hard exit are closed by the update
            // loop and delayCall in FullscreenPlayController — NOT here.
            // Close() during [InitializeOnLoad] destroys the managed
            // EditorWindow but leaves the native window on screen, making
            // it unfindable by the time the proper cleanup runs.

            s_FullscreenWindow = null;
            s_FullscreenRect = Rect.zero;
            EditorPrefs.DeleteKey("FullscreenPlay.Active");
        }

        /// <summary>
        /// Finds GameView windows left over from a hard exit by looking for
        /// extra instances beyond the user's original docked Game tab.
        /// </summary>
        /// <returns>true if an orphan was found and closed.</returns>
        internal static bool CloseOrphanedFullscreenWindows()
        {
            try
            {
                if (GameViewType == null) return false;

                var allGameViews = Resources.FindObjectsOfTypeAll(GameViewType);
                if (allGameViews.Length <= 1) return false;

                // The orphaned popup sits at (0,0) covering the full screen.
                // Close any GameView whose position starts at the origin and
                // spans the full screen width — that's our leftover popup.
                var res = Screen.currentResolution;
                float scale = EditorGUIUtility.pixelsPerPoint;
                float screenW = res.width / scale;
                bool closed = false;

                foreach (var obj in allGameViews)
                {
                    var win = obj as EditorWindow;
                    if (win == null) continue;

                    var pos = win.position;
                    if (pos.x == 0 && pos.y == 0 && pos.width >= screenW)
                    {
                        Debug.Log("[Fullscreen Play] Closing orphaned fullscreen window from previous session.");
                        win.Close();
                        closed = true;
                    }
                }
                return closed;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Fullscreen Play] Orphan cleanup failed: {e.Message}");
                return false;
            }
        }

        private static Rect GetTargetScreenRect()
        {
            // Get the screen resolution - this gives the full desktop resolution
            // which is what we want for fullscreen windowed mode.
            var res = Screen.currentResolution;

            // Account for editor DPI scaling.
            // EditorWindow.position is in scaled (logical) coordinates,
            // while Screen.currentResolution is in physical pixels.
            float scale = EditorGUIUtility.pixelsPerPoint;
            float w = res.width / scale;
            float h = res.height / scale;

            return new Rect(0, 0, w, h);
        }

        private static void HideToolbar(EditorWindow gameView)
        {
            try
            {
                var showToolbarProp = GameViewType.GetProperty("showToolbar",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                showToolbarProp?.SetValue(gameView, false);
            }
            catch
            {
                // Non-critical: toolbar will be visible but functional
            }
        }

        private static void CopyGameViewSettings(EditorWindow fullscreenView)
        {
            try
            {
                // Find the existing GameView to copy settings from
                var existingViews = Resources.FindObjectsOfTypeAll(GameViewType);
                EditorWindow sourceView = null;
                foreach (var view in existingViews)
                {
                    if (view != fullscreenView)
                    {
                        sourceView = view as EditorWindow;
                        break;
                    }
                }

                if (sourceView == null) return;

                // Copy targetDisplay via reflection
                CopyProperty(sourceView, fullscreenView, "targetDisplay");

                // Copy selected size index (resolution/aspect ratio)
                CopyProperty(sourceView, fullscreenView, "selectedSizeIndex");

                // Copy Gizmos visibility state
                var playModeViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PlayModeView");
                if (playModeViewType != null)
                {
                    var isShowingGizmos = playModeViewType.GetMethod("IsShowingGizmos",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (isShowingGizmos != null)
                    {
                        bool gizmos = (bool)isShowingGizmos.Invoke(sourceView, null);
                        var gizmosField = GameViewType.GetField("m_Gizmos",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        gizmosField?.SetValue(fullscreenView, gizmos);

                        var setShowGizmos = playModeViewType.GetMethod("SetShowGizmos",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        setShowGizmos?.Invoke(fullscreenView, new object[] { gizmos });
                    }
                }

                // Copy VSync toggle state
                CopyField(sourceView, fullscreenView, "m_VSyncEnabled");

                // Copy Stats overlay state
                CopyField(sourceView, fullscreenView, "m_Stats");

                // Copy Low Resolution Aspect Ratios (array — clone to avoid shared reference)
                var lowResField = GameViewType.GetField("m_LowResolutionForAspectRatios",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (lowResField != null)
                {
                    var arr = lowResField.GetValue(sourceView) as bool[];
                    if (arr != null)
                        lowResField.SetValue(fullscreenView, (bool[])arr.Clone());
                }

                // Copy XR render mode (only relevant for XR/VR projects)
                CopyField(sourceView, fullscreenView, "m_XRRenderMode");

                // Copy "No Camera Warning" visibility
                CopyField(sourceView, fullscreenView, "m_NoCameraWarning");
            }
            catch (Exception e)
            {
                // Non-critical: the fullscreen view will work with defaults
                Debug.LogWarning($"[Fullscreen Play] Could not copy GameView settings: {e.Message}");
            }
        }

        /// <summary>
        /// Copies a property value between GameView instances via reflection.
        /// </summary>
        private static void CopyProperty(EditorWindow source, EditorWindow target, string propertyName)
        {
            var prop = GameViewType.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(target, prop.GetValue(source));
        }

        /// <summary>
        /// Copies a field value between GameView instances via reflection.
        /// Only safe for value types and immutable references — mutable
        /// reference types (e.g. arrays) need manual cloning.
        /// </summary>
        private static void CopyField(EditorWindow source, EditorWindow target, string fieldName)
        {
            var field = GameViewType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(target, field.GetValue(source));
        }

        // --- Windows-specific fullscreen helpers ---

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_STYLE = -16;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;

        private static IntPtr s_WindowHandle;

        /// <summary>
        /// Gets the native window handle for the fullscreen popup.
        /// Uses the window title set on the EditorWindow to find the
        /// correct HWND, avoiding the <c>GetForegroundWindow</c> race
        /// where another window could steal focus between frames.
        /// </summary>
        private static IntPtr GetPopupWindowHandle()
        {
            if (s_FullscreenWindow == null) return IntPtr.Zero;

            // Focus first to ensure the window is visible and foreground
            s_FullscreenWindow.Focus();

            // Use the window title to find the HWND reliably.
            // EditorWindow popup titles map to Win32 window titles.
            string title = s_FullscreenWindow.titleContent.text;
            if (!string.IsNullOrEmpty(title))
            {
                var hwnd = FindWindow(null, title);
                if (hwnd != IntPtr.Zero) return hwnd;
            }

            // Fallback: use GetForegroundWindow immediately after Focus().
            // Less reliable but works when the title approach fails.
            return GetForegroundWindow();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static void MakeWindowFullscreen(Rect rect)
        {
            s_WindowHandle = GetPopupWindowHandle();
            if (s_WindowHandle == IntPtr.Zero) return;

            // Set to popup style (removes all chrome) and position covering full screen
            SetWindowLong(s_WindowHandle, GWL_STYLE, WS_POPUP | WS_VISIBLE);

            float scale = EditorGUIUtility.pixelsPerPoint;
            int x = (int)(rect.x * scale);
            int y = (int)(rect.y * scale);
            int w = (int)(rect.width * scale);
            int h = (int)(rect.height * scale);

            // Use HWND_TOP (not HWND_TOPMOST) to place the window at the top
            // of the z-order without pinning it there permanently. This covers
            // the taskbar on initial show but allows alt-tab to bring other
            // windows in front.
            SetWindowPos(s_WindowHandle, HWND_TOP, x, y, w, h, SWP_SHOWWINDOW);
        }

        private static void RestoreWindow()
        {
            s_WindowHandle = IntPtr.Zero;
        }

#endif
    }
}
