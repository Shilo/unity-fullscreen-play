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

            // Show toast notification (delayed one frame so it layers on top)
            if (FullscreenPlaySettings.ShowToast)
            {
                EditorApplication.delayCall += () =>
                {
                    if (!IsFullscreen) return;
                    FullscreenToast.Show(s_FullscreenRect);
                };
            }

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
        }

        private static void OnAppFocusChanged(bool focused)
        {
            if (!focused || !IsFullscreen) return;

            // Unity app just regained OS focus (user alt-tabbed back)
            if (FullscreenPlaySettings.ShowToast
                && FullscreenPlaySettings.ShowToastOnRefocus)
            {
                FullscreenToast.ResetTimer(s_FullscreenRect);
            }
        }

        /// <summary>
        /// Debounce guard: multiple systems can handle F11 (MenuItem shortcut,
        /// [Shortcut] attribute, globalEventHandler). Although globalEventHandler
        /// calls Event.Use() to consume the event, this guard prevents any
        /// residual double-trigger within the same frame from toggling twice.
        /// </summary>
        private static double s_LastToggleTime;

        public static void ToggleFullscreen()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - s_LastToggleTime < 0.1) return;
            s_LastToggleTime = now;

            if (IsFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        /// <summary>
        /// Called during domain reload or editor startup to clean up orphaned state.
        /// </summary>
        public static void Cleanup()
        {
            EditorApplication.focusChanged -= OnAppFocusChanged;
            if (s_FullscreenWindow != null)
            {
                try { s_FullscreenWindow.Close(); }
                catch { /* silent — window may already be destroyed */ }
            }
            s_FullscreenWindow = null;
            s_FullscreenRect = Rect.zero;
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
                var targetDisplayProp = GameViewType.GetProperty("targetDisplay",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetDisplayProp != null)
                {
                    var displayValue = targetDisplayProp.GetValue(sourceView);
                    targetDisplayProp.SetValue(fullscreenView, displayValue);
                }

                // Copy selected size index (resolution/aspect ratio)
                var selectedSizeIndexProp = GameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selectedSizeIndexProp != null)
                {
                    var sizeValue = selectedSizeIndexProp.GetValue(sourceView);
                    selectedSizeIndexProp.SetValue(fullscreenView, sizeValue);
                }
            }
            catch (Exception e)
            {
                // Non-critical: the fullscreen view will work with defaults
                Debug.LogWarning($"[Fullscreen Play] Could not copy GameView settings: {e.Message}");
            }
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
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
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

        /// <summary>
        /// Exposes GetForegroundWindow for use by FullscreenToast to find
        /// its own HWND and bring it above the fullscreen window.
        /// </summary>
        internal static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

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

        /// <summary>
        /// Brings the given HWND to the top of the z-order (above the
        /// fullscreen window) without pinning it permanently.
        /// Used to ensure the toast popup appears above the game.
        /// </summary>
        internal static void BringWindowToTop(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
#endif
    }
}
