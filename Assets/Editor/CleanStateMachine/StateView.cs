using UnityEngine;

namespace CleanStateMachine
{
    public class StateView : ISelectable
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public string Name { get; set; }

        public bool IsSelected { get; set; }

        private static GUIStyle _style;
        private static GUIStyle _outlineStyle;
        private static Texture2D _cachedTexture;
        private static int _cachedCornerRadius;
        private static Texture2D _cachedOutlineTexture;
        private static int _cachedOutlineRadius;
        private static int _cachedOutlineBorderWidth;
        private static readonly Color FillColor = new Color(0.18f, 0.18f, 0.20f);
        private static readonly Color OutlineColor = Color.yellow;
        private const float DefaultWidth = 160f;
        private const float DefaultHeight = 60f;
        private const int BaseCornerRadius = 8;
        private const float OutlineBorderWidth = 2f;

        public StateView(Vector2 position, string name = "State")
        {
            Position = position;
            Size = new Vector2(DefaultWidth, DefaultHeight);
            Name = name;
        }

        public Vector2 GetCenter()
        {
            return new Vector2(Position.x + Size.x * 0.5f, Position.y + Size.y * 0.5f);
        }

        public Rect GetGraphBounds()
        {
            return new Rect(Position.x, Position.y, Size.x, Size.y);
        }

        public bool ContainsPoint(Vector2 graphPoint)
        {
            if (graphPoint.x < Position.x || graphPoint.x > Position.x + Size.x ||
                graphPoint.y < Position.y || graphPoint.y > Position.y + Size.y)
                return false;

            float r = BaseCornerRadius;

            if (graphPoint.x < Position.x + r && graphPoint.y < Position.y + r)
            {
                float dx = graphPoint.x - (Position.x + r);
                float dy = graphPoint.y - (Position.y + r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x > Position.x + Size.x - r && graphPoint.y < Position.y + r)
            {
                float dx = graphPoint.x - (Position.x + Size.x - r);
                float dy = graphPoint.y - (Position.y + r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x < Position.x + r && graphPoint.y > Position.y + Size.y - r)
            {
                float dx = graphPoint.x - (Position.x + r);
                float dy = graphPoint.y - (Position.y + Size.y - r);
                return dx * dx + dy * dy <= r * r;
            }

            if (graphPoint.x > Position.x + Size.x - r && graphPoint.y > Position.y + Size.y - r)
            {
                float dx = graphPoint.x - (Position.x + Size.x - r);
                float dy = graphPoint.y - (Position.y + Size.y - r);
                return dx * dx + dy * dy <= r * r;
            }

            return true;
        }

        public void Draw(float zoom, Vector2 panOffset)
        {
            if (_style == null)
            {
                _style = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(4, 4, 4, 4)
                };
            }

            int scaledRadius = Mathf.Max(1, Mathf.RoundToInt(BaseCornerRadius * zoom));
            EnsureTexture(scaledRadius);

            _style.normal.background = _cachedTexture;
            _style.border = new RectOffset(scaledRadius, scaledRadius, scaledRadius, scaledRadius);

            Vector2 screenPos = Position * zoom + panOffset;
            Vector2 scaledSize = Size * zoom;

            var rect = new Rect(screenPos.x, screenPos.y, scaledSize.x, scaledSize.y);

            _style.fontSize = Mathf.RoundToInt(12 * zoom);

            GUI.Box(rect, Name, _style);
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
            int scaledRadius = Mathf.Max(1, Mathf.RoundToInt(BaseCornerRadius * zoom));
            int borderWidth = Mathf.Max(1, Mathf.RoundToInt(OutlineBorderWidth * zoom));
            EnsureOutlineTexture(scaledRadius, borderWidth);

            if (_outlineStyle == null)
            {
                _outlineStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(4, 4, 4, 4)
                };
            }

            _outlineStyle.normal.background = _cachedOutlineTexture;
            _outlineStyle.border = new RectOffset(scaledRadius, scaledRadius, scaledRadius, scaledRadius);

            Vector2 screenPos = Position * zoom + panOffset;
            Vector2 scaledSize = Size * zoom;

            var rect = new Rect(screenPos.x, screenPos.y, scaledSize.x, scaledSize.y);

            GUI.Box(rect, "", _outlineStyle);
        }

        private static void EnsureTexture(int cornerRadius)
        {
            if (_cachedTexture != null && _cachedCornerRadius == cornerRadius)
                return;

            if (_cachedTexture != null)
            {
                Object.DestroyImmediate(_cachedTexture);
                _cachedTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedTexture = GenerateFilledTexture(texSize, texSize, cornerRadius, FillColor);
            _cachedTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedCornerRadius = cornerRadius;
        }

        private static void EnsureOutlineTexture(int cornerRadius, int borderWidth)
        {
            if (_cachedOutlineTexture != null && _cachedOutlineRadius == cornerRadius && _cachedOutlineBorderWidth == borderWidth)
                return;

            if (_cachedOutlineTexture != null)
            {
                Object.DestroyImmediate(_cachedOutlineTexture);
                _cachedOutlineTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedOutlineTexture = GenerateBorderTexture(texSize, texSize, cornerRadius, borderWidth, OutlineColor);
            _cachedOutlineTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedOutlineRadius = cornerRadius;
            _cachedOutlineBorderWidth = borderWidth;
        }

        private static Texture2D GenerateFilledTexture(int width, int height, int radius, Color fillColor)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color transparent = Color.clear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = IsInsideFilledRoundedRect(x, y, width, height, radius) ? fillColor : transparent;
                    tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateBorderTexture(int width, int height, int radius, int borderWidth, Color borderColor)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color transparent = Color.clear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool insideOuter = IsInsideFilledRoundedRect(x, y, width, height, radius);
                    if (!insideOuter)
                    {
                        tex.SetPixel(x, y, transparent);
                        continue;
                    }

                    bool insideInner = IsInsideFilledRoundedRectInset(x, y, width, height, radius, borderWidth);
                    tex.SetPixel(x, y, insideInner ? transparent : borderColor);
                }
            }

            tex.Apply();
            return tex;
        }

        private static bool IsInsideFilledRoundedRect(int x, int y, int w, int h, int r)
        {
            if (r <= 0)
                return true;

            if (x < r && y < r)
            {
                float dx = r - x - 0.5f;
                float dy = r - y - 0.5f;
                return dx * dx + dy * dy <= r * r;
            }

            if (x >= w - r && y < r)
            {
                float dx = x - (w - r) + 0.5f;
                float dy = r - y - 0.5f;
                return dx * dx + dy * dy <= r * r;
            }

            if (x < r && y >= h - r)
            {
                float dx = r - x - 0.5f;
                float dy = y - (h - r) + 0.5f;
                return dx * dx + dy * dy <= r * r;
            }

            if (x >= w - r && y >= h - r)
            {
                float dx = x - (w - r) + 0.5f;
                float dy = y - (h - r) + 0.5f;
                return dx * dx + dy * dy <= r * r;
            }

            return true;
        }

        private static bool IsInsideFilledRoundedRectInset(int x, int y, int w, int h, int r, int inset)
        {
            int left = inset;
            int right = w - inset;
            int top = inset;
            int bottom = h - inset;

            if (x < left || x >= right || y < top || y >= bottom)
                return false;

            int ri = r - inset;
            if (ri <= 0)
                return true;

            if (x < left + ri && y < top + ri)
            {
                float dx = (left + ri) - x - 0.5f;
                float dy = (top + ri) - y - 0.5f;
                return dx * dx + dy * dy <= ri * ri;
            }

            if (x >= right - ri && y < top + ri)
            {
                float dx = x - (right - ri) + 0.5f;
                float dy = (top + ri) - y - 0.5f;
                return dx * dx + dy * dy <= ri * ri;
            }

            if (x < left + ri && y >= bottom - ri)
            {
                float dx = (left + ri) - x - 0.5f;
                float dy = y - (bottom - ri) + 0.5f;
                return dx * dx + dy * dy <= ri * ri;
            }

            if (x >= right - ri && y >= bottom - ri)
            {
                float dx = x - (right - ri) + 0.5f;
                float dy = y - (bottom - ri) + 0.5f;
                return dx * dx + dy * dy <= ri * ri;
            }

            return true;
        }
    }
}
