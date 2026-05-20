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

        private static readonly Color PendingColor = new Color(0.60f, 0.80f, 1.00f, 1.00f);

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

            float width = Mathf.Max(1f, 1f * zoom);
            DrawLine(startPos, endPos, PendingColor, width);

            if (Vector3.Distance(startPos, endPos) > 1f)
                DrawMidArrowhead(startPos, endPos, PendingColor, zoom);
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
