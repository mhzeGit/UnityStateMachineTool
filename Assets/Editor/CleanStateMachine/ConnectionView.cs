using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConnectionView : ISelectable
    {
        public StateView From { get; }
        public StateView To { get; }
        public bool IsSelected { get; set; }
        public bool IsActive { get; set; }
        public double ActivationTime { get; set; }
        public float PerpendicularOffset { get; set; }
        public MonoScript ConditionScript { get; set; }
        public ConditionScript ConditionInstance { get; set; }

        private static readonly Color ConnectionColor = new Color(0.537f, 0.706f, 0.980f, 0.85f);
        private static readonly Color SelectedColor = new Color(0.537f, 0.706f, 0.980f, 1f);
        private const float HitTestThreshold = 10f;
        private const float ArrowGraphSize = 10f;
        private const float ArrowGraphWidth = 5f;
        private const float BaseWidth = 2f;
        private const float SelectedBaseWidth = 3f;

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

            Vector2 a = From.GetCenter();
            Vector2 b = To.GetCenter();
            Vector2 dir = (b - a).normalized;

            if (From.GetHashCode() > To.GetHashCode())
                dir = -dir;

            return new Vector2(-dir.y, dir.x) * PerpendicularOffset;
        }

        private void GetLineEndpoints(out Vector2 from, out Vector2 to)
        {
            from = From.GetCenter() + GetOffsetVector();
            to = To.GetCenter() + GetOffsetVector();
        }

        private void GetArrowheadVertices(Vector2 from, Vector2 to, out Vector2 tip, out Vector2 left, out Vector2 right)
        {
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            tip = (from + to) * 0.5f;
            Vector2 basePt = tip - dir * ArrowGraphSize;
            left = basePt + perp * ArrowGraphWidth;
            right = basePt - perp * ArrowGraphWidth;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.001f)
                return Vector2.Distance(p, a);

            Vector2 dir = ab / len;
            float t = Vector2.Dot(p - a, dir);
            Vector2 closest;

            if (t <= 0f)
                closest = a;
            else if (t >= len)
                closest = b;
            else
                closest = a + dir * t;

            return Vector2.Distance(p, closest);
        }

        public Rect GetGraphBounds()
        {
            GetLineEndpoints(out Vector2 from, out Vector2 to);
            GetArrowheadVertices(from, to, out Vector2 tip, out Vector2 left, out Vector2 right);

            float minX = Mathf.Min(from.x, to.x, tip.x, left.x, right.x) - HitTestThreshold;
            float maxX = Mathf.Max(from.x, to.x, tip.x, left.x, right.x) + HitTestThreshold;
            float minY = Mathf.Min(from.y, to.y, tip.y, left.y, right.y) - HitTestThreshold;
            float maxY = Mathf.Max(from.y, to.y, tip.y, left.y, right.y) + HitTestThreshold;

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public bool ContainsPoint(Vector2 graphPoint)
        {
            GetLineEndpoints(out Vector2 from, out Vector2 to);

            if (DistToSegment(graphPoint, from, to) <= HitTestThreshold)
                return true;

            GetArrowheadVertices(from, to, out Vector2 tip, out Vector2 left, out Vector2 right);

            if (DistToSegment(graphPoint, tip, left) <= HitTestThreshold ||
                DistToSegment(graphPoint, left, right) <= HitTestThreshold ||
                DistToSegment(graphPoint, right, tip) <= HitTestThreshold)
                return true;

            return false;
        }

        public void DrawSelectionOverlay(float zoom, Vector2 panOffset)
        {
        }

        public void Draw(float zoom, Vector2 panOffset)
        {
            Vector2 offsetVec = GetOffsetVector() * zoom;
            Vector3 startPos = (Vector3)(From.GetCenter() * zoom + panOffset + offsetVec);
            Vector3 endPos = (Vector3)(To.GetCenter() * zoom + panOffset + offsetVec);

            bool isActive = IsActive;
            Color color = IsSelected ? SelectedColor : (isActive ? UITheme.ActiveConnection : ConnectionColor);
            float width = Mathf.Max(1f, (IsSelected ? SelectedBaseWidth : BaseWidth) * zoom);

            DrawLine(startPos, endPos, color, width);
            DrawMidArrowhead(startPos, endPos, color, zoom);

            if (isActive)
                DrawActiveWave(startPos, endPos, zoom);
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

        private void DrawActiveWave(Vector3 start, Vector3 end, float zoom)
        {
            double elapsed = Time.realtimeSinceStartup - ActivationTime;
            float fade = Mathf.Clamp01(1f - (float)(elapsed / 1.8));
            if (fade <= 0.01f)
            {
                IsActive = false;
                return;
            }
            fade = fade * fade;

            Vector3 dir = (end - start).normalized;
            float totalLen = Vector3.Distance(start, end);
            if (totalLen < 0.01f) return;

            float speed = 1.5f;
            int circleCount = 5;
            float circleRadius = Mathf.Max(1.5f, 3f * zoom);

            Color prevColor = Handles.color;

            for (int i = 0; i < circleCount; i++)
            {
                float phase = (float)i / circleCount;
                float t = (Time.realtimeSinceStartup * speed + phase) % 1.0f;

                Vector3 pos = start + dir * (t * totalLen);

                Color circleColor = UITheme.ActiveConnectionWave;
                circleColor.a *= fade * (0.5f + 0.3f * Mathf.Sin(i * 2.5f + 1f));
                Handles.color = circleColor;
                Handles.DrawSolidDisc(pos, Vector3.forward, circleRadius);
            }

            if (fade < 0.5f)
            {
                Color color = UITheme.ActiveConnection;
                color.a *= fade * 2f;
                float width = Mathf.Max(1f, 2f * zoom);
                DrawLine(start, end, color, width);
                DrawMidArrowhead(start, end, color, zoom);
            }

            Handles.color = prevColor;
        }
    }
}
