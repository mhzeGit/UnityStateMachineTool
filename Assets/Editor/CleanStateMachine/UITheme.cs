using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public static class UITheme
    {
        public static readonly Color PanelBg = new Color(0.050f, 0.050f, 0.052f);
        public static readonly Color PanelHeaderBg = new Color(0.070f, 0.070f, 0.075f);
        public static readonly Color PanelBorder = new Color(0.10f, 0.10f, 0.12f);
        public static readonly Color SplitterBg = new Color(0.08f, 0.08f, 0.09f);
        public static readonly Color SplitterHover = new Color(0.25f, 0.28f, 0.32f);
        public static readonly Color RowEven = new Color(0.055f, 0.055f, 0.058f);
        public static readonly Color RowOdd = new Color(0.048f, 0.048f, 0.050f);
        public static readonly Color RowSelected = new Color(0.12f, 0.14f, 0.18f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color TextSecondary = new Color(0.55f, 0.55f, 0.58f);
        public static readonly Color Accent = new Color(0.35f, 0.50f, 0.70f);
        public static readonly Color FieldBg = new Color(0.10f, 0.10f, 0.12f);
        public static readonly Color ButtonColor = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color ButtonHover = new Color(0.18f, 0.20f, 0.25f);
        public static readonly Color IconColor = new Color(0.60f, 0.60f, 0.62f);

        public const float HeaderHeight = 28f;
        public const float SplitterWidth = 5f;
        public const float CollapsedWidth = 28f;
        public const float MinPanelWidth = 160f;
        public const float MaxPanelWidth = 400f;
        public const float DefaultPanelWidth = 220f;
        public const float RowHeight = 24f;
        public const float FooterHeight = 28f;
        public const float Padding = 4f;

        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _secondaryStyle;
        private static GUIStyle _closeButtonStyle;
        private static GUIStyle _collapsedTabStyle;
        private static GUIStyle _valueStyle;
        private static GUIStyle _emptyStyle;
        private static GUIStyle _variableLabelStyle;

        private static Texture2D _panelBgTex;
        private static Texture2D _fieldBgTex;

        private static void EnsureTextures()
        {
            if (_panelBgTex == null)
            {
                _panelBgTex = MakeTex(1, 1, PanelBg);
                _panelBgTex.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_fieldBgTex == null)
            {
                _fieldBgTex = MakeTex(1, 1, FieldBg);
                _fieldBgTex.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontStyle = FontStyle.Bold,
                        fontSize = 11,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(8, 0, 0, 0)
                    };
                }
                return _headerStyle;
            }
        }

        public static GUIStyle SectionStyle
        {
            get
            {
                if (_sectionStyle == null)
                {
                    _sectionStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontStyle = FontStyle.Bold,
                        fontSize = 10,
                        normal = { textColor = Accent },
                        padding = new RectOffset(6, 0, 0, 0)
                    };
                }
                return _sectionStyle;
            }
        }

        public static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 11,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(6, 0, 0, 0)
                    };
                }
                return _labelStyle;
            }
        }

        public static GUIStyle SecondaryStyle
        {
            get
            {
                if (_secondaryStyle == null)
                {
                    _secondaryStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 10,
                        normal = { textColor = TextSecondary },
                        padding = new RectOffset(6, 0, 0, 0)
                    };
                }
                return _secondaryStyle;
            }
        }

        public static GUIStyle CloseButtonStyle
        {
            get
            {
                if (_closeButtonStyle == null)
                {
                    _closeButtonStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        normal = { textColor = IconColor },
                        hover = { textColor = Color.white },
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _closeButtonStyle;
            }
        }

        public static GUIStyle CollapsedTabStyle
        {
            get
            {
                if (_collapsedTabStyle == null)
                {
                    _collapsedTabStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextSecondary },
                        hover = { textColor = Color.white },
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _collapsedTabStyle;
            }
        }

        public static GUIStyle VariableLabelStyle
        {
            get
            {
                if (_variableLabelStyle == null)
                {
                    _variableLabelStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 10,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(4, 0, 0, 0),
                        clipping = TextClipping.Clip
                    };
                }
                return _variableLabelStyle;
            }
        }

        public static GUIStyle EmptyStyle
        {
            get
            {
                if (_emptyStyle == null)
                {
                    _emptyStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        normal = { textColor = TextSecondary },
                        wordWrap = true
                    };
                }
                return _emptyStyle;
            }
        }

        public static void DrawPanelBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), PanelBorder);
        }

        public static void DrawSplitter(Rect rect, bool hover)
        {
            EditorGUI.DrawRect(rect, hover ? SplitterHover : SplitterBg);
        }

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            return tex;
        }
    }
}
