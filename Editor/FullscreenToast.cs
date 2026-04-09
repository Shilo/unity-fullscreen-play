using UnityEditor;
using UnityEngine;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Material Design 3 styled toast notification showing keycap hints for
    /// exiting fullscreen. Flat dark theme with anti-aliased rounded keycap
    /// badges. Non-interactive (click-through).
    /// </summary>
    internal class FullscreenToast : EditorWindow
    {
        private static FullscreenToast s_Instance;

        private double _startTime;
        private float _duration;
        private string[] _keys;

        // Cached GUI resources
        private GUIStyle _labelStyle;
        private GUIStyle _keyLabelStyle;
        private GUIStyle _keycapStyle;
        private static Texture2D s_KeycapTexture;

        // Animation
        private const float FadeStart = 0.65f;

        // Dimensions
        private const float ToastWidth = 340f;
        private const float ToastHeight = 48f;
        private const float TopOffset = 30f;

        // Keycap layout
        private const float KeyPadH = 10f;
        private const float KeyHeight = 26f;
        private const float KeySpacing = 6f;
        private const float TextKeyGap = 12f;

        // Keycap corner radius
        private const int KeyRadius = 6;

        // MD3 Dark theme palette
        private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        private static readonly Color KeycapBgColor = new Color(0.22f, 0.22f, 0.25f, 1f);
        private static readonly Color LabelTextColor = new Color(0.90f, 0.88f, 0.90f);
        private static readonly Color KeycapTextColor = new Color(0.78f, 0.76f, 0.81f);

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
                    var hwnd = FindWindowByTitle("FullscreenPlayToast");
                    if (hwnd != System.IntPtr.Zero)
                    {
                        FullscreenGameView.BringWindowToTop(hwnd);
                        MakeClickThrough(hwnd);
                    }
                }
                catch { /* silent */ }
            };
#endif

            EditorApplication.update += s_Instance.Tick;
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern System.IntPtr FindWindow(string lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(System.IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(System.IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(
            System.IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private static System.IntPtr FindWindowByTitle(string title)
        {
            return FindWindow(null, title);
        }

        private static void MakeClickThrough(System.IntPtr hwnd)
        {
            const int GWL_EXSTYLE = -20;
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;
            const uint LWA_ALPHA = 0x2;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA); // fully visible, click-through
        }
#endif

        /// <summary>
        /// Resets the toast timer so it replays from the beginning.
        /// If the toast is already hidden, re-shows it. If it's mid-fade,
        /// resets to fully opaque and restarts the duration.
        /// </summary>
        public static void ResetTimer(Rect screenRect)
        {
            if (s_Instance != null)
            {
                // Already showing — just reset the start time
                s_Instance._startTime = EditorApplication.timeSinceStartup;

#if UNITY_EDITOR_WIN
                try
                {
                    var hwnd = FindWindowByTitle("FullscreenPlayToast");
                    if (hwnd != System.IntPtr.Zero)
                        FullscreenGameView.BringWindowToTop(hwnd);
                }
                catch { /* silent */ }
#endif
            }
            else
            {
                // Toast was already hidden — re-show it
                Show(screenRect);
            }
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

        private void EnsureStyles()
        {
            if (s_KeycapTexture == null)
            {
                int size = KeyRadius * 2 + 2;
                s_KeycapTexture = CreateRoundedRectTexture(size, size, KeyRadius, Color.white);
            }

            if (_keycapStyle == null)
            {
                _keycapStyle = new GUIStyle
                {
                    normal = { background = s_KeycapTexture },
                    border = new RectOffset(KeyRadius, KeyRadius, KeyRadius, KeyRadius)
                };
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Normal
                };
            }

            if (_keyLabelStyle == null)
            {
                _keyLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Normal
                };
            }
        }

        private void OnGUI()
        {
            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            float t = Mathf.Clamp01((float)(elapsed / _duration));
            float alpha = t > FadeStart ? 1f - Mathf.InverseLerp(FadeStart, 1f, t) : 1f;

            EnsureStyles();

            var fullRect = new Rect(0, 0, position.width, position.height);

            // Background — flat dark surface
            EditorGUI.DrawRect(fullRect,
                new Color(BgColor.r, BgColor.g, BgColor.b, BgColor.a * alpha));

            // Calculate total content width for centering
            var labelContent = new GUIContent(I18n.Tr("exit_fullscreen"));
            float labelWidth = _labelStyle.CalcSize(labelContent).x;

            float keysWidth = 0f;
            foreach (var key in _keys)
            {
                keysWidth += _keyLabelStyle.CalcSize(new GUIContent(key)).x
                           + KeyPadH * 2f + KeySpacing;
            }
            keysWidth -= KeySpacing; // remove trailing spacing

            float totalWidth = labelWidth + TextKeyGap + keysWidth;
            float cursorX = (fullRect.width - totalWidth) / 2f;
            float contentY = (fullRect.height - KeyHeight) / 2f;

            // Label text
            _labelStyle.normal.textColor =
                new Color(LabelTextColor.r, LabelTextColor.g, LabelTextColor.b, alpha);
            GUI.Label(
                new Rect(cursorX, 0, labelWidth, fullRect.height),
                labelContent, _labelStyle);
            cursorX += labelWidth + TextKeyGap;

            // Keycap badges
            _keyLabelStyle.normal.textColor =
                new Color(KeycapTextColor.r, KeycapTextColor.g, KeycapTextColor.b, alpha);

            foreach (var key in _keys)
            {
                float keyTextWidth = _keyLabelStyle.CalcSize(new GUIContent(key)).x;
                float keyWidth = keyTextWidth + KeyPadH * 2f;
                var keyRect = new Rect(cursorX, contentY, keyWidth, KeyHeight);

                // Flat rounded keycap background via tinted white 9-slice texture
                GUI.color = new Color(
                    KeycapBgColor.r, KeycapBgColor.g, KeycapBgColor.b,
                    KeycapBgColor.a * alpha);
                GUI.Box(keyRect, GUIContent.none, _keycapStyle);
                GUI.color = Color.white;

                // Keycap label
                GUI.Label(keyRect, key, _keyLabelStyle);

                cursorX += keyWidth + KeySpacing;
            }
        }

        /// <summary>
        /// Creates a 9-slice-ready texture with anti-aliased rounded corners
        /// using a signed distance field.
        /// </summary>
        private static Texture2D CreateRoundedRectTexture(
            int width, int height, int radius, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            float hx = width * 0.5f;
            float hy = height * 0.5f;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    // Signed distance to rounded rectangle boundary
                    float dx = Mathf.Max(Mathf.Abs(px + 0.5f - hx) - hx + radius, 0f);
                    float dy = Mathf.Max(Mathf.Abs(py + 0.5f - hy) - hy + radius, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    // Anti-aliased edge (smoothstep over ~1px)
                    float aa = Mathf.Clamp01(0.5f - dist);

                    var c = color;
                    c.a *= aa;
                    tex.SetPixel(px, py, c);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private void OnDestroy()
        {
            EditorApplication.update -= Tick;
            if (s_Instance == this)
                s_Instance = null;
        }
    }
}
