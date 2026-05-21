using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class CommentGroupView : ISelectable
    {
        public bool IsSelected { get; set; }

        private Vector2 _fallbackPosition;
        private readonly List<StateView> _members = new();

        public string Label;
        public IReadOnlyList<StateView> Members => _members;

        private const float PadH = 20f;
        private const float PadTop = 30f;
        private const float PadBot = 15f;
        private const float CRadius = 12f;

        // Grayscale group colors
        private static readonly Color BgCol = new Color(0.18f, 0.18f, 0.18f, 0.25f);
        private static readonly Color BorderCol = new Color(0.40f, 0.40f, 0.40f, 0.35f);
        private static readonly Color SelBorderCol = new Color(0.70f, 0.70f, 0.70f, 0.80f);
        private static readonly Color HeaderCol = new Color(0.25f, 0.25f, 0.25f, 0.50f);
        private static readonly Color TextCol = new Color(0.80f, 0.80f, 0.80f);

        private static Texture2D _fillTex;
        private static int _fillRadius;
        private static Texture2D _borderTex;
        private static int _borderRadius;
        private static int _borderWidth;
        private static GUIStyle _fillStyle;
        private static GUIStyle _borderStyle;
        private static GUIStyle _labelStyle;

        public CommentGroupView(IEnumerable<StateView> members, string label = "Comment Group")
        {
            Label = label;
            _members.AddRange(members);
        }

        private Rect GetMembersBounds()
        {
            if (_members.Count == 0)
                return new Rect(_fallbackPosition.x, _fallbackPosition.y, 160f, 40f);

            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < _members.Count; i++)
            {
                Rect r = _members[i].GetGraphBounds();
                if (r.xMin < xMin) xMin = r.xMin;
                if (r.xMax > xMax) xMax = r.xMax;
                if (r.yMin < yMin) yMin = r.yMin;
                if (r.yMax > yMax) yMax = r.yMax;
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public Vector2 Position
        {
            get
            {
                Rect b = GetMembersBounds();
                return new Vector2(b.x - PadH, b.y - PadTop);
            }
            set
            {
                if (_members.Count == 0)
                {
                    _fallbackPosition = value;
                    return;
                }

                Vector2 current = Position;
                Vector2 delta = value - current;
                if (delta.sqrMagnitude < 0.0001f) return;

                for (int i = 0; i < _members.Count; i++)
                    _members[i].Position += delta;
            }
        }

        public Vector2 Size
        {
            get
            {
                Rect b = GetMembersBounds();
                return new Vector2(b.width + PadH * 2f, b.height + PadTop + PadBot);
            }
        }

        public Rect GetGraphBounds()
        {
            return new Rect(Position.x, Position.y, Size.x, Size.y);
        }

        public bool ContainsPoint(Vector2 p)
        {
            Rect b = GetGraphBounds();
            if (!b.Contains(p)) return false;

            float r = CRadius;
            if (p.x < b.x + r && p.y < b.y + r)
            {
                float dx = p.x - (b.x + r);
                float dy = p.y - (b.y + r);
                return dx * dx + dy * dy <= r * r;
            }
            if (p.x > b.xMax - r && p.y < b.y + r)
            {
                float dx = p.x - (b.xMax - r);
                float dy = p.y - (b.y + r);
                return dx * dx + dy * dy <= r * r;
            }
            if (p.x < b.x + r && p.y > b.yMax - r)
            {
                float dx = p.x - (b.x + r);
                float dy = p.y - (b.yMax - r);
                return dx * dx + dy * dy <= r * r;
            }
            if (p.x > b.xMax - r && p.y > b.yMax - r)
            {
                float dx = p.x - (b.xMax - r);
                float dy = p.y - (b.yMax - r);
                return dx * dx + dy * dy <= r * r;
            }
            return true;
        }

        public void Draw(float zoom, Vector2 panOffset)
        {
            Rect b = GetGraphBounds();
            Vector2 sp = b.position * zoom + panOffset;
            Vector2 ss = b.size * zoom;
            var sr = new Rect(sp.x, sp.y, ss.x, ss.y);

            int r = Mathf.Max(1, Mathf.RoundToInt(CRadius * zoom));

            EnsureFillTex(r);

            if (_fillStyle == null)
            {
                _fillStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };
                _labelStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = TextCol },
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(8, 4, 0, 0)
                };
            }

            _fillStyle.normal.background = _fillTex;
            _fillStyle.border = new RectOffset(r, r, r, r);
            GUI.Box(sr, "", _fillStyle);

            float headerH = Mathf.Max(1f, 24f * zoom);
            var headerRect = new Rect(sr.x, sr.y, sr.width, headerH);
            EditorGUI.DrawRect(headerRect, HeaderCol);

            _labelStyle.fontSize = Mathf.RoundToInt(11f * zoom);
            GUI.Box(headerRect, Label, _labelStyle);
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
            Rect b = GetGraphBounds();
            Vector2 sp = b.position * zoom + panOffset;
            Vector2 ss = b.size * zoom;
            var sr = new Rect(sp.x, sp.y, ss.x, ss.y);

            int r = Mathf.Max(1, Mathf.RoundToInt(CRadius * zoom));
            int bw = Mathf.Max(1, Mathf.RoundToInt(2f * zoom));

            EnsureBorderTex(r, bw);

            if (_borderStyle == null)
            {
                _borderStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };
            }

            _borderStyle.normal.background = _borderTex;
            _borderStyle.border = new RectOffset(r, r, r, r);
            GUI.color = IsSelected ? SelBorderCol : BorderCol;
            GUI.Box(sr, "", _borderStyle);
            GUI.color = Color.white;
        }

        private static void EnsureFillTex(int r)
        {
            if (_fillTex != null && _fillRadius == r) return;
            if (_fillTex != null) { Object.DestroyImmediate(_fillTex); _fillTex = null; }

            int s = r * 2 + 8;
            _fillTex = GenRoundedFill(s, s, r, BgCol);
            _fillTex.hideFlags = HideFlags.HideAndDontSave;
            _fillRadius = r;
        }

        private static void EnsureBorderTex(int r, int bw)
        {
            if (_borderTex != null && _borderRadius == r && _borderWidth == bw) return;
            if (_borderTex != null) { Object.DestroyImmediate(_borderTex); _borderTex = null; }

            int s = r * 2 + 8;
            _borderTex = GenRoundedBorder(s, s, r, bw, Color.white);
            _borderTex.hideFlags = HideFlags.HideAndDontSave;
            _borderRadius = r;
            _borderWidth = bw;
        }

        private static Texture2D GenRoundedFill(int w, int h, int r, Color fill)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color clear = Color.clear;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, IsInsideRoundedRect(x, y, w, h, r) ? fill : clear);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenRoundedBorder(int w, int h, int r, int bw, Color col)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color clear = Color.clear;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool outer = IsInsideRoundedRect(x, y, w, h, r);
                    if (!outer) { tex.SetPixel(x, y, clear); continue; }
                    bool inner = IsInsideRoundedRectInset(x, y, w, h, r, bw);
                    tex.SetPixel(x, y, inner ? clear : col);
                }
            tex.Apply();
            return tex;
        }

        private static bool IsInsideRoundedRect(int x, int y, int w, int h, int r)
        {
            if (r <= 0) return true;
            if (x < r && y < r) return Sq(r - x - 0.5f, r - y - 0.5f) <= r * r;
            if (x >= w - r && y < r) return Sq(x - (w - r) + 0.5f, r - y - 0.5f) <= r * r;
            if (x < r && y >= h - r) return Sq(r - x - 0.5f, y - (h - r) + 0.5f) <= r * r;
            if (x >= w - r && y >= h - r) return Sq(x - (w - r) + 0.5f, y - (h - r) + 0.5f) <= r * r;
            return true;
        }

        private static bool IsInsideRoundedRectInset(int x, int y, int w, int h, int r, int inset)
        {
            int l = inset, rt = w - inset, t = inset, b = h - inset;
            if (x < l || x >= rt || y < t || y >= b) return false;
            int ri = r - inset;
            if (ri <= 0) return true;
            if (x < l + ri && y < t + ri) return Sq((l + ri) - x - 0.5f, (t + ri) - y - 0.5f) <= ri * ri;
            if (x >= rt - ri && y < t + ri) return Sq(x - (rt - ri) + 0.5f, (t + ri) - y - 0.5f) <= ri * ri;
            if (x < l + ri && y >= b - ri) return Sq((l + ri) - x - 0.5f, y - (b - ri) + 0.5f) <= ri * ri;
            if (x >= rt - ri && y >= b - ri) return Sq(x - (rt - ri) + 0.5f, y - (b - ri) + 0.5f) <= ri * ri;
            return true;
        }

        private static float Sq(float a, float b) => a * a + b * b;
    }
}
