using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConnectionView : ISelectable
    {
        public StateView From { get; }
        public StateView To { get; }
        public bool IsSelected { get; set; }

        private static readonly Color ConnectionColor = new Color(0.60f, 0.80f, 1.00f, 0.90f);
        private static readonly Color SelectedColor = new Color(0.80f, 0.92f, 1.00f, 1.00f);
        private const float HitTestThreshold = 10f;
        private const float BaseWidth = 5f;
        private const float SelectedBaseWidth = 7f;

        public ConnectionView(StateView from, StateView to)
        {
            From = from;
            To = to;
        }

        public Vector2 Position
        {
            get => GetGraphBounds().position;
            set { }
        }

        public Vector2 Size => GetGraphBounds().size;

        public Rect GetGraphBounds()
        {
            Vector3 from = From.GetCenter();
            Vector3 to = To.GetCenter();

            float minX = Mathf.Min(from.x, to.x);
            float maxX = Mathf.Max(from.x, to.x);
            float minY = Mathf.Min(from.y, to.y);
            float maxY = Mathf.Max(from.y, to.y);

            float margin = HitTestThreshold;
            return new Rect(minX - margin, minY - margin, maxX - minX + margin * 2, maxY - minY + margin * 2);
        }

        public bool ContainsPoint(Vector2 graphPoint)
        {
            Vector3 from = From.GetCenter();
            Vector3 to = To.GetCenter();

            Vector3 line = to - from;
            float lineLen = line.magnitude;
            if (lineLen < 0.001f)
                return Vector2.Distance(graphPoint, from) <= HitTestThreshold;

            Vector3 dir = line / lineLen;
            Vector3 toPoint = graphPoint - (Vector2)from;
            float projection = Vector3.Dot(toPoint, dir);

            Vector3 closest;
            if (projection <= 0f)
                closest = from;
            else if (projection >= lineLen)
                closest = to;
            else
                closest = from + dir * projection;

            return Vector2.Distance(graphPoint, closest) <= HitTestThreshold;
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
            DrawLine(From.GetCenter() * zoom + panOffset, To.GetCenter() * zoom + panOffset, SelectedColor, Mathf.Max(1f, (SelectedBaseWidth + 3f) * zoom));
        }

        public void Draw(float zoom, Vector2 panOffset)
        {
            Vector3 startPos = From.GetCenter() * zoom + panOffset;
            Vector3 endPos = To.GetCenter() * zoom + panOffset;

            Color color = IsSelected ? SelectedColor : ConnectionColor;
            float width = Mathf.Max(1f, (IsSelected ? SelectedBaseWidth : BaseWidth) * zoom);

            DrawLine(startPos, endPos, color, width);
        }

        private static void DrawLine(Vector3 start, Vector3 end, Color color, float width)
        {
            Vector3 dir = (end - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float halfW = width * 0.5f;

            Vector3[] corners = new Vector3[]
            {
                start + perp * halfW,
                start - perp * halfW,
                end - perp * halfW,
                end + perp * halfW,
            };

            Handles.DrawSolidRectangleWithOutline(corners, color, color);
        }
    }
}
