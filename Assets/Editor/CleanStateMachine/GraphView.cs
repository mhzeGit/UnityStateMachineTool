using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class GraphView
    {
        private static readonly Color Bg = new Color(0.100f, 0.100f, 0.100f);
        private static readonly Color GridMinor = new Color(0.120f, 0.120f, 0.120f);
        private static readonly Color GridMajor = new Color(0.150f, 0.150f, 0.150f);
        private static readonly Color Crosshair = new Color(0.180f, 0.180f, 0.180f, 0.350f);

        private const float GridS = 20f;
        private const float GridL = 100f;
        private const float CrosshairSize = 4f;

        public void Draw(Rect rect, Vector2 panOffset, float zoom)
        {
            EditorGUI.DrawRect(rect, Bg);

            float thickness = Mathf.Max(1f, zoom);
            float sGridS = GridS * zoom;
            float sGridL = GridL * zoom;
            float sCross = CrosshairSize * zoom;

            DrawGridLines(rect, panOffset, sGridS, sGridL, thickness);
            DrawCrosshairs(rect, panOffset, sGridL, sCross);
        }

        private static void DrawGridLines(Rect rect, Vector2 panOffset, float sGridS, float sGridL, float thickness)
        {
            float ox = panOffset.x % sGridS;
            float oy = panOffset.y % sGridS;
            if (ox < 0f) ox += sGridS;
            if (oy < 0f) oy += sGridS;

            for (float x = ox; x < rect.width; x += sGridS)
                EditorGUI.DrawRect(new Rect(x, 0f, thickness, rect.height), GridMinor);
            for (float y = oy; y < rect.height; y += sGridS)
                EditorGUI.DrawRect(new Rect(0f, y, rect.width, thickness), GridMinor);

            ox = panOffset.x % sGridL;
            oy = panOffset.y % sGridL;
            if (ox < 0f) ox += sGridL;
            if (oy < 0f) oy += sGridL;

            for (float x = ox; x < rect.width; x += sGridL)
                EditorGUI.DrawRect(new Rect(x, 0f, thickness, rect.height), GridMajor);
            for (float y = oy; y < rect.height; y += sGridL)
                EditorGUI.DrawRect(new Rect(0f, y, rect.width, thickness), GridMajor);
        }

        private static void DrawCrosshairs(Rect rect, Vector2 panOffset, float sGridL, float sCross)
        {
            float ox = panOffset.x % sGridL;
            float oy = panOffset.y % sGridL;
            if (ox < 0f) ox += sGridL;
            if (oy < 0f) oy += sGridL;

            for (float x = ox; x < rect.width; x += sGridL)
            {
                for (float y = oy; y < rect.height; y += sGridL)
                {
                    EditorGUI.DrawRect(new Rect(x - sCross, y - 1f, sCross * 2f, 2f), Crosshair);
                    EditorGUI.DrawRect(new Rect(x - 1f, y - sCross, 2f, sCross * 2f), Crosshair);
                }
            }
        }
    }
}
