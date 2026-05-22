using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class GridBackground : VisualElement
    {
        private Vector2 _panOffset;
        private float _zoom = 1f;

        private static readonly Color GridMinor = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color GridMajor = new Color(0.20f, 0.20f, 0.20f);
        private const float GridS = 20f;
        private const float GridL = 100f;
        private const float MinStep = 6f;

        public GridBackground()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void UpdateView(Vector2 panOffset, float zoom)
        {
            _panOffset = panOffset;
            _zoom = zoom;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            Rect rect = contentRect;
            if (rect.width < 1f || rect.height < 1f) return;

            float thickness = Mathf.Max(1f, _zoom);
            DrawGridLines(mgc, rect, _panOffset.x, sGridS, thickness, GridMinor, true);
            DrawGridLines(mgc, rect, _panOffset.y, sGridS, thickness, GridMinor, false);
            DrawGridLines(mgc, rect, _panOffset.x, sGridL, thickness, GridMajor, true);
            DrawGridLines(mgc, rect, _panOffset.y, sGridL, thickness, GridMajor, false);
        }

        private float sGridS => GridS * _zoom;
        private float sGridL => GridL * _zoom;

        private static void DrawGridLines(MeshGenerationContext mgc, Rect rect, float offset, float step, float thickness, Color color, bool isVertical)
        {
            if (step < MinStep) return;

            float half = thickness * 0.5f;
            float rangeStart = isVertical ? rect.xMin : rect.yMin;
            float rangeEnd   = isVertical ? rect.xMax : rect.yMax;
            float orthStart  = isVertical ? rect.yMin : rect.xMin;
            float orthEnd    = isVertical ? rect.yMax : rect.xMax;

            float o = offset % step;
            if (o < 0f) o += step;
            float first = rangeStart + o;
            if (first >= rangeEnd) return;

            int count = Mathf.CeilToInt((rangeEnd - first) / step);
            var mesh = mgc.Allocate(count * 4, count * 6);
            int vi = 0;
            float pos = first;

            while (pos < rangeEnd)
            {
                if (isVertical)
                    AddQuad(mesh, pos - half, orthStart, pos + half, orthEnd, color, ref vi);
                else
                    AddQuad(mesh, orthStart, pos - half, orthEnd, pos + half, color, ref vi);
                pos += step;
            }
        }

        private static void AddQuad(MeshWriteData mesh, float x1, float y1, float x2, float y2, Color color, ref int vi)
        {
            int baseIndex = vi;

            mesh.SetNextVertex(new Vertex { position = new Vector3(x1, y1, 0f), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x2, y1, 0f), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x1, y2, 0f), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x2, y2, 0f), tint = color });

            mesh.SetNextIndex((ushort)baseIndex);
            mesh.SetNextIndex((ushort)(baseIndex + 1));
            mesh.SetNextIndex((ushort)(baseIndex + 2));
            mesh.SetNextIndex((ushort)(baseIndex + 1));
            mesh.SetNextIndex((ushort)(baseIndex + 3));
            mesh.SetNextIndex((ushort)(baseIndex + 2));

            vi += 4;
        }
    }
}
