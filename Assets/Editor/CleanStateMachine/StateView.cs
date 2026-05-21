using UnityEngine;

namespace CleanStateMachine
{
    public class StateView : ISelectable
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public string Name { get; set; }

        public bool IsSelected { get; set; }
        public bool IsEntry { get; }
        public bool IsEditing { get; set; }
        public string EditingBuffer { get; set; }
        public StateClassData StateClass { get; set; }
        public bool IsActive { get; set; }

        private static GUIStyle _fillStyle;
        private static GUIStyle _borderStyle;
        private static GUIStyle _shadowStyle;
        private static GUIStyle _selectionStyle;
        private static GUIStyle _editStyle;
        private static GUIStyle _glowStyle;

        private static Texture2D _cachedFillTexture;
        private static int _cachedFillRadius;
        private static Texture2D _cachedShadowTexture;
        private static int _cachedShadowInnerRadius;
        private static Texture2D _cachedBorderTexture;
        private static int _cachedBorderRadius;
        private static int _cachedBorderWidth;
        private static Texture2D _cachedSelectionTexture;
        private static int _cachedSelectionRadius;
        private static int _cachedSelectionBorderWidth;

        private static Texture2D _cachedEntryFillTexture;
        private static int _cachedEntryFillRadius;
        private static Texture2D _cachedEntryBorderTexture;
        private static int _cachedEntryBorderRadius;
        private static int _cachedEntryBorderWidth;

        private static Texture2D _cachedGlowTexture;
        private static int _cachedGlowInnerRadius;

        // Grayscale node fill; color reserved for functional indicators (entry, selection)
        private static readonly Color FillColor = new Color(0.26f, 0.26f, 0.26f);
        private static readonly Color PermanentBorderColor = new Color(0.34f, 0.34f, 0.34f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.30f);
        private static readonly Color SelectionColor = new Color(0.537f, 0.706f, 0.980f);

        private static readonly Color EntryFillColor = new Color(0.302f, 0.502f, 0.302f);
        private static readonly Color EntryBorderColor = new Color(0.651f, 0.890f, 0.631f);

        private const float DefaultWidth = 160f;
        private const float DefaultHeight = 40f;
        private const int BaseCornerRadius = 8;
        private const float PermanentBorderWidth = 1.5f;
        private const float SelectionBorderWidth = 2f;
        private const float ShadowOffsetPx = 4f;
        private const float ShadowExpandPx = 6f;
        private const int ShadowBlurKernel = 3;
        private const float GlowExpandPx = 12f;
        private const float GlowPulseSpeed = 2.5f;
        private const int GlowBlurKernel = 4;

        public StateView(Vector2 position, string name = "State", bool isEntry = false)
        {
            Position = position;
            Size = new Vector2(DefaultWidth, DefaultHeight);
            Name = name;
            IsEntry = isEntry;
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
            if (_fillStyle == null)
            {
                _fillStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(4, 4, 4, 4)
                };
                _borderStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };
                _shadowStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };
            }

            int scaledRadius = Mathf.Max(1, Mathf.RoundToInt(BaseCornerRadius * zoom));
            int borderWidth = Mathf.Max(1, Mathf.RoundToInt(PermanentBorderWidth * zoom));
            int shadowExpand = Mathf.Max(1, Mathf.RoundToInt(ShadowExpandPx * zoom));

            EnsureShadowTexture(scaledRadius, shadowExpand);
            if (IsEntry)
            {
                EnsureEntryFillTexture(scaledRadius);
                EnsureEntryBorderTexture(scaledRadius, borderWidth);
            }
            else
            {
                EnsureFillTexture(scaledRadius);
                EnsureBorderTexture(scaledRadius, borderWidth, PermanentBorderColor);
            }

            var fillBorder = new RectOffset(scaledRadius, scaledRadius, scaledRadius, scaledRadius);
            int shadowRadius = scaledRadius + shadowExpand;
            var shadowBorder = new RectOffset(shadowRadius, shadowRadius, shadowRadius, shadowRadius);

            Vector2 screenPos = Position * zoom + panOffset;
            Vector2 scaledSize = Size * zoom;
            var rect = new Rect(screenPos.x, screenPos.y, scaledSize.x, scaledSize.y);

            float shadowExpandScreen = ShadowExpandPx * zoom;
            float shadowOffset = Mathf.Max(1f, ShadowOffsetPx * zoom);
            var shadowRect = new Rect(
                screenPos.x - shadowExpandScreen + shadowOffset,
                screenPos.y - shadowExpandScreen + shadowOffset,
                scaledSize.x + shadowExpandScreen * 2f,
                scaledSize.y + shadowExpandScreen * 2f
            );

            _shadowStyle.normal.background = _cachedShadowTexture;
            _shadowStyle.border = shadowBorder;
            GUI.Box(shadowRect, "", _shadowStyle);

            if (IsActive)
                DrawActiveGlow(zoom, panOffset, rect, scaledRadius);

            _fillStyle.normal.background = IsEntry ? _cachedEntryFillTexture : _cachedFillTexture;
            _fillStyle.border = fillBorder;
            _fillStyle.fontSize = Mathf.RoundToInt(12 * zoom);

            if (IsEditing)
            {
                GUI.Box(rect, "", _fillStyle);

                if (_editStyle == null)
                {
                    _editStyle = new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white },
                        focused = { textColor = Color.white },
                        padding = new RectOffset(4, 4, 4, 4),
                        border = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                        overflow = new RectOffset(0, 0, 0, 0),
                        wordWrap = false,
                        clipping = TextClipping.Clip
                    };
                }

                _editStyle.normal.background = _fillStyle.normal.background;
                _editStyle.focused.background = _fillStyle.normal.background;
                _editStyle.fontSize = _fillStyle.fontSize;
                _editStyle.border = fillBorder;

                GUI.SetNextControlName("StateRenameField");
                string newName = GUI.TextField(rect, EditingBuffer, _editStyle);
                if (newName != EditingBuffer)
                {
                    EditingBuffer = newName;
                }
            }
            else
            {
                GUI.Box(rect, Name, _fillStyle);
            }

            _borderStyle.normal.background = IsEntry ? _cachedEntryBorderTexture : _cachedBorderTexture;
            _borderStyle.border = fillBorder;
            GUI.Box(rect, "", _borderStyle);
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
            int scaledRadius = Mathf.Max(1, Mathf.RoundToInt(BaseCornerRadius * zoom));
            int borderWidth = Mathf.Max(1, Mathf.RoundToInt(SelectionBorderWidth * zoom));
            EnsureSelectionTexture(scaledRadius, borderWidth);

            if (_selectionStyle == null)
                _selectionStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };

            Vector2 screenPos = Position * zoom + panOffset;
            Vector2 scaledSize = Size * zoom;
            var rect = new Rect(screenPos.x, screenPos.y, scaledSize.x, scaledSize.y);

            _selectionStyle.normal.background = _cachedSelectionTexture;
            _selectionStyle.border = new RectOffset(scaledRadius, scaledRadius, scaledRadius, scaledRadius);
            GUI.Box(rect, "", _selectionStyle);
        }

        private void DrawActiveGlow(float zoom, Vector2 panOffset, Rect nodeRect, int scaledRadius)
        {
            if (_glowStyle == null)
                _glowStyle = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };

            float pulse = (Mathf.Sin((float)(Time.realtimeSinceStartup * GlowPulseSpeed)) + 1f) * 0.5f;

            float minExpand = 8f;
            float maxExpand = 16f;
            float expand = (minExpand + pulse * (maxExpand - minExpand)) * zoom;

            int expandInt = Mathf.Max(1, Mathf.RoundToInt(expand));
            EnsureGlowTexture(scaledRadius, expandInt);

            int glowRadius = scaledRadius + expandInt;
            var glowBorder = new RectOffset(glowRadius, glowRadius, glowRadius, glowRadius);

            var glowRect = new Rect(
                nodeRect.x - expand,
                nodeRect.y - expand,
                nodeRect.width + expand * 2f,
                nodeRect.height + expand * 2f
            );

            float minAlpha = 0.35f;
            float maxAlpha = 0.85f;
            float alpha = minAlpha + pulse * (maxAlpha - minAlpha);

            Color glowColor = UITheme.ActiveStateGlow;
            glowColor.a *= alpha;

            _glowStyle.normal.background = _cachedGlowTexture;
            _glowStyle.border = glowBorder;
            GUI.color = glowColor;
            GUI.Box(glowRect, "", _glowStyle);
            GUI.color = Color.white;
        }

        private static void EnsureGlowTexture(int innerRadius, int expand)
        {
            if (_cachedGlowTexture != null && _cachedGlowInnerRadius == innerRadius)
                return;

            if (_cachedGlowTexture != null)
            {
                Object.DestroyImmediate(_cachedGlowTexture);
                _cachedGlowTexture = null;
            }

            int radius = innerRadius + expand;
            int texSize = radius * 2 + 8;
            _cachedGlowTexture = GenerateGlowTexture(texSize, texSize, radius, Color.white, GlowBlurKernel);
            _cachedGlowTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedGlowInnerRadius = innerRadius;
        }

        private static void EnsureFillTexture(int cornerRadius)
        {
            if (_cachedFillTexture != null && _cachedFillRadius == cornerRadius)
                return;

            if (_cachedFillTexture != null)
            {
                Object.DestroyImmediate(_cachedFillTexture);
                _cachedFillTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedFillTexture = GenerateFilledTexture(texSize, texSize, cornerRadius, FillColor);
            _cachedFillTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedFillRadius = cornerRadius;
        }

        private static void EnsureShadowTexture(int innerRadius, int expand)
        {
            if (_cachedShadowTexture != null && _cachedShadowInnerRadius == innerRadius)
                return;

            if (_cachedShadowTexture != null)
            {
                Object.DestroyImmediate(_cachedShadowTexture);
                _cachedShadowTexture = null;
            }

            int radius = innerRadius + expand;
            int texSize = radius * 2 + 8;
            _cachedShadowTexture = GenerateSoftShadowTexture(texSize, texSize, radius, ShadowColor, ShadowBlurKernel);
            _cachedShadowTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedShadowInnerRadius = innerRadius;
        }

        private static void EnsureBorderTexture(int cornerRadius, int borderWidth, Color borderColor)
        {
            if (_cachedBorderTexture != null && _cachedBorderRadius == cornerRadius && _cachedBorderWidth == borderWidth)
                return;

            if (_cachedBorderTexture != null)
            {
                Object.DestroyImmediate(_cachedBorderTexture);
                _cachedBorderTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedBorderTexture = GenerateBorderTexture(texSize, texSize, cornerRadius, borderWidth, borderColor);
            _cachedBorderTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedBorderRadius = cornerRadius;
            _cachedBorderWidth = borderWidth;
        }

        private static void EnsureSelectionTexture(int cornerRadius, int borderWidth)
        {
            if (_cachedSelectionTexture != null && _cachedSelectionRadius == cornerRadius && _cachedSelectionBorderWidth == borderWidth)
                return;

            if (_cachedSelectionTexture != null)
            {
                Object.DestroyImmediate(_cachedSelectionTexture);
                _cachedSelectionTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedSelectionTexture = GenerateBorderTexture(texSize, texSize, cornerRadius, borderWidth, SelectionColor);
            _cachedSelectionTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedSelectionRadius = cornerRadius;
            _cachedSelectionBorderWidth = borderWidth;
        }

        private static void EnsureEntryFillTexture(int cornerRadius)
        {
            if (_cachedEntryFillTexture != null && _cachedEntryFillRadius == cornerRadius)
                return;

            if (_cachedEntryFillTexture != null)
            {
                Object.DestroyImmediate(_cachedEntryFillTexture);
                _cachedEntryFillTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedEntryFillTexture = GenerateFilledTexture(texSize, texSize, cornerRadius, EntryFillColor);
            _cachedEntryFillTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedEntryFillRadius = cornerRadius;
        }

        private static void EnsureEntryBorderTexture(int cornerRadius, int borderWidth)
        {
            if (_cachedEntryBorderTexture != null && _cachedEntryBorderRadius == cornerRadius && _cachedEntryBorderWidth == borderWidth)
                return;

            if (_cachedEntryBorderTexture != null)
            {
                Object.DestroyImmediate(_cachedEntryBorderTexture);
                _cachedEntryBorderTexture = null;
            }

            int texSize = cornerRadius * 2 + 8;
            _cachedEntryBorderTexture = GenerateBorderTexture(texSize, texSize, cornerRadius, borderWidth, EntryBorderColor);
            _cachedEntryBorderTexture.hideFlags = HideFlags.HideAndDontSave;
            _cachedEntryBorderRadius = cornerRadius;
            _cachedEntryBorderWidth = borderWidth;
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
                    float alpha = GetRoundedRectCoverage(x, y, width, height, radius);
                    Color color = fillColor;
                    color.a *= alpha;
                    tex.SetPixel(x, y, color);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateSoftShadowTexture(int width, int height, int radius, Color shadowColor, int blurKernel)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] src = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = GetRoundedRectCoverage(x, y, width, height, radius);
                    src[y * width + x] = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * alpha);
                }
            }

            if (blurKernel >= 3)
            {
                Color[] blurred = new Color[src.Length];
                int half = blurKernel / 2;
                float inv = 1f / (blurKernel * blurKernel);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float a = 0f;
                        for (int ky = 0; ky < blurKernel; ky++)
                        {
                            for (int kx = 0; kx < blurKernel; kx++)
                            {
                                int sx = Mathf.Clamp(x + kx - half, 0, width - 1);
                                int sy = Mathf.Clamp(y + ky - half, 0, height - 1);
                                a += src[sy * width + sx].a;
                            }
                        }
                        blurred[y * width + x] = new Color(0f, 0f, 0f, a * inv * shadowColor.a);
                    }
                }

                tex.SetPixels(blurred);
            }
            else
            {
                tex.SetPixels(src);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateGlowTexture(int width, int height, int radius, Color glowColor, int blurKernel)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] src = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = GetRoundedRectCoverage(x, y, width, height, radius);
                    src[y * width + x] = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * alpha);
                }
            }

            if (blurKernel >= 3)
            {
                Color[] blurred = new Color[src.Length];
                int half = blurKernel / 2;
                float inv = 1f / (blurKernel * blurKernel);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int ky = 0; ky < blurKernel; ky++)
                        {
                            for (int kx = 0; kx < blurKernel; kx++)
                            {
                                int sx = Mathf.Clamp(x + kx - half, 0, width - 1);
                                int sy = Mathf.Clamp(y + ky - half, 0, height - 1);
                                Color c = src[sy * width + sx];
                                r += c.r;
                                g += c.g;
                                b += c.b;
                                a += c.a;
                            }
                        }
                        blurred[y * width + x] = new Color(r * inv, g * inv, b * inv, a * inv);
                    }
                }

                tex.SetPixels(blurred);
            }
            else
            {
                tex.SetPixels(src);
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
                    float coverage = GetBorderCoverage(x, y, width, height, radius, borderWidth);
                    if (coverage <= 0f)
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                    else
                    {
                        Color color = borderColor;
                        color.a *= coverage;
                        tex.SetPixel(x, y, color);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        private static bool IsInsideFilledRoundedRect(float fx, float fy, int w, int h, int r)
        {
            if (r <= 0) return true;
            if (fx < r && fy < r)
            {
                float dx = r - fx;
                float dy = r - fy;
                return dx * dx + dy * dy <= r * r;
            }
            if (fx >= w - r && fy < r)
            {
                float dx = fx - (w - r);
                float dy = r - fy;
                return dx * dx + dy * dy <= r * r;
            }
            if (fx < r && fy >= h - r)
            {
                float dx = r - fx;
                float dy = fy - (h - r);
                return dx * dx + dy * dy <= r * r;
            }
            if (fx >= w - r && fy >= h - r)
            {
                float dx = fx - (w - r);
                float dy = fy - (h - r);
                return dx * dx + dy * dy <= r * r;
            }
            return true;
        }

        private static float GetRoundedRectCoverage(int x, int y, int w, int h, int r)
        {
            int count = 0;
            for (int sy = 0; sy < 3; sy++)
            {
                for (int sx = 0; sx < 3; sx++)
                {
                    float fx = x + (sx + 0.5f) / 3f;
                    float fy = y + (sy + 0.5f) / 3f;
                    if (IsInsideFilledRoundedRect(fx, fy, w, h, r))
                        count++;
                }
            }
            return count / 9f;
        }

        private static float GetBorderCoverage(int x, int y, int w, int h, int r, int inset)
        {
            int count = 0;
            for (int sy = 0; sy < 3; sy++)
            {
                for (int sx = 0; sx < 3; sx++)
                {
                    float fx = x + (sx + 0.5f) / 3f;
                    float fy = y + (sy + 0.5f) / 3f;
                    bool insideOuter = IsInsideFilledRoundedRect(fx, fy, w, h, r);
                    if (!insideOuter) continue;
                    bool insideInner = IsInsideFilledRoundedRectInset(fx, fy, w, h, r, inset);
                    if (!insideInner) count++;
                }
            }
            return count / 9f;
        }

        private static bool IsInsideFilledRoundedRectInset(float fx, float fy, int w, int h, int r, int inset)
        {
            int left = inset;
            int right = w - inset;
            int top = inset;
            int bottom = h - inset;
            if (fx < left || fx >= right || fy < top || fy >= bottom)
                return false;
            int ri = r - inset;
            if (ri <= 0) return true;
            if (fx < left + ri && fy < top + ri)
            {
                float dx = (left + ri) - fx;
                float dy = (top + ri) - fy;
                return dx * dx + dy * dy <= ri * ri;
            }
            if (fx >= right - ri && fy < top + ri)
            {
                float dx = fx - (right - ri);
                float dy = (top + ri) - fy;
                return dx * dx + dy * dy <= ri * ri;
            }
            if (fx < left + ri && fy >= bottom - ri)
            {
                float dx = (left + ri) - fx;
                float dy = fy - (bottom - ri);
                return dx * dx + dy * dy <= ri * ri;
            }
            if (fx >= right - ri && fy >= bottom - ri)
            {
                float dx = fx - (right - ri);
                float dy = fy - (bottom - ri);
                return dx * dx + dy * dy <= ri * ri;
            }
            return true;
        }
    }
}
