using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConnectionView : ISelectable
    {
        public StateView From { get; }
        public StateView To { get; }
        public bool IsSelected { get; set; }
        public float PerpendicularOffset { get; set; }

        private static readonly Color ConnectionColor = new Color(0.60f, 0.80f, 1.00f, 1.00f);
        private static readonly Color SelectedColor = new Color(0.80f, 0.92f, 1.00f, 1.00f);
        private const float HitTestThreshold = 3f;
        private const float BoundsMargin = 0.5f;
        private const float BaseWidth = 1.5f;
        private const float SelectedBaseWidth = 2f;

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

        private Vector2 GetOffsetVector()
        {
            if (PerpendicularOffset == 0f)
                return Vector2.zero;

            Vector2 from = From.GetCenter();
            Vector2 to = To.GetCenter();
            Vector2 dir = (to - from).normalized;
            return new Vector2(-dir.y, dir.x) * PerpendicularOffset;
        }

        public Rect GetGraphBounds()
        {
            Vector3 from = From.GetCenter() + GetOffsetVector();
            Vector3 to = To.GetCenter() + GetOffsetVector();

            float minX = Mathf.Min(from.x, to.x);
            float maxX = Mathf.Max(from.x, to.x);
            float minY = Mathf.Min(from.y, to.y);
            float maxY = Mathf.Max(from.y, to.y);

            float margin = BoundsMargin;
            return new Rect(minX - margin, minY - margin, maxX - minX + margin * 2, maxY - minY + margin * 2);
        }

        public bool ContainsPoint(Vector2 graphPoint)
        {
            Vector3 from = From.GetCenter() + GetOffsetVector();
            Vector3 to = To.GetCenter() + GetOffsetVector();

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
        }

        public void Draw(float zoom, Vector2 panOffset)
        {
            Vector2 offsetVec = GetOffsetVector() * zoom;
            Vector3 startPos = (Vector3)(From.GetCenter() * zoom + panOffset + offsetVec);
            Vector3 endPos = (Vector3)(To.GetCenter() * zoom + panOffset + offsetVec);

            Color color = IsSelected ? SelectedColor : ConnectionColor;
            float width = Mathf.Max(1f, (IsSelected ? SelectedBaseWidth : BaseWidth) * zoom);

            DrawLine(startPos, endPos, color, width);
            DrawMidArrowhead(startPos, endPos, color, zoom);
        }

        private static void DrawLine(Vector3 start, Vector3 end, Color color, float width)
        {
            Vector3 dir = (end - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float halfW = width * 0.5f;

            Color prev = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(
                start + perp * halfW,
                start - perp * halfW,
                end - perp * halfW,
                end + perp * halfW
            );
            Handles.color = prev;
        }

        private static void DrawMidArrowhead(Vector3 start, Vector3 end, Color color, float zoom)
        {
            Vector3 mid = (start + end) * 0.5f;
            Vector3 dir = (end - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            float arrowSize = Mathf.Max(6f, 10f * zoom);
            float arrowWidth = arrowSize * 0.5f;
            Vector3 basePt = mid - dir * arrowSize;

            Color prev = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(mid, basePt + perp * arrowWidth, basePt - perp * arrowWidth);
            Handles.color = prev;
        }
    }
}
