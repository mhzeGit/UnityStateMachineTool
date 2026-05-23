using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class ConnectionArrowsLayer : VisualElement
    {
        private readonly List<ConnectionView> _connections;
        private readonly ConnectionController _connectionController;
        private float _zoom = 1f;
        private Vector2 _panOffset;

        private static readonly Color ConnectionColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color SelectedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color PendingColor = new Color(0.60f, 0.80f, 1.00f, 1.00f);
        private static readonly Color ActiveConnectionColor = new Color(1.0f, 1.0f, 1.0f, 1f);
        private static readonly Color ActiveConnectionWaveColor = new Color(0.4f, 0.9f, 0.4f, 1f);

        private const float ArrowGraphSize = 10f;
        private const float ArrowGraphWidth = 5f;
        private const float BaseWidth = 2f;
        private const float SelectedBaseWidth = 3f;
        private const float FeatherPixels = 0.8f;

        public ConnectionArrowsLayer(List<ConnectionView> connections, ConnectionController connectionController)
        {
            _connections = connections;
            _connectionController = connectionController;
            generateVisualContent += OnGenerateVisualContent;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            style.overflow = Overflow.Hidden;
        }

        public void UpdateView(float zoom, Vector2 panOffset)
        {
            _zoom = zoom;
            _panOffset = panOffset;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            DrawAllConnections(mgc);
            DrawPendingConnection(mgc);
        }

        private void DrawAllConnections(MeshGenerationContext mgc)
        {
            for (int i = 0; i < _connections.Count; i++)
            {
                var conn = _connections[i];
                GetScreenEndpoints(conn, out Vector3 startPos, out Vector3 endPos);

                float fade = 0f;
                double elapsed = 0;
                if (conn.IsActive)
                {
                    elapsed = Time.realtimeSinceStartup - conn.ActivationTime;
                    fade = Mathf.Clamp01(1f - (float)(elapsed / 3.0));
                    if (fade <= 0.01f)
                    {
                        conn.IsActive = false;
                        fade = 0f;
                    }
                    else
                    {
                        fade *= fade;
                    }
                }

                float widthMultiplier = 1f;
                if (fade > 0.01f)
                {
                    const float burstDuration = 0.25f;
                    if (elapsed < burstDuration)
                    {
                        float t = (float)(elapsed / burstDuration);
                        widthMultiplier = 1f + 1f * (1f - t) * (1f - t);
                    }
                }

                Color color = conn.IsSelected ? SelectedColor : Color.Lerp(ConnectionColor, ActiveConnectionColor, fade);
                float width = Mathf.Max(1f, (conn.IsSelected ? SelectedBaseWidth : BaseWidth) * _zoom * widthMultiplier);

                DrawLine(mgc, startPos, endPos, color, width);
                DrawMidArrowhead(mgc, startPos, endPos, color, _zoom);

                if (fade > 0.01f)
                    DrawActiveWave(mgc, startPos, endPos, _zoom, fade, color, elapsed);
            }
        }

        private void DrawPendingConnection(MeshGenerationContext mgc)
        {
            if (!_connectionController.IsConnecting) return;

            Vector3 startPos = _connectionController.SourceNode.GetCenter() * _zoom + _panOffset;
            Vector3 endPos = _connectionController.CurrentMouseGraphPos * _zoom + _panOffset;

            float width = Mathf.Max(1f, 1f * _zoom);
            DrawLine(mgc, startPos, endPos, PendingColor, width);

            if (Vector3.Distance(startPos, endPos) > 1f)
                DrawMidArrowhead(mgc, startPos, endPos, PendingColor, _zoom);
        }

        private Vector2 GetOffsetVector(ConnectionView conn)
        {
            if (conn.PerpendicularOffset == 0f) return Vector2.zero;
            Vector2 a = conn.From.GetCenter();
            Vector2 b = conn.To.GetCenter();
            Vector2 dir = (b - a).normalized;
            if (conn.From.GetHashCode() > conn.To.GetHashCode()) dir = -dir;
            return new Vector2(-dir.y, dir.x) * conn.PerpendicularOffset;
        }

        private void GetScreenEndpoints(ConnectionView conn, out Vector3 from, out Vector3 to)
        {
            Vector2 offset = GetOffsetVector(conn) * _zoom;
            from = conn.From.GetCenter() * _zoom + _panOffset + offset;
            to = conn.To.GetCenter() * _zoom + _panOffset + offset;
        }

        private static void DrawLine(MeshGenerationContext mgc, Vector3 start, Vector3 end, Color color, float width)
        {
            Vector3 dir = (end - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            float halfW = width * 0.5f;
            float feather = Mathf.Max(0.5f, FeatherPixels);

            var mesh = mgc.Allocate(8, 18);

            Color cEdge = new Color(color.r, color.g, color.b, 0f);

            Vector3 slo = start + perp * (halfW + feather);
            Vector3 sli = start + perp * halfW;
            Vector3 sri = start - perp * halfW;
            Vector3 sro = start - perp * (halfW + feather);

            Vector3 elo = end + perp * (halfW + feather);
            Vector3 eli = end + perp * halfW;
            Vector3 eri = end - perp * halfW;
            Vector3 ero = end - perp * (halfW + feather);

            mesh.SetNextVertex(new Vertex { position = slo, tint = cEdge });
            mesh.SetNextVertex(new Vertex { position = sli, tint = color });
            mesh.SetNextVertex(new Vertex { position = sri, tint = color });
            mesh.SetNextVertex(new Vertex { position = sro, tint = cEdge });

            mesh.SetNextVertex(new Vertex { position = elo, tint = cEdge });
            mesh.SetNextVertex(new Vertex { position = eli, tint = color });
            mesh.SetNextVertex(new Vertex { position = eri, tint = color });
            mesh.SetNextVertex(new Vertex { position = ero, tint = cEdge });

            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(5);
            mesh.SetNextIndex(0); mesh.SetNextIndex(5); mesh.SetNextIndex(4);

            mesh.SetNextIndex(1); mesh.SetNextIndex(2); mesh.SetNextIndex(6);
            mesh.SetNextIndex(1); mesh.SetNextIndex(6); mesh.SetNextIndex(5);

            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(7);
            mesh.SetNextIndex(2); mesh.SetNextIndex(7); mesh.SetNextIndex(6);
        }

        private static void DrawMidArrowhead(MeshGenerationContext mgc, Vector3 start, Vector3 end, Color color, float zoom)
        {
            Vector3 mid = (start + end) * 0.5f;
            Vector3 dir = (end - start).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            float arrowSize = Mathf.Max(6f, 10f * zoom);
            float arrowWidth = arrowSize * 0.5f;
            Vector3 basePt = mid - dir * arrowSize;

            Vector3 tip = mid;
            Vector3 left = basePt + perp * arrowWidth;
            Vector3 right = basePt - perp * arrowWidth;

            Vector3 centroid = (tip + left + right) / 3f;
            float feather = Mathf.Max(0.5f, FeatherPixels);

            Color cEdge = new Color(color.r, color.g, color.b, 0f);

            float offsetScale = feather / Mathf.Max(0.1f,
                Vector3.Distance(centroid, tip) +
                Vector3.Distance(centroid, left) +
                Vector3.Distance(centroid, right) / 3f);

            Vector3 tipO = tip + (tip - centroid).normalized * feather;
            Vector3 leftO = left + (left - centroid).normalized * feather;
            Vector3 rightO = right + (right - centroid).normalized * feather;

            var mesh = mgc.Allocate(6, 21);

            mesh.SetNextVertex(new Vertex { position = tip, tint = color });
            mesh.SetNextVertex(new Vertex { position = left, tint = color });
            mesh.SetNextVertex(new Vertex { position = right, tint = color });
            mesh.SetNextVertex(new Vertex { position = tipO, tint = cEdge });
            mesh.SetNextVertex(new Vertex { position = leftO, tint = cEdge });
            mesh.SetNextVertex(new Vertex { position = rightO, tint = cEdge });

            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);

            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(4);
            mesh.SetNextIndex(0); mesh.SetNextIndex(4); mesh.SetNextIndex(3);

            mesh.SetNextIndex(1); mesh.SetNextIndex(2); mesh.SetNextIndex(5);
            mesh.SetNextIndex(1); mesh.SetNextIndex(5); mesh.SetNextIndex(4);

            mesh.SetNextIndex(2); mesh.SetNextIndex(0); mesh.SetNextIndex(3);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(5);
        }

        private static void DrawActiveWave(MeshGenerationContext mgc, Vector3 start, Vector3 end, float zoom, float fade, Color arrowColor, double elapsed)
        {
            Vector3 dir = (end - start).normalized;
            float totalLen = Vector3.Distance(start, end);
            if (totalLen < 0.01f) return;

            const float burstDuration = 0.25f;
            const float burstPeak = 2.5f;

            float burst = 1f;
            if (elapsed < burstDuration)
            {
                float t = (float)(elapsed / burstDuration);
                burst = 1f + (burstPeak - 1f) * (1f - t) * (1f - t);
            }

            float speed = 0.8f * burst;
            int circleCount = 5;
            float circleRadius = Mathf.Max(2f, 4f * zoom) * burst;

            Color waveColor = new Color(arrowColor.r, arrowColor.g, arrowColor.b, arrowColor.a * fade);

            if (burst > 1f)
            {
                float burstAlpha = (burst - 1f) / (burstPeak - 1f);
                waveColor = Color.Lerp(waveColor, Color.white, burstAlpha * burstAlpha);
                waveColor.a = arrowColor.a * fade;
            }

            for (int i = 0; i < circleCount; i++)
            {
                float phase = (float)i / circleCount;
                float t = (Time.realtimeSinceStartup * speed + phase) % 1.0f;

                Vector3 pos = start + dir * (t * totalLen);

                DrawCircle(mgc, pos, circleRadius, waveColor);
            }
        }

        private static void DrawCircle(MeshGenerationContext mgc, Vector3 center, float radius, Color color)
        {
            int segments = 12;
            float feather = Mathf.Max(0.5f, FeatherPixels);
            int vertCount = 1 + segments * 2;
            int indexCount = segments * 3 + segments * 6;

            var mesh = mgc.Allocate(vertCount, indexCount);
            Color cEdge = new Color(color.r, color.g, color.b, 0f);

            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, 0f), tint = color });

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Vector3 innerPos = new Vector3(center.x + cos * radius, center.y + sin * radius, 0f);
                Vector3 outerPos = new Vector3(center.x + cos * (radius + feather), center.y + sin * (radius + feather), 0f);

                mesh.SetNextVertex(new Vertex { position = innerPos, tint = color });
                mesh.SetNextVertex(new Vertex { position = outerPos, tint = cEdge });
            }

            for (int i = 0; i < segments; i++)
            {
                int ni = (i + 1) % segments;
                ushort innerI = (ushort)(1 + i * 2);
                ushort innerN = (ushort)(1 + ni * 2);
                ushort outerI = (ushort)(1 + i * 2 + 1);
                ushort outerN = (ushort)(1 + ni * 2 + 1);

                mesh.SetNextIndex(0);
                mesh.SetNextIndex(innerI);
                mesh.SetNextIndex(innerN);

                mesh.SetNextIndex(innerI);
                mesh.SetNextIndex(outerI);
                mesh.SetNextIndex(outerN);

                mesh.SetNextIndex(innerI);
                mesh.SetNextIndex(outerN);
                mesh.SetNextIndex(innerN);
            }
        }
    }
}
