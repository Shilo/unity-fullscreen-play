using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// A small borderless popup that shows "Press Esc or F11 to exit fullscreen"
    /// and fades out after a configurable duration.
    /// </summary>
    internal class FullscreenToast : EditorWindow
    {
        private static FullscreenToast s_Instance;

        private double _startTime;
        private float _duration;
        private string _message;
        private GUIStyle _labelStyle;

        private const float FadeStart = 0.65f; // start fading at 65% of duration
        private const float ToastWidth = 340f;
        private const float ToastHeight = 44f;
        private const float TopOffset = 30f;


        public static void Show(Rect screenRect)
        {
            Hide();

            s_Instance = CreateInstance<FullscreenToast>();
            s_Instance._startTime = EditorApplication.timeSinceStartup;
            s_Instance._duration = FullscreenPlaySettings.ToastDuration;

            string exitKeys = FullscreenPlaySettings.EnableHotkey ? "Esc  or  F11" : "Esc";
            s_Instance._message = $"Press  {exitKeys}  to exit fullscreen";

            float x = screenRect.x + (screenRect.width - ToastWidth) / 2f;
            float y = screenRect.y + TopOffset;

            s_Instance.ShowPopup();
            s_Instance.position = new Rect(x, y, ToastWidth, ToastHeight);
            s_Instance.minSize = new Vector2(ToastWidth, ToastHeight);
            s_Instance.maxSize = new Vector2(ToastWidth, ToastHeight);

#if UNITY_EDITOR_WIN
            // Bring the toast above the fullscreen window. We use delayCall
            // so the Win32 window has been created by the time we look it up.
            EditorApplication.delayCall += () =>
            {
                if (s_Instance == null) return;
                try
                {
                    s_Instance.Focus();
                    var hwnd = FullscreenGameView.GetForegroundWindowHandle();
                    FullscreenGameView.BringWindowToTop(hwnd);
                }
                catch { /* silent */ }
            };
#endif

            EditorApplication.update += s_Instance.Tick;
        }

        public static void Hide()
        {
            if (s_Instance != null)
            {
                EditorApplication.update -= s_Instance.Tick;
                s_Instance.Close();
                s_Instance = null;
            }
        }

        private void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            if (elapsed >= _duration)
            {
                Hide();
                return;
            }

            Repaint();
        }

        private void OnGUI()
        {
            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            float t = Mathf.Clamp01((float)(elapsed / _duration));

            float alpha = 1f;
            if (t > FadeStart)
                alpha = 1f - Mathf.InverseLerp(FadeStart, 1f, t);

            var fullRect = new Rect(0, 0, position.width, position.height);

            // Background
            EditorGUI.DrawRect(fullRect, new Color(0.12f, 0.12f, 0.12f, 0.92f * alpha));

            // Subtle border
            DrawBorder(fullRect, new Color(0.35f, 0.35f, 0.35f, 0.6f * alpha));

            // Text — cache the style, only update alpha each frame
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Normal
                };
            }
            _labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, alpha);

            GUI.Label(fullRect, _message, _labelStyle);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        private void OnDestroy()
        {
            EditorApplication.update -= Tick;
            if (s_Instance == this)
                s_Instance = null;
        }
    }
}
