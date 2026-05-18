using UnityEngine;

namespace CleanStateMachine
{
    public class GraphPanController
    {
        public bool IsPanning { get; private set; }

        private Vector2 _panStartMouse;
        private Vector2 _panStartOffset;

        public void HandleInput(Rect rect, ref Vector2 panOffset)
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
            }
        }
    }
}
