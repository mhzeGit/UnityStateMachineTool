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

        public void Draw(Rect rect, Vector2 panOffset)
        {
            EditorGUI.DrawRect(rect, Bg);

            Handles.BeginGUI();
            DrawGrid(rect, panOffset);
            Handles.EndGUI();
        }

        private void DrawGrid(Rect rect, Vector2 panOffset)
        {
            float ox = panOffset.x % GridS;
            float oy = panOffset.y % GridS;
            if (ox < 0f) ox += GridS;
            if (oy < 0f) oy += GridS;

            Handles.color = GridMinor;
            for (float x = ox; x < rect.width; x += GridS)
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, rect.height));
            for (float y = oy; y < rect.height; y += GridS)
                Handles.DrawLine(new Vector3(0f, y), new Vector3(rect.width, y));

            ox = panOffset.x % GridL;
            oy = panOffset.y % GridL;
            if (ox < 0f) ox += GridL;
            if (oy < 0f) oy += GridL;

            Handles.color = GridMajor;
            for (float x = ox; x < rect.width; x += GridL)
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, rect.height));
            for (float y = oy; y < rect.height; y += GridL)
                Handles.DrawLine(new Vector3(0f, y), new Vector3(rect.width, y));

            Handles.color = Crosshair;
            for (float x = ox; x < rect.width; x += GridL)
            {
                for (float y = oy; y < rect.height; y += GridL)
                {
                    var center = new Vector3(x, y, 0f);
                    Handles.DrawLine(center + Vector3.left * 4f, center + Vector3.right * 4f);
                    Handles.DrawLine(center + Vector3.down * 4f, center + Vector3.up * 4f);
                }
            }
        }
    }
}
