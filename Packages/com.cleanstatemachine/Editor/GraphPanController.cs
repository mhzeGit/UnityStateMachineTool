using UnityEngine;

namespace CleanStateMachine
{
    public class GraphPanController
    {
        public bool IsPanning { get; private set; }

        private Vector2 _panStartMouse;
        private Vector2 _panStartOffset;
        private bool _hasDragged;

        private const float ZoomMin = 0.1f;
        private const float ZoomMax = 5f;
        private const float ZoomSensitivity = 0.05f;
        private const float ZoomClamp = 0.5f;

        private const float ScrollThreshold = 2.5f;
        private const float TouchpadPanScale = 20f;
        private const float ZoomSensitivityTouchpad = 0.1f;

        public void HandleInput(Rect rect, ref Vector2 panOffset, ref float zoom)
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDrag when e.button == 1 && rect.Contains(e.mousePosition) && !IsPanning:
                    IsPanning = true;
                    _panStartMouse = e.mousePosition;
                    _panStartOffset = panOffset;
                    _hasDragged = true;
                    e.Use();
                    break;

                case EventType.MouseDrag when IsPanning:
                    panOffset = _panStartOffset + e.mousePosition - _panStartMouse;
                    _hasDragged = true;
                    e.Use();
                    break;

                case EventType.MouseUp when IsPanning:
                    IsPanning = false;
                    e.Use();
                    break;

                case EventType.ScrollWheel when rect.Contains(e.mousePosition):
                    HandleScrollWheel(e, ref panOffset, ref zoom);
                    e.Use();
                    break;
            }
        }

        public bool ConsumeContextClickIfPanned()
        {
            if (_hasDragged)
            {
                _hasDragged = false;
                return true;
            }
            return false;
        }

        public void CancelPanning()
        {
            IsPanning = false;
            _hasDragged = false;
        }

        private void HandleScrollWheel(Event e, ref Vector2 panOffset, ref float zoom)
        {
            bool hasX = !Mathf.Approximately(e.delta.x, 0f);
            bool hasY = !Mathf.Approximately(e.delta.y, 0f);

            if (hasX && hasY)
            {
                ApplyZoom(e.delta.y, e.mousePosition, ref panOffset, ref zoom, ZoomSensitivityTouchpad);
            }
            else if (hasX)
            {
                panOffset.x -= e.delta.x * TouchpadPanScale;
            }
            else if (hasY && Mathf.Abs(e.delta.y) < ScrollThreshold)
            {
                panOffset.y -= e.delta.y * TouchpadPanScale;
            }
            else if (hasY)
            {
                ApplyZoom(e.delta.y, e.mousePosition, ref panOffset, ref zoom, ZoomSensitivity);
            }
        }

        private static void ApplyZoom(float deltaY, Vector2 mousePosition, ref Vector2 panOffset, ref float zoom, float sensitivity)
        {
            float clamped = Mathf.Clamp(-deltaY * sensitivity, -ZoomClamp, ZoomClamp);
            float newZoom = Mathf.Clamp(zoom * Mathf.Exp(clamped), ZoomMin, ZoomMax);

            Vector2 worldPos = (mousePosition - panOffset) / zoom;
            panOffset = mousePosition - worldPos * newZoom;
            zoom = newZoom;
        }
    }
}
