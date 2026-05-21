using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public static class UITheme
    {
        // Pure grayscale palette — clean, minimal, no saturation

        // Base surfaces
        public static readonly Color PanelBg = Gray(0.12f);
        public static readonly Color PanelHeaderBg = Gray(0.15f);
        public static readonly Color PanelBorder = Gray(0.08f);
        public static readonly Color SplitterBg = Gray(0.12f);
        public static readonly Color SplitterHover = Gray(0.30f);
        public static readonly Color SplitterActive = Gray(0.45f);

        // Row states
        public static readonly Color RowEven = Gray(0.14f);
        public static readonly Color RowOdd = Gray(0.12f);
        public static readonly Color RowSelected = Gray(0.22f);
        public static readonly Color RowBorder = Gray(0.08f);
        public static readonly Color RowBoundary = Gray(0.25f);
        public static readonly Color RowBg = Gray(0.17f);
        public static readonly Color RowBgSelected = Gray(0.27f);
        public static readonly Color RowBgDrag = Gray(0.32f);
        public static readonly Color DropIndicator = Accent;

        // Text
        public static readonly Color TextColor = Gray(0.95f);
        public static readonly Color TextSecondary = Gray(0.75f);
        public static readonly Color TextMuted = Gray(0.50f);

        // Accent (neutral — used sparingly)
        public static readonly Color Accent = Gray(0.85f);
        public static readonly Color AccentSoft = Gray(0.65f);
        public static readonly Color Mauve = Gray(0.60f);
        public static readonly Color Peach = Gray(0.60f);
        public static readonly Color Teal = Gray(0.60f);

        // Semantic (minimal — for indicators only)
        public static readonly Color Success = Gray(0.6f);
        public static readonly Color Warning = Gray(0.6f);
        public static readonly Color Error = Gray(0.6f);
        public static readonly Color Info = Gray(0.6f);

        // Fields & inputs
        public static readonly Color FieldBg = Gray(0.09f);
        public static readonly Color FieldBgFocused = Gray(0.18f);
        public static readonly Color ButtonColor = Gray(0.20f);
        public static readonly Color ButtonHover = Gray(0.28f);
        public static readonly Color IconColor = Gray(0.60f);
        public static readonly Color TypeBadgeBg = Gray(0.22f);
        public static readonly Color TypeBadgeText = Gray(0.85f);
        public static readonly Color RowFieldBg = Gray(0.09f);

        // Active / runtime indicators (keep subtle color for function)
        public static readonly Color ActiveStateGlow = new Color(0.47f, 0.67f, 0.90f, 0.40f);
        public static readonly Color ActiveConnection = new Color(0.30f, 0.80f, 0.70f, 1f);
        public static readonly Color ActiveConnectionWave = new Color(1f, 1f, 1f, 0.80f);

        // Layout constants
        public const float HeaderHeight = 36f;
        public const float SplitterWidth = 4f;
        public const float CollapsedWidth = 32f;
        public const float MinPanelWidth = 220f;
        public const float MaxPanelWidth = 600f;
        public const float DefaultPanelWidth = 280f;
        public const float RowHeight = 32f;
        public const float FooterHeight = 30f;
        public const float Padding = 8f;

        // Cached styles
        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _secondaryStyle;
        private static GUIStyle _collapsedTabStyle;
        private static GUIStyle _emptyStyle;
        private static GUIStyle _variableLabelStyle;
        private static GUIStyle _typeBadgeStyle;
        private static GUIStyle _rowFieldStyle;
        private static GUIStyle _infoBoxStyle;
        private static GUIStyle _deleteButtonStyle;
        private static GUIStyle _rowNameFieldStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _foldoutHeaderStyle;
        private static GUIStyle _largeTitleStyle;

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
                        fontSize = 14,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(12, 0, 0, 0)
                    };
                }
                return _headerStyle;
            }
        }
        
        public static GUIStyle LargeTitleStyle
        {
            get
            {
                if (_largeTitleStyle == null)
                {
                    _largeTitleStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontStyle = FontStyle.Bold,
                        fontSize = 16,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(12, 0, 0, 0)
                    };
                }
                return _largeTitleStyle;
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
                        fontSize = 11,
                        normal = { textColor = TextSecondary },
                        padding = new RectOffset(10, 0, 0, 0)
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
                        fontSize = 12,
                        normal = { textColor = TextSecondary },
                        padding = new RectOffset(10, 0, 0, 0)
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
                        fontSize = 12,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(10, 0, 0, 0)
                    };
                }
                return _secondaryStyle;
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
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextMuted },
                        hover = { textColor = TextColor },
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
                        fontSize = 11,
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
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextColor },
                        padding = new RectOffset(6, 0, 0, 0),
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
                        fontSize = 9,
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
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = TextColor, background = _rowFieldTex },
                        focused = { textColor = TextColor, background = _fieldFocusedTex },
                        active = { textColor = TextColor, background = _fieldFocusedTex },
                        hover = { textColor = TextColor, background = _rowFieldTex },
                        padding = new RectOffset(8, 4, 0, 0),
                        border = new RectOffset(1, 1, 1, 1),
                        clipping = TextClipping.Clip
                    };
                }
                return _rowFieldStyle;
            }
        }

        public static GUIStyle RowNameFieldStyle
        {
            get
            {
                if (_rowNameFieldStyle == null)
                {
                    EnsureTextures();
                    _rowNameFieldStyle = new GUIStyle
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = TextColor, background = null },
                        focused = { textColor = TextColor, background = _fieldFocusedTex },
                        active = { textColor = TextColor, background = _fieldFocusedTex },
                        hover = { textColor = TextColor },
                        padding = new RectOffset(8, 4, 0, 0),
                        border = new RectOffset(1, 1, 1, 1),
                        clipping = TextClipping.Clip
                    };
                }
                return _rowNameFieldStyle;
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
                        fontSize = 12,
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
                        fontSize = 12,
                        normal = { textColor = TextMuted },
                        wordWrap = true,
                        padding = new RectOffset(16, 16, 12, 12)
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
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextSecondary, background = _rowFieldTex },
                        hover = { textColor = TextColor },
                        padding = new RectOffset(10, 10, 0, 0),
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
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextColor },
                        hover = { textColor = Accent },
                        padding = new RectOffset(10, 0, 0, 0)
                    };
                }
                return _foldoutHeaderStyle;
            }
        }

        // --- Helper draw methods ---

        public static void DrawPanelBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBg);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), PanelBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), PanelBorder);
        }

        public static void DrawHeaderBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelHeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), RowBorder);
        }

        public static void DrawSectionDivider(float y, float width)
        {
            EditorGUI.DrawRect(new Rect(12f, y, width - 24f, 1f), Gray(0.16f));
        }

        public static void DrawSmallButton(Rect rect, bool hover)
        {
            EditorGUI.DrawRect(rect, hover ? ButtonHover : ButtonColor);
            if (hover)
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.06f));
        }

        public static void DrawCard(Rect rect)
        {
            EditorGUI.DrawRect(rect, Gray(0.14f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Gray(0.20f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Gray(0.08f));
        }

        public static void DrawSmallCard(Rect rect)
        {
            EditorGUI.DrawRect(rect, Gray(0.13f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Gray(0.18f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Gray(0.08f));
        }

        public static void DrawPlusIcon(Rect rect, Color color)
        {
            Vector2 c = rect.center;
            float thickness = 2f;
            float armLen = Mathf.Min(rect.width, rect.height) * 0.3f;
            EditorGUI.DrawRect(new Rect(c.x - armLen, c.y - thickness * 0.5f, armLen * 2f, thickness), color);
            EditorGUI.DrawRect(new Rect(c.x - thickness * 0.5f, c.y - armLen, thickness, armLen * 2f), color);
        }

        public static void DrawArrowLeft(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float s = Mathf.Min(rect.width, rect.height) * 0.4f;
            Vector2 c = rect.center;
            float rightX = s * 0.5f;
            float leftX = s;
            var pts = new Vector3[]
            {
                new Vector3(c.x - leftX, c.y, 0f),
                new Vector3(c.x + rightX, c.y - s * 0.6f, 0f),
                new Vector3(c.x + rightX, c.y + s * 0.6f, 0f)
            };
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(pts);
            Handles.EndGUI();
        }

        public static void DrawArrowRight(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float s = Mathf.Min(rect.width, rect.height) * 0.4f;
            Vector2 c = rect.center;
            float leftX = s * 0.5f;
            float rightX = s;
            var pts = new Vector3[]
            {
                new Vector3(c.x + rightX, c.y, 0f),
                new Vector3(c.x - leftX, c.y - s * 0.6f, 0f),
                new Vector3(c.x - leftX, c.y + s * 0.6f, 0f)
            };
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(pts);
            Handles.EndGUI();
        }

        public static void DrawGroupLabel(Rect rect, string label)
        {
            Rect textRect = new Rect(rect.x + 12f, rect.y, rect.width - 24f, rect.height);
            var style = new GUIStyle(SectionStyle) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Gray(0.6f) } };
            GUI.Label(textRect, label.ToUpperInvariant(), style);
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
