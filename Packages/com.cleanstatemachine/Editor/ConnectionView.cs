using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConditionEntryView
    {
        public MonoScript Script;
        public ConditionScript Instance;
    }

    public class ConnectionView : ISelectable
    {
        public StateView From { get; }
        public StateView To { get; }
        public bool IsSelected { get; set; }
        public bool IsActive { get; set; }
        public double ActivationTime { get; set; }
        public float PerpendicularOffset { get; set; }
        public List<ConditionEntryView> ConditionEntries { get; set; } = new List<ConditionEntryView>();

        private const float HitTestThreshold = 10f;
        private const float ArrowGraphSize = 10f;
        private const float ArrowGraphWidth = 5f;

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

        public bool BoxOverlaps(Rect selectionRect)
        {
            GetLineEndpoints(out Vector2 from, out Vector2 to);

            if (LineIntersectsRect(from, to, selectionRect))
                return true;

            GetArrowheadVertices(from, to, out Vector2 tip, out Vector2 left, out Vector2 right);

            if (LineIntersectsRect(tip, left, selectionRect))
                return true;
            if (LineIntersectsRect(left, right, selectionRect))
                return true;
            if (LineIntersectsRect(right, tip, selectionRect))
                return true;

            return false;
        }

        private static bool LineIntersectsRect(Vector2 a, Vector2 b, Rect r)
        {
            if (r.Contains(a) || r.Contains(b))
                return true;

            float xMin = r.xMin, xMax = r.xMax, yMin = r.yMin, yMax = r.yMax;

            if (SegmentsIntersect(a, b, new Vector2(xMin, yMin), new Vector2(xMax, yMin)))
                return true;
            if (SegmentsIntersect(a, b, new Vector2(xMin, yMax), new Vector2(xMax, yMax)))
                return true;
            if (SegmentsIntersect(a, b, new Vector2(xMin, yMin), new Vector2(xMin, yMax)))
                return true;
            if (SegmentsIntersect(a, b, new Vector2(xMax, yMin), new Vector2(xMax, yMax)))
                return true;

            return false;
        }

        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            Vector2 d1 = p2 - p1;
            Vector2 d2 = p4 - p3;

            float cross = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Approximately(cross, 0f))
                return false;

            Vector2 d3 = p3 - p1;
            float t = (d3.x * d2.y - d3.y * d2.x) / cross;
            float u = (d3.x * d1.y - d3.y * d1.x) / cross;

            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
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

            float minX = Mathf.Min(from.x, to.x, tip.x, left.x, right.x);
            float maxX = Mathf.Max(from.x, to.x, tip.x, left.x, right.x);
            float minY = Mathf.Min(from.y, to.y, tip.y, left.y, right.y);
            float maxY = Mathf.Max(from.y, to.y, tip.y, left.y, right.y);

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
    }
}
