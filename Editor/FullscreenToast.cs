using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shilo.FullscreenPlay.Editor
{
    /// <summary>
    /// Material Design 3 styled toast notification showing keycap hints for
    /// exiting fullscreen. Rendered as a VisualElement overlay on the fullscreen
    /// GameView — no separate window, so it never steals focus.
    /// </summary>
    internal class FullscreenToast
    {
        private static FullscreenToast s_Instance;

        public static bool IsVisible => s_Instance != null;

        private double _startTime;
        private float _duration;
        private VisualElement _root;

        // Animation
        private const float FadeStart = 0.65f;

        // Dimensions
        private const float ToastWidth = 340f;
        private const float ToastHeight = 48f;
        private const float TopOffset = 30f;
        private const int ToastRadius = 10;

        // Keycap layout
        private const float KeyPadH = 10f;
        private const float KeyHeight = 26f;
        private const float KeySpacing = 6f;
        private const float TextKeyGap = 12f;
        private const int KeyRadius = 6;

        // MD3 Dark theme palette
        private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        private static readonly Color KeycapBgColor = new Color(0.22f, 0.22f, 0.25f, 1f);
        private static readonly Color LabelTextColor = new Color(0.90f, 0.88f, 0.90f);
        private static readonly Color KeycapTextColor = new Color(0.78f, 0.76f, 0.81f);

        public static void Show(EditorWindow fullscreenWindow)
        {
            Hide();

            var instance = new FullscreenToast();
            instance._startTime = EditorApplication.timeSinceStartup;
            instance._duration = FullscreenPlaySettings.ToastDuration;
            instance.Build(fullscreenWindow);

            EditorApplication.update += instance.Tick;
            s_Instance = instance;
        }

        public static void Hide()
        {
            if (s_Instance != null)
            {
                EditorApplication.update -= s_Instance.Tick;
                s_Instance._root?.RemoveFromHierarchy();
                s_Instance = null;
            }
        }

        /// <summary>
        /// Resets the toast timer so it replays from the beginning.
        /// If the toast is already hidden, re-shows it. If it's mid-fade,
        /// resets to fully opaque and restarts the duration.
        /// </summary>
        public static void ResetTimer(EditorWindow fullscreenWindow)
        {
            if (s_Instance != null)
            {
                s_Instance._startTime = EditorApplication.timeSinceStartup;
            }
            else
            {
                Show(fullscreenWindow);
            }
        }

        private void Build(EditorWindow window)
        {
            // Full-screen transparent overlay (non-interactive)
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.alignItems = Align.Center;

            // Toast bar
            var toast = new VisualElement { pickingMode = PickingMode.Ignore };
            toast.style.marginTop = TopOffset;
            toast.style.width = ToastWidth;
            toast.style.height = ToastHeight;
            toast.style.backgroundColor = BgColor;
            toast.style.borderTopLeftRadius = ToastRadius;
            toast.style.borderTopRightRadius = ToastRadius;
            toast.style.borderBottomLeftRadius = ToastRadius;
            toast.style.borderBottomRightRadius = ToastRadius;
            toast.style.flexDirection = FlexDirection.Row;
            toast.style.alignItems = Align.Center;
            toast.style.justifyContent = Justify.Center;

            // "Exit fullscreen" label
            var label = new Label(I18n.Tr("exit_fullscreen"))
            {
                pickingMode = PickingMode.Ignore
            };
            label.style.color = LabelTextColor;
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.marginRight = TextKeyGap;
            toast.Add(label);

            // Keycap badges
            string[] keys = FullscreenPlaySettings.EnableHotkey
                ? new[] { "F11", "Esc" }
                : new[] { "Esc" };

            for (int i = 0; i < keys.Length; i++)
            {
                var keycap = new VisualElement { pickingMode = PickingMode.Ignore };
                keycap.style.backgroundColor = KeycapBgColor;
                keycap.style.borderTopLeftRadius = KeyRadius;
                keycap.style.borderTopRightRadius = KeyRadius;
                keycap.style.borderBottomLeftRadius = KeyRadius;
                keycap.style.borderBottomRightRadius = KeyRadius;
                keycap.style.paddingLeft = KeyPadH;
                keycap.style.paddingRight = KeyPadH;
                keycap.style.height = KeyHeight;
                keycap.style.justifyContent = Justify.Center;
                if (i > 0)
                    keycap.style.marginLeft = KeySpacing;

                var keyLabel = new Label(keys[i]) { pickingMode = PickingMode.Ignore };
                keyLabel.style.color = KeycapTextColor;
                keyLabel.style.fontSize = 11;
                keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                keyLabel.style.marginTop = 0;
                keyLabel.style.marginBottom = 0;
                keyLabel.style.marginLeft = 0;
                keyLabel.style.marginRight = 0;
                keyLabel.style.paddingTop = 0;
                keyLabel.style.paddingBottom = 0;
                keyLabel.style.paddingLeft = 0;
                keyLabel.style.paddingRight = 0;
                keycap.Add(keyLabel);

                toast.Add(keycap);
            }

            _root.Add(toast);
            window.rootVisualElement.Add(_root);
        }

        private void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            if (elapsed >= _duration)
            {
                Hide();
                return;
            }

            float t = Mathf.Clamp01((float)(elapsed / _duration));
            float alpha = t > FadeStart ? 1f - Mathf.InverseLerp(FadeStart, 1f, t) : 1f;
            if (_root != null)
                _root.style.opacity = alpha;
        }
    }
}
