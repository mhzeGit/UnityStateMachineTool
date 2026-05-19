using UnityEngine;

namespace CleanStateMachine
{
    public class StateView
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public string Name { get; set; }

        private static GUIStyle _style;
        private static Texture2D _cachedTexture;
        private static int _cachedCornerRadius;
        private static readonly Color FillColor = new Color(0.18f, 0.18f, 0.20f);
        private const float DefaultWidth = 160f;
        private const float DefaultHeight = 60f;
        private const int BaseCornerRadius = 8;

        public StateView(Vector2 position, string name = "State")
        {
            Position = position;
            Size = new Vector2(DefaultWidth, DefaultHeight);
            Name = name;
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
            _cachedTexture = GenerateTexture(texSize, texSize, cornerRadius);
            _cachedTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedCornerRadius = cornerRadius;
        }

        private static Texture2D GenerateTexture(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color transparent = Color.clear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = FillColor;

                    if (x < radius && y < radius)
                    {
                        float dx = radius - x - 0.5f;
                        float dy = radius - y - 0.5f;
                        if (dx * dx + dy * dy > radius * radius)
                            color = transparent;
                    }
                    else if (x >= width - radius && y < radius)
                    {
                        float dx = x - (width - radius) + 0.5f;
                        float dy = radius - y - 0.5f;
                        if (dx * dx + dy * dy > radius * radius)
                            color = transparent;
                    }
                    else if (x < radius && y >= height - radius)
                    {
                        float dx = radius - x - 0.5f;
                        float dy = y - (height - radius) + 0.5f;
                        if (dx * dx + dy * dy > radius * radius)
                            color = transparent;
                    }
                    else if (x >= width - radius && y >= height - radius)
                    {
                        float dx = x - (width - radius) + 0.5f;
                        float dy = y - (height - radius) + 0.5f;
                        if (dx * dx + dy * dy > radius * radius)
                            color = transparent;
                    }

                    tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
