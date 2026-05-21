using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public static class UITheme
    {
        // Pure grayscale palette — clean, minimal, no saturation

        // Base surfaces
        public static readonly Color PanelBg = Gray(0.10f);
        public static readonly Color PanelHeaderBg = Gray(0.14f);
        public static readonly Color PanelBorder = Gray(0.18f);
        public static readonly Color SplitterBg = Gray(0.10f);
        public static readonly Color SplitterHover = Gray(0.28f);
        public static readonly Color SplitterActive = Gray(0.40f);

        // Row states
        public static readonly Color RowEven = Gray(0.11f);
        public static readonly Color RowOdd = Gray(0.105f);
        public static readonly Color RowSelected = Gray(0.20f);
        public static readonly Color RowBorder = Gray(0.16f);

        // Text
        public static readonly Color TextColor = Gray(0.92f);
        public static readonly Color TextSecondary = Gray(0.72f);
        public static readonly Color TextMuted = Gray(0.42f);

        // Accent (neutral — used sparingly)
        public static readonly Color Accent = Gray(0.65f);
        public static readonly Color AccentSoft = Gray(0.55f);
        public static readonly Color Mauve = Gray(0.75f);
        public static readonly Color Peach = Gray(0.70f);
        public static readonly Color Teal = Gray(0.80f);

        // Semantic (minimal — for indicators only)
        public static readonly Color Success = new Color(0.40f, 0.73f, 0.40f);
        public static readonly Color Warning = new Color(0.85f, 0.75f, 0.40f);
        public static readonly Color Error = new Color(0.85f, 0.35f, 0.35f);
        public static readonly Color Info = new Color(0.45f, 0.65f, 0.85f);

        // Fields & inputs
        public static readonly Color FieldBg = Gray(0.13f);
        public static readonly Color FieldBgFocused = Gray(0.16f);
        public static readonly Color ButtonColor = Gray(0.16f);
        public static readonly Color ButtonHover = Gray(0.22f);
        public static readonly Color IconColor = Gray(0.50f);
        public static readonly Color TypeBadgeBg = Gray(0.25f);
        public static readonly Color TypeBadgeText = Gray(0.90f);
        public static readonly Color RowFieldBg = Gray(0.10f);

        // Active / runtime indicators (keep subtle color for function)
        public static readonly Color ActiveStateGlow = new Color(0.47f, 0.67f, 0.90f, 0.40f);
        public static readonly Color ActiveConnection = new Color(0.30f, 0.80f, 0.70f, 1f);
        public static readonly Color ActiveConnectionWave = new Color(1f, 1f, 1f, 0.80f);

        // Layout constants
        public const float HeaderHeight = 30f;
        public const float SplitterWidth = 5f;
        public const float CollapsedWidth = 28f;
        public const float MinPanelWidth = 160f;
        public const float MaxPanelWidth = 600f;
        public const float DefaultPanelWidth = 240f;
        public const float RowHeight = 26f;
        public const float FooterHeight = 28f;
        public const float Padding = 6f;

        // Cached styles
        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _secondaryStyle;
        private static GUIStyle _closeButtonStyle;
        private static GUIStyle _collapsedTabStyle;
        private static GUIStyle _emptyStyle;
        private static GUIStyle _variableLabelStyle;
        private static GUIStyle _typeBadgeStyle;
        private static GUIStyle _rowFieldStyle;
        private static GUIStyle _infoBoxStyle;
        private static GUIStyle _deleteButtonStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _foldoutHeaderStyle;

        // Textures
        private static Texture2D _panelBgTex;
        private static Texture2D _fieldBgTex;
        private static Texture2D _rowFieldTex;
        private static Texture2D _fieldFocusedTex;

        private static Color Gray(float v)
        {
            return new Color(v, v, v);
        }

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
            if (_rowFieldTex == null)
            {
                _rowFieldTex = MakeTex(1, 1, RowFieldBg);
                _rowFieldTex.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_fieldFocusedTex == null)
            {
                _fieldFocusedTex = MakeTex(1, 1, FieldBgFocused);
                _fieldFocusedTex.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        // --- GUIStyle accessors ---

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
                        fontSize = 12,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(10, 0, 0, 0)
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
                        normal = { textColor = TextSecondary },
                        padding = new RectOffset(8, 0, 0, 0)
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
                        normal = { textColor = TextSecondary },
                        padding = new RectOffset(8, 0, 0, 0)
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
                        fontSize = 11,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(8, 0, 0, 0)
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
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = IconColor },
                        hover = { textColor = TextColor },
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _closeButtonStyle;
            }
        }

        public static GUIStyle DeleteButtonStyle
        {
            get
            {
                if (_deleteButtonStyle == null)
                {
                    _deleteButtonStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextMuted },
                        hover = { textColor = Error },
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _deleteButtonStyle;
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
                        fontSize = 10,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextSecondary },
                        hover = { textColor = TextColor },
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
                        fontSize = 11,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(4, 0, 0, 0),
                        clipping = TextClipping.Clip
                    };
                }
                return _variableLabelStyle;
            }
        }

        public static GUIStyle TypeBadgeStyle
        {
            get
            {
                if (_typeBadgeStyle == null)
                {
                    _typeBadgeStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 8,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TypeBadgeText },
                        padding = new RectOffset(4, 4, 0, 0)
                    };
                }
                return _typeBadgeStyle;
            }
        }

        public static GUIStyle RowFieldStyle
        {
            get
            {
                if (_rowFieldStyle == null)
                {
                    EnsureTextures();
                    _rowFieldStyle = new GUIStyle
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = TextColor, background = _rowFieldTex },
                        focused = { textColor = TextColor, background = _fieldFocusedTex },
                        active = { textColor = TextColor, background = _fieldFocusedTex },
                        hover = { textColor = TextColor, background = _rowFieldTex },
                        padding = new RectOffset(6, 2, 0, 0),
                        border = new RectOffset(1, 1, 1, 1),
                        clipping = TextClipping.Clip
                    };
                }
                return _rowFieldStyle;
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
                        normal = { textColor = TextMuted },
                        wordWrap = true
                    };
                }
                return _emptyStyle;
            }
        }

        public static GUIStyle InfoBoxStyle
        {
            get
            {
                if (_infoBoxStyle == null)
                {
                    _infoBoxStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        normal = { textColor = TextMuted },
                        wordWrap = true,
                        padding = new RectOffset(12, 12, 8, 8)
                    };
                }
                return _infoBoxStyle;
            }
        }

        public static GUIStyle ToolbarButtonStyle
        {
            get
            {
                if (_toolbarButtonStyle == null)
                {
                    EnsureTextures();
                    _toolbarButtonStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextSecondary, background = _rowFieldTex },
                        hover = { textColor = TextColor },
                        padding = new RectOffset(8, 8, 0, 0),
                        border = new RectOffset(2, 2, 2, 2)
                    };
                }
                return _toolbarButtonStyle;
            }
        }

        public static GUIStyle FoldoutHeaderStyle
        {
            get
            {
                if (_foldoutHeaderStyle == null)
                {
                    _foldoutHeaderStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextColor },
                        hover = { textColor = Accent },
                        padding = new RectOffset(8, 0, 0, 0)
                    };
                }
                return _foldoutHeaderStyle;
            }
        }

        // --- Helper draw methods ---

        public static void DrawPanelBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
        }

        public static void DrawHeaderBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelHeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), RowBorder);
        }

        public static void DrawSectionDivider(float y, float width)
        {
            EditorGUI.DrawRect(new Rect(8f, y, width - 16f, 1f), Gray(0.18f));
        }

        public static void DrawSmallButton(Rect rect, bool hover)
        {
            EditorGUI.DrawRect(rect, hover ? ButtonHover : ButtonColor);
            if (hover)
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.04f));
        }

        public static void DrawCard(Rect rect)
        {
            EditorGUI.DrawRect(rect, Gray(0.12f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Gray(0.17f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Gray(0.17f));
        }

        public static void DrawSmallCard(Rect rect)
        {
            EditorGUI.DrawRect(rect, Gray(0.115f));
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y, rect.width - 8f, 1f), Gray(0.16f));
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.yMax - 1f, rect.width - 8f, 1f), Gray(0.16f));
        }

        public static void DrawGroupLabel(Rect rect, string label)
        {
            Rect bg = new Rect(rect.x + 8f, rect.y + 4f, 4f, rect.height - 8f);
            EditorGUI.DrawRect(bg, Gray(0.45f));
            Rect textRect = new Rect(rect.x + 18f, rect.y, rect.width - 22f, rect.height);
            var style = new GUIStyle(SecondaryStyle) { fontSize = 10, fontStyle = FontStyle.Bold };
            GUI.Label(textRect, label, style);
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
