using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class ConnectionController
    {
        public bool IsConnecting { get; private set; }
        public StateView SourceNode { get; private set; }

        private Vector2 _currentMouseGraphPos;

        public event Action<StateView, StateView> ConnectionCompleted;

        private static readonly Color PendingColor = new Color(0.60f, 0.80f, 1.00f, 0.45f);

        public void StartConnection(StateView source)
        {
            SourceNode = source;
            IsConnecting = true;
            _currentMouseGraphPos = source.GetCenter();
        }

        public void UpdatePending(Vector2 graphMousePos)
        {
            _currentMouseGraphPos = graphMousePos;
        }

        public bool TryComplete(Vector2 graphMousePos, IReadOnlyList<StateView> allStates)
        {
            if (!IsConnecting)
                return false;

            for (int i = allStates.Count - 1; i >= 0; i--)
            {
                if (allStates[i] != SourceNode && allStates[i].ContainsPoint(graphMousePos))
                {
                    ConnectionCompleted?.Invoke(SourceNode, allStates[i]);
                    Cancel();
                    return true;
                }
            }

            return false;
        }

        public void Cancel()
        {
            IsConnecting = false;
            SourceNode = null;
        }

        public void DrawPending(float zoom, Vector2 panOffset)
        {
            if (!IsConnecting)
                return;

            Vector3 startPos = SourceNode.GetCenter() * zoom + panOffset;
            Vector3 endPos = _currentMouseGraphPos * zoom + panOffset;

            Vector3 dir = (endPos - startPos).normalized;
            float distance = Vector3.Distance(startPos, endPos);
            float tangentStrength = Mathf.Max(distance * 0.5f, 50f);
            Vector3 startTan = startPos + dir * tangentStrength;
            Vector3 endTan = endPos - dir * tangentStrength;

            Handles.DrawBezier(startPos, endPos, startTan, endTan, PendingColor, null, 2f);

            if (distance > 1f)
            {
                EvaluateCubicBezier(startPos, startTan, endTan, endPos, 0.5f, out Vector3 mid, out Vector3 tan);
                DrawArrowhead(mid, tan, PendingColor, zoom);
            }
        }

        private static void EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, out Vector3 position, out Vector3 tangent)
        {
            float u = 1f - t;
            float u2 = u * u;
            float u3 = u2 * u;
            float t2 = t * t;
            float t3 = t2 * t;

            position = u3 * p0 + 3f * u2 * t * p1 + 3f * u * t2 * p2 + t3 * p3;
            tangent = 3f * u2 * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t2 * (p3 - p2);
        }

        private static void DrawArrowhead(Vector3 tip, Vector3 tangent, Color color, float zoom)
        {
            float size = Mathf.Max(8f, 12f * zoom);
            Vector3 dir = tangent.normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            Vector3 p1 = tip;
            Vector3 p2 = tip - dir * size + perp * (size * 0.4f);
            Vector3 p3 = tip - dir * size - perp * (size * 0.4f);

            Color prev = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(p1, p2, p3);
            Handles.color = prev;
        }
    }
}
