using UnityEngine;

namespace CleanStateMachine
{
    public class GraphPanController
    {
        public bool IsPanning { get; private set; }

        private Vector2 _panStartMouse;
        private Vector2 _panStartOffset;

        private const float ZoomMin = 0.1f;
        private const float ZoomMax = 5f;
        private const float ZoomStep = 0.05f;

        public void HandleInput(Rect rect, ref Vector2 panOffset, ref float zoom)
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 1 && rect.Contains(e.mousePosition):
                    IsPanning = true;
                    _panStartMouse = e.mousePosition;
                    _panStartOffset = panOffset;
                    e.Use();
                    break;

                case EventType.MouseDrag when IsPanning:
                    panOffset = _panStartOffset + e.mousePosition - _panStartMouse;
                    e.Use();
                    break;

                case EventType.MouseUp when IsPanning:
                    IsPanning = false;
                    e.Use();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition):
                    float zoomFactor = 1f + Mathf.Abs(e.delta.y) * ZoomStep;
                    float newZoom = e.delta.y > 0
                        ? zoom / zoomFactor
                        : zoom * zoomFactor;
                    newZoom = Mathf.Clamp(newZoom, ZoomMin, ZoomMax);

                    Vector2 mouseScreen = e.mousePosition;
                    Vector2 worldPos = (mouseScreen - panOffset) / zoom;
                    panOffset = mouseScreen - worldPos * newZoom;
                    zoom = newZoom;

                    e.Use();
                    break;
            }
        }
    }
}
