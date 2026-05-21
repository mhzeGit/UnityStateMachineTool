using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class GraphView
    {
        private static readonly Color Bg = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color GridMinor = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color GridMajor = new Color(0.20f, 0.20f, 0.20f);
        private const float GridS = 20f;
        private const float GridL = 100f;

        public void Draw(Rect rect, Vector2 panOffset, float zoom)
        {
            EditorGUI.DrawRect(rect, Bg);

            float thickness = Mathf.Max(1f, zoom);
            float sGridS = GridS * zoom;
            float sGridL = GridL * zoom;

            DrawGridLines(rect, panOffset, sGridS, sGridL, thickness);
        }

        private static void DrawGridLines(Rect rect, Vector2 panOffset, float sGridS, float sGridL, float thickness)
        {
            float ox = panOffset.x % sGridS;
            float oy = panOffset.y % sGridS;
            if (ox < 0f) ox += sGridS;
            if (oy < 0f) oy += sGridS;

            for (float x = rect.x + ox; x < rect.x + rect.width; x += sGridS)
                EditorGUI.DrawRect(new Rect(x, rect.y, thickness, rect.height), GridMinor);
            for (float y = rect.y + oy; y < rect.y + rect.height; y += sGridS)
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, thickness), GridMinor);

            ox = panOffset.x % sGridL;
            oy = panOffset.y % sGridL;
            if (ox < 0f) ox += sGridL;
            if (oy < 0f) oy += sGridL;

            for (float x = rect.x + ox; x < rect.x + rect.width; x += sGridL)
                EditorGUI.DrawRect(new Rect(x, rect.y, thickness, rect.height), GridMajor);
            for (float y = rect.y + oy; y < rect.y + rect.height; y += sGridL)
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, thickness), GridMajor);
        }
    }
}
