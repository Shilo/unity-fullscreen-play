using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Chrome-style toast notification that shows keycap hints for exiting
    /// fullscreen. Renders "Exit fullscreen [F11] [Esc]" with styled key
    /// caps and fades out after a configurable duration.
    /// </summary>
    internal class FullscreenToast : EditorWindow
    {
        private static FullscreenToast s_Instance;

        private double _startTime;
        private float _duration;
        private string[] _keys;
        private GUIStyle _labelStyle;
        private GUIStyle _keyStyle;

        private const float FadeStart = 0.65f;
        private const float ToastWidth = 360f;
        private const float ToastHeight = 40f;
        private const float TopOffset = 30f;

        // Keycap sizing
        private const float KeyPadH = 10f;  // horizontal padding inside keycap
        private const float KeyPadV = 4f;   // vertical padding inside keycap
        private const float KeySpacing = 6f; // space between keycaps
        private const float KeyHeight = 24f;
        private const float TextKeyGap = 14f; // gap between label text and first keycap

        public static void Show(Rect screenRect)
        {
            Hide();

            s_Instance = CreateInstance<FullscreenToast>();
            s_Instance._startTime = EditorApplication.timeSinceStartup;
            s_Instance._duration = FullscreenPlaySettings.ToastDuration;

            // Build key list
            if (FullscreenPlaySettings.EnableHotkey)
                s_Instance._keys = new[] { "F11", "Esc" };
            else
                s_Instance._keys = new[] { "Esc" };

            float x = screenRect.x + (screenRect.width - ToastWidth) / 2f;
            float y = screenRect.y + TopOffset;

            s_Instance.ShowPopup();
            s_Instance.position = new Rect(x, y, ToastWidth, ToastHeight);
            s_Instance.minSize = new Vector2(ToastWidth, ToastHeight);
            s_Instance.maxSize = new Vector2(ToastWidth, ToastHeight);

#if UNITY_EDITOR_WIN
            // Set a unique title so we can find the toast's own HWND
            s_Instance.titleContent = new GUIContent("FullscreenPlayToast");

            EditorApplication.delayCall += () =>
            {
                if (s_Instance == null) return;
                try
                {
                    // Find the toast HWND by its unique title — not
                    // GetForegroundWindow, which would return the GameView.
                    var hwnd = FindWindowByTitle("FullscreenPlayToast");
                    if (hwnd != System.IntPtr.Zero)
                        FullscreenGameView.BringWindowToTop(hwnd);
                }
                catch { /* silent */ }
            };
#endif

            EditorApplication.update += s_Instance.Tick;
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern System.IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static System.IntPtr FindWindowByTitle(string title)
        {
            return FindWindow(null, title);
        }
#endif

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

            // Background — dark, semi-transparent
            EditorGUI.DrawRect(fullRect, new Color(0.11f, 0.11f, 0.11f, 0.94f * alpha));

            // Border — subtle
            DrawBorder(fullRect, new Color(0.3f, 0.3f, 0.3f, 0.5f * alpha));

            // Ensure styles are cached
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Normal
                };
            }

            if (_keyStyle == null)
            {
                _keyStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
            }

            // Layout: [padding] [label text] [gap] [key1] [space] [key2] [padding]
            float contentY = (fullRect.height - KeyHeight) / 2f;
            float cursorX = 16f; // left padding

            // Label text
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, alpha);
            var labelContent = new GUIContent(I18n.Tr("exit_fullscreen"));
            float labelWidth = _labelStyle.CalcSize(labelContent).x;
            GUI.Label(new Rect(cursorX, 0, labelWidth, fullRect.height), labelContent, _labelStyle);
            cursorX += labelWidth + TextKeyGap;

            // Keycaps
            _keyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f, alpha);
            foreach (var key in _keys)
            {
                float keyTextWidth = _keyStyle.CalcSize(new GUIContent(key)).x;
                float keyWidth = keyTextWidth + KeyPadH * 2f;

                var keyRect = new Rect(cursorX, contentY, keyWidth, KeyHeight);

                // Keycap background — slightly lighter than toast bg
                EditorGUI.DrawRect(keyRect, new Color(0.22f, 0.22f, 0.22f, 0.95f * alpha));

                // Keycap border — lighter edge for 3D effect
                DrawBorder(keyRect, new Color(0.45f, 0.45f, 0.45f, 0.7f * alpha));

                // Bottom shadow line for depth
                EditorGUI.DrawRect(
                    new Rect(keyRect.x, keyRect.yMax - 2, keyRect.width, 2),
                    new Color(0.08f, 0.08f, 0.08f, 0.6f * alpha));

                // Key label
                GUI.Label(keyRect, key, _keyStyle);

                cursorX += keyWidth + KeySpacing;
            }
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
